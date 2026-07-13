using System.Collections.Generic;
using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Server;

/// <summary>
/// Spawns tree scene instances deterministically from seed (all peers run this, no sync RPC needed).
/// Server-side: tracks HP per tree, validates chop RPCs, broadcasts fell events.
///
/// Tree nodes are added as children of this node so their NodePaths are identical on all peers
/// (/root/GameWorld/TreeSystem/tree.0, tree.1, …), making server→client RPCs work correctly.
/// </summary>
public partial class TreeSystem : Node
{
    public static TreeSystem Instance { get; private set; } = null!;

    private PackedScene _treeScene = null!;
    private PackedScene _woodDropScene = null!;

    // Server-only: HP per tree ID. SortedDictionary for deterministic iteration (ADR-0011).
    private readonly SortedDictionary<string, int> _treeHp      = new();
    // Accumulated felled tree IDs — persisted in save so trees don't respawn on reload (M8).
    private readonly SortedSet<string>             _felledTreeIds = new();

    private TreeConfig _treeCfg = TreeConfig.Default;

    public override void _Ready()
    {
        Instance = this;

        _treeCfg      = TreeConfig.Default;
        _treeScene    = GD.Load<PackedScene>("res://scenes/tree.tscn");
        _woodDropScene = GD.Load<PackedScene>("res://scenes/WoodDrop.tscn");

        // TerrainSystem._Ready() must run before TreeSystem._Ready() in the scene order.
        var heightmap = TerrainSystem.Heightmap;
        if (heightmap.Length == 0)
        {
            GD.PrintErr("[TreeSystem] TerrainSystem.Heightmap not ready — add TerrainSystem before TreeSystem in the scene tree.");
            return;
        }

        var trees = new TreeGenerator(GameSession.WorldSeed, TerrainConfig.Default, _treeCfg)
            .Generate(heightmap);

        foreach (var data in trees)
        {
            var node = _treeScene.Instantiate<Node3D>();
            node.Name = data.Id;
            AddChild(node);
            node.GlobalPosition = new Vector3(data.WorldX, data.WorldY, data.WorldZ);

            if (Multiplayer.IsServer())
                _treeHp[data.Id] = _treeCfg.TreeHp;
        }

        GD.Print($"[TreeSystem] placed {trees.Count} trees");
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

        if (!_treeHp.TryGetValue(treeId, out int hp)) return; // unknown or already felled

        hp -= 1;
        _treeHp[treeId] = hp;
        GD.Print($"[TreeSystem] {treeId} chopped by peer {sender}, HP={hp}");

        if (hp <= 0)
            FellTree(treeId, sender);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the IDs of all trees that still have HP (not yet felled).
    /// Used by VillageSystem to find the nearest tree for NPC job assignment.
    /// </summary>
    public System.Collections.Generic.ICollection<string> GetAvailableTreeIds() => _treeHp.Keys;

    /// <summary>
    /// Server-side tree chop triggered by an NPC (no RPC hop needed).
    /// Wood yield goes to the settlement stockpile instead of player inventory.
    /// SkillSystem is NOT notified — NPC labour does not grant player XP.
    /// Called by VillageSystem job tick.
    /// </summary>
    /// <summary>
    /// Returns wood yielded (WoodYield) when the tree is felled, 0 on intermediate chops.
    /// Caller (VillageSystem) is responsible for routing the yield to the stockpile via the
    /// NPC haul loop — this method no longer calls AddToStockpile directly.
    /// </summary>
    public int ServerChopTree(string treeId)
    {
        if (!Multiplayer.IsServer()) return 0;
        if (!_treeHp.TryGetValue(treeId, out int hp)) return 0; // already felled or unknown

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
        _felledTreeIds.Add(treeId);
        // Wood yield is returned to VillageSystem via ServerChopTree — no direct stockpile call.
        GD.Print($"[TreeSystem] NPC felled {treeId}");

        var treeNode = GetNodeOrNull<Node3D>(treeId);
        var dropPos  = treeNode?.GlobalPosition ?? Vector3.Zero;
        Rpc(MethodName.OnTreeFelled, treeId, dropPos, _treeCfg.WoodYield);
    }

    private void FellTree(string treeId, long byPeer)
    {
        _treeHp.Remove(treeId);
        _felledTreeIds.Add(treeId);

        // Award woodcutting XP via the real skill system (M5).
        SkillSystem.Instance?.NotifyAction(byPeer, "skill.woodcutting");
        GD.Print($"[TreeSystem] peer {byPeer} felled {treeId} → +1 woodcutting XP");

        // Give wood directly to the chopper's inventory.
        InventorySystem.Instance.AddItem(byPeer, "resource.wood", _treeCfg.WoodYield);

        // Find the tree node's world position for the visual drop.
        var treeNode = GetNodeOrNull<Node3D>(treeId);
        var dropPos  = treeNode?.GlobalPosition ?? Vector3.Zero;

        // Tell all peers (+ server itself) to remove the tree and spawn the wood prop.
        Rpc(MethodName.OnTreeFelled, treeId, dropPos, _treeCfg.WoodYield);
    }

    // ── Save / Load (M8) ─────────────────────────────────────────────────────

    /// <summary>Returns IDs of all trees fully felled this session. Used by SaveSystem.</summary>
    public List<string> GetFelledTreeIdsForSave() => new List<string>(_felledTreeIds);

    /// <summary>
    /// Re-hides trees that were felled in a previous session.
    /// Removes each ID from _treeHp (so NPC job loops skip them) and fires
    /// ClientRemoveTreeNode on all peers to despawn the visual node.
    /// Partially-damaged trees (non-zero HP) are not persisted and reset to full.
    /// Called by SaveSystem.TryLoad().
    /// </summary>
    public void RestoreFelledTreesFromSave(List<string> felledIds)
    {
        if (!Multiplayer.IsServer()) return;

        int restored = 0;
        foreach (var id in felledIds)
        {
            if (!_treeHp.ContainsKey(id)) continue; // already gone or unknown
            _treeHp.Remove(id);
            _felledTreeIds.Add(id);
            Rpc(MethodName.ClientRemoveTreeNode, id);
            restored++;
        }
        GD.Print($"[TreeSystem] restored {restored} felled tree(s) from save");
    }

    /// <summary>
    /// Removes a tree node on all peers without spawning any wood drop prop.
    /// Used only for save/load restoration — the wood was already awarded in the prior session.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ClientRemoveTreeNode(string treeId)
    {
        var node = GetNodeOrNull<Node3D>(treeId);
        node?.QueueFree();
    }

    // ── Fell broadcast ────────────────────────────────────────────────────────

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void OnTreeFelled(string treeId, Vector3 dropPosition, int woodCount)
    {
        // Remove tree node from scene.
        var treeNode = GetNodeOrNull<Node3D>(treeId);
        treeNode?.QueueFree();

        // Spawn wood drop visual. Wood is already in the player's inventory;
        // this prop is purely cosmetic and auto-despawns after 30 seconds.
        var drop = _woodDropScene.Instantiate<Node3D>();
        drop.Name = $"wood_{treeId}";
        GetParent().AddChild(drop);
        drop.GlobalPosition = dropPosition + Vector3.Up * 0.3f;

        var timer = GetTree().CreateTimer(3.0);
        timer.Timeout += () => { if (GodotObject.IsInstanceValid(drop)) drop.QueueFree(); };

        GD.Print($"[TreeSystem] {treeId} felled — {woodCount} wood at {dropPosition}");
    }
}
