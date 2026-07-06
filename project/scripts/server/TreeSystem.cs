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
    private PackedScene _treeScene = null!;
    private PackedScene _woodDropScene = null!;

    // Server-only: HP per tree ID. SortedDictionary for deterministic iteration (ADR-0011).
    private readonly SortedDictionary<string, int> _treeHp = new();

    private TreeConfig _treeCfg = TreeConfig.Default;

    public override void _Ready()
    {
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

    private void FellTree(string treeId, long byPeer)
    {
        _treeHp.Remove(treeId);

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
