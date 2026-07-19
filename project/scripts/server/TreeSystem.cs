using System.Collections.Generic;
using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Server;

/// <summary>
/// Spawns tree scene instances deterministically from seed (all peers run this, no sync RPC needed).
/// Server-side: tracks HP per tree, validates chop RPCs, broadcasts fell and regrow events.
///
/// After a tree is felled a stump mesh is shown. The server tracks a 300-second regrowth timer;
/// when it expires the stump is replaced by a full tree on all peers and HP is restored.
/// This removes the need to persist felled trees across save/load — GetFelledTreeIdsForSave()
/// returns an empty list and RestoreFelledTreesFromSave() is a no-op.
///
/// Tree nodes are added as children of this node so their NodePaths are identical on all peers
/// (/root/GameWorld/TreeSystem/tree.0, tree.1, …), making server→client RPCs work correctly.
/// </summary>
public partial class TreeSystem : Node
{
    public static TreeSystem Instance { get; private set; } = null!;

    private const double STUMP_REGROW_SEC = 300.0;

    private PackedScene _treeScene    = null!;
    private PackedScene _woodDropScene = null!;

    // Server-only: HP per tree ID. SortedDictionary for deterministic iteration (ADR-0011).
    private readonly SortedDictionary<string, int>    _treeHp       = new();
    // Server-only: when each stump should regrow (world-time seconds).
    private readonly SortedDictionary<string, double> _stumpTimers  = new();
    // Known world position for every tree (populated on all peers from deterministic generation).
    private readonly SortedDictionary<string, Vector3> _treePositions = new();

    private TreeConfig _treeCfg      = TreeConfig.Default;
    private double     _worldTimeSec = 0.0;

    public override void _Ready()
    {
        Instance = this;

        _treeCfg       = TreeConfig.Default;
        _treeScene     = GD.Load<PackedScene>("res://scenes/tree.tscn");
        _woodDropScene = GD.Load<PackedScene>("res://scenes/WoodDrop.tscn");

        // TerrainSystem._Ready() must run before TreeSystem._Ready() in the scene order.
        var heightmap = TerrainSystem.Heightmap;
        if (heightmap.Length == 0)
        {
            GD.PrintErr("[TreeSystem] TerrainSystem.Heightmap not ready — add TerrainSystem before TreeSystem in the scene tree.");
            return;
        }

        var trees = new TreeGenerator(GameSession.WorldSeed, TerrainConfig.Default, _treeCfg)
            .Generate(heightmap, TerrainSystem.River?.ChannelMask);

        foreach (var data in trees)
        {
            var pos  = new Vector3(data.WorldX, data.WorldY, data.WorldZ);
            var node = _treeScene.Instantiate<Node3D>();
            node.Name = data.Id;
            AddChild(node);
            node.GlobalPosition = pos;

            _treePositions[data.Id] = pos; // populated on all peers

            if (Multiplayer.IsServer())
                _treeHp[data.Id] = _treeCfg.TreeHp;
        }

        GD.Print($"[TreeSystem] placed {trees.Count} trees");
    }

    public override void _Process(double delta)
    {
        if (!Multiplayer.IsServer()) return;

        _worldTimeSec += delta;

        // Collect trees whose stumps have finished regrowing.
        var toRegrow = new List<string>();
        foreach (var (id, regrowAt) in _stumpTimers)
        {
            if (_worldTimeSec >= regrowAt)
                toRegrow.Add(id);
        }

        foreach (var id in toRegrow)
        {
            _stumpTimers.Remove(id);
            _treeHp[id] = _treeCfg.TreeHp; // restore HP so NPCs and players can chop again
            Rpc(MethodName.ClientRegrowTree, id);
            GD.Print($"[TreeSystem] {id} regrown after {STUMP_REGROW_SEC}s");
        }
    }

    // ── Chop RPC ─────────────────────────────────────────────────────────────

    /// <summary>Called by TreeNode on the client when the player hits E on a tree.</summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveChop(string treeId)
    {
        if (!Multiplayer.IsServer()) return;

        long sender = Multiplayer.GetRemoteSenderId();
        // Direct call (host/solo bypasses RPC) returns 0; normalise to peer 1.
        if (sender == 0) sender = 1L;

        if (!_treeHp.TryGetValue(treeId, out int hp)) return; // unknown or currently a stump

        hp -= 1;
        _treeHp[treeId] = hp;
        GD.Print($"[TreeSystem] {treeId} chopped by peer {sender}, HP={hp}");

        if (hp <= 0)
            FellTree(treeId, sender);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the IDs of all trees that still have HP (not currently stumps).
    /// Used by VillageSystem to find the nearest tree for NPC job assignment.
    /// </summary>
    public System.Collections.Generic.ICollection<string> GetAvailableTreeIds() => _treeHp.Keys;

    /// <summary>
    /// Server-side tree chop triggered by an NPC (no RPC hop needed).
    /// Wood yield goes to the settlement stockpile instead of player inventory.
    /// SkillSystem is NOT notified — NPC labour does not grant player XP.
    /// Returns wood yielded (WoodYield) when the tree is felled, 0 on intermediate chops.
    /// Caller (VillageSystem) is responsible for routing the yield to the stockpile.
    /// </summary>
    public int ServerChopTree(string treeId)
    {
        if (!Multiplayer.IsServer()) return 0;
        if (!_treeHp.TryGetValue(treeId, out int hp)) return 0; // stump or unknown

        hp -= 1;
        _treeHp[treeId] = hp;

        if (hp <= 0)
        {
            FellTreeForNpc(treeId);
            return _treeCfg.WoodYield;
        }
        return 0;
    }

    private void FellTreeForNpc(string treeId)
    {
        _treeHp.Remove(treeId);
        _stumpTimers[treeId] = _worldTimeSec + STUMP_REGROW_SEC;
        GD.Print($"[TreeSystem] NPC felled {treeId} — regrow in {STUMP_REGROW_SEC}s");

        var treeNode = GetNodeOrNull<Node3D>(treeId);
        var dropPos  = treeNode?.GlobalPosition ?? Vector3.Zero;
        Rpc(MethodName.OnTreeFelled, treeId, dropPos, _treeCfg.WoodYield);
    }

    private void FellTree(string treeId, long byPeer)
    {
        _treeHp.Remove(treeId);
        _stumpTimers[treeId] = _worldTimeSec + STUMP_REGROW_SEC;

        // Award woodcutting XP via the real skill system (M5).
        SkillSystem.Instance?.NotifyAction(byPeer, "skill.woodcutting");
        GD.Print($"[TreeSystem] peer {byPeer} felled {treeId} → +1 woodcutting XP — regrow in {STUMP_REGROW_SEC}s");

        // Give wood directly to the chopper's inventory.
        InventorySystem.Instance.AddItem(byPeer, "resource.wood", _treeCfg.WoodYield);

        var treeNode = GetNodeOrNull<Node3D>(treeId);
        var dropPos  = treeNode?.GlobalPosition ?? Vector3.Zero;

        Rpc(MethodName.OnTreeFelled, treeId, dropPos, _treeCfg.WoodYield);
    }

    // ── Save / Load (M8) ─────────────────────────────────────────────────────

    /// <summary>
    /// Stumps are transient — trees always regrow, nothing to persist.
    /// Returns an empty list; SaveData.FelledTreeIds field is kept in the schema for
    /// backward compatibility with saves written before regrowth was added.
    /// </summary>
    public List<string> GetFelledTreeIdsForSave() => new();

    /// <summary>No-op: trees that were felled in a prior session will be regrown on load.</summary>
    public void RestoreFelledTreesFromSave(List<string> felledIds)
    {
        // Trees regrow automatically via stump timers; no restoration needed.
    }

    // ── Tree fell broadcast ───────────────────────────────────────────────────

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void OnTreeFelled(string treeId, Vector3 dropPosition, int woodCount)
    {
        // Remove tree node.
        var treeNode = GetNodeOrNull<Node3D>(treeId);
        treeNode?.QueueFree();

        // Spawn stump mesh where the tree was.
        if (_treePositions.TryGetValue(treeId, out var treePos))
        {
            var stump = new MeshInstance3D
            {
                Name = $"stump_{treeId}",
                Mesh = new CylinderMesh
                {
                    TopRadius    = 0.25f,
                    BottomRadius = 0.30f,
                    Height       = 0.5f
                }
            };
            AddChild(stump);
            stump.GlobalPosition = treePos + Vector3.Up * 0.25f;
        }

        // Spawn wood drop visual (cosmetic, auto-despawns after 3 s).
        var drop = _woodDropScene.Instantiate<Node3D>();
        drop.Name = $"wood_{treeId}";
        GetParent().AddChild(drop);
        drop.GlobalPosition = dropPosition + Vector3.Up * 0.3f;

        var timer = GetTree().CreateTimer(3.0);
        timer.Timeout += () => { if (GodotObject.IsInstanceValid(drop)) drop.QueueFree(); };

        GD.Print($"[TreeSystem] {treeId} felled — {woodCount} wood at {dropPosition}");
    }

    // ── Tree regrow broadcast ─────────────────────────────────────────────────

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ClientRegrowTree(string treeId)
    {
        // Remove stump.
        var stump = GetNodeOrNull<Node3D>($"stump_{treeId}");
        stump?.QueueFree();

        // Re-instantiate tree scene at its original position.
        if (!_treePositions.TryGetValue(treeId, out var pos)) return;

        var tree = _treeScene.Instantiate<Node3D>();
        tree.Name = treeId;
        AddChild(tree);
        tree.GlobalPosition = pos;
    }
}
