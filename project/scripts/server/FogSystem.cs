using System;
using System.Collections.Generic;
using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Server;

/// <summary>
/// Server-authoritative fog-of-war manager (worldgen.md §11).
///
/// Runs every FOG_UPDATE_INTERVAL seconds:
///   1. Collects positions of all connected player nodes.
///   2. Calls FogOfWarData.UpdateVisibility — marks newly explored cells as SEEN/VISIBLE.
///   3. If the grid changed, encodes to Base64 and broadcasts to all clients via
///      ClientApplyFog RPC so each client's LocalState.FogSnapshot is updated.
///
/// Save/load: GetFogBase64() / RestoreFogFromBase64() called by SaveSystem.
///
/// Node must appear in GameWorld.tscn AFTER SaveSystem (or at least in the same frame).
/// Add it after VillageSystem in the scene.
/// </summary>
public partial class FogSystem : Node
{
    public static FogSystem Instance { get; private set; } = null!;

    private const float  FOG_UPDATE_INTERVAL = 1.5f;
    private const string PLAYERS_PATH        = "/root/GameWorld/Players";

    private FogOfWarData _fog     = new();
    private float        _timer;

    public override void _Ready()
    {
        Instance = this;
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null!;
    }

    public override void _Process(double delta)
    {
        if (!Multiplayer.IsServer()) return;

        _timer += (float)delta;
        if (_timer < FOG_UPDATE_INTERVAL) return;
        _timer = 0f;

        var positions = CollectPlayerPositions();
        if (positions.Count == 0) return;

        bool changed = _fog.UpdateVisibility(positions);
        if (changed)
            BroadcastFog();
    }

    // ── Save / load API ───────────────────────────────────────────────────────

    /// <summary>Returns the current fog state encoded as a Base64 string for persistence.</summary>
    public string GetFogBase64() => Convert.ToBase64String(_fog.ToBytes());

    /// <summary>Restores fog from a previously saved Base64 string.</summary>
    public void RestoreFogFromBase64(string? base64)
    {
        if (string.IsNullOrEmpty(base64)) return;
        try
        {
            var bytes = Convert.FromBase64String(base64);
            _fog = FogOfWarData.FromBytes(bytes);
        }
        catch
        {
            GD.PrintErr("[Fog] Failed to decode saved fog data — starting fresh.");
            _fog = new FogOfWarData();
        }
    }

    // ── Broadcast ─────────────────────────────────────────────────────────────

    /// <summary>Sends the current fog state to a single peer (used on late-connect by SaveSystem).</summary>
    public void SendFogToClient(long peerId)
    {
        var base64 = Convert.ToBase64String(_fog.ToBytes());
        RpcId(peerId, MethodName.ClientApplyFog, base64);
    }

    /// <summary>
    /// Broadcasts the current fog state to all peers (CallLocal=true includes the host).
    /// Called by SaveSystem after restoring fog from a save, so clients don't keep the
    /// initial all-UNSEEN state while the server has the restored explored grid.
    /// </summary>
    public void BroadcastFog()
    {
        var base64 = Convert.ToBase64String(_fog.ToBytes());
        Rpc(MethodName.ClientApplyFog, base64);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ClientApplyFog(string base64)
    {
        try
        {
            var bytes = Convert.FromBase64String(base64);
            var grid  = FogOfWarData.FromBytes(bytes);
            LocalState.SetFog(grid);
        }
        catch
        {
            GD.PrintErr("[Fog] Failed to decode received fog data — ignoring.");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private List<(float X, float Z)> CollectPlayerPositions()
    {
        var result  = new List<(float, float)>();
        var players = GetNodeOrNull<Node>(PLAYERS_PATH);
        if (players == null) return result;

        foreach (Node child in players.GetChildren())
        {
            if (child is Node3D p3d)
                result.Add((p3d.GlobalPosition.X, p3d.GlobalPosition.Z));
        }
        return result;
    }
}
