using System.Collections.Generic;
using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Server;

/// <summary>
/// Server-authoritative settlement manager.
///
/// Responsibilities:
///   - Track each peer's Kingdom Marker position (one per peer).
///   - Validate and execute building placement: resource cost, territory radius, map bounds.
///   - Broadcast marker/building spawns to all peers via RPC.
///
/// Territory rule: buildings may only be placed within TERRITORY_RADIUS world units
/// of the planting peer's Kingdom Marker.
///
/// Node must appear in GameWorld.tscn AFTER InventorySystem (cost deduction dependency).
/// </summary>
public partial class SettlementSystem : Node
{
    public static SettlementSystem Instance { get; private set; } = null!;

    // Server-only. SortedDictionary: ADR-0011 deterministic iteration.
    private readonly SortedDictionary<long, Vector3> _markers = new();

    public const float TERRITORY_RADIUS = 40f;

    /// <summary>
    /// Emitted on ALL peers when a Kingdom Marker is planted.
    /// PlacementController connects to this (via untyped path) to cache its own marker pos.
    /// </summary>
    [Signal]
    public delegate void MarkerPlantedEventHandler(long peerId, Vector3 position);

    public override void _Ready() => Instance = this;

    /// <summary>
    /// Returns true if peerId is the founder of their own settlement.
    /// Key in _markers is always the founder's peer ID — no separate FounderId field needed.
    /// See docs/gdd/settlements.md §1.4.
    /// </summary>
    public bool IsFounder(long peerId) => _markers.ContainsKey(peerId);

    // ── Kingdom Marker ────────────────────────────────────────────────────────

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestPlantMarker(Vector3 position)
    {
        if (!Multiplayer.IsServer()) return;

        long sender = Multiplayer.GetRemoteSenderId();
        if (sender == 0) sender = 1L; // direct call from host/solo

        if (_markers.ContainsKey(sender))
        {
            GD.Print($"[Settlement] peer {sender} already has a Kingdom Marker");
            return;
        }

        // Reject if the new territory circle would overlap any existing one.
        // Two circles of radius R overlap when centre-to-centre distance < 2R.
        foreach (var (otherId, otherPos) in _markers)
        {
            float dist = new Vector2(position.X, position.Z)
                             .DistanceTo(new Vector2(otherPos.X, otherPos.Z));
            if (dist < TERRITORY_RADIUS * 2f)
            {
                GD.Print($"[Settlement] peer {sender} marker rejected — too close to peer {otherId} ({dist:F1} < {TERRITORY_RADIUS * 2f})");
                return;
            }
        }

        float y = TerrainSystem.GetHeightAtWorld(position.X, position.Z) + 0.05f;
        var snapped = new Vector3(position.X, y, position.Z);
        _markers[sender] = snapped;

        GD.Print($"[Settlement] peer {sender} planted Kingdom Marker at {snapped}");
        Rpc(MethodName.SpawnMarker, sender, snapped);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SpawnMarker(long peerId, Vector3 position)
    {
        var scene = GD.Load<PackedScene>("res://scenes/KingdomMarker.tscn");
        var node  = scene.Instantiate<Node3D>();
        node.Name = $"marker_{peerId}";
        AddChild(node);
        node.GlobalPosition = position;

        // If this is the local peer's marker, record founder status in LocalState.
        // BuildMenu reads this for UX greying — server enforcement is via IsFounder().
        if (peerId == Multiplayer.GetUniqueId())
            LocalState.SetFounder();

        // Signal lets PlacementController cache this peer's territory centre.
        EmitSignal(SignalName.MarkerPlanted, peerId, position);
        GD.Print($"[Settlement] spawned Kingdom Marker for peer {peerId}");
    }

    // ── Building placement ────────────────────────────────────────────────────

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestPlaceBuilding(string buildingId, Vector3 position)
    {
        if (!Multiplayer.IsServer()) return;

        long sender = Multiplayer.GetRemoteSenderId();
        if (sender == 0) sender = 1L;

        var data = BuildingRegistry.Find(buildingId);
        if (data == null)
        {
            GD.PrintErr($"[Settlement] unknown buildingId '{buildingId}'");
            return;
        }

        // Must have a marker planted first.
        if (!_markers.TryGetValue(sender, out var marker))
        {
            GD.PrintErr($"[Settlement] peer {sender} has no Kingdom Marker — plant one with F first");
            return;
        }

        // Must be within territory.
        if (position.DistanceTo(marker) > TERRITORY_RADIUS)
        {
            GD.PrintErr($"[Settlement] peer {sender} tried to build outside territory");
            return;
        }

        // Must have enough resources.
        foreach (var (itemId, count) in data.Cost)
        {
            if (!InventorySystem.Instance.HasItems(sender, itemId, count))
            {
                GD.PrintErr($"[Settlement] peer {sender} missing {count}× {itemId} for {buildingId}");
                return;
            }
        }

        // Consume resources.
        foreach (var (itemId, count) in data.Cost)
            InventorySystem.Instance.RemoveItems(sender, itemId, count);

        // Snap Y to terrain surface.
        float y = TerrainSystem.GetHeightAtWorld(position.X, position.Z);
        var finalPos = new Vector3(position.X, y, position.Z);

        GD.Print($"[Settlement] peer {sender} placed {buildingId} at {finalPos}");
        Rpc(MethodName.SpawnBuilding, buildingId, finalPos);
    }

    /// <summary>
    /// Returns a safe respawn position for the given peer:
    /// just above their Kingdom Marker, or terrain origin if no marker planted yet.
    /// Called by NeedsSystem on player death.
    /// </summary>
    public Vector3 GetRespawnPosition(long peerId)
    {
        if (_markers.TryGetValue(peerId, out var markerPos))
            return markerPos + Vector3.Up * 1f;

        float y = TerrainSystem.GetHeightAtWorld(0f, 0f) + 0.6f;
        return new Vector3(0f, y, 0f);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SpawnBuilding(string buildingId, Vector3 position)
    {
        var data = BuildingRegistry.Find(buildingId);
        if (data == null) return;

        var scene = GD.Load<PackedScene>(data.ScenePath);
        var node  = scene.Instantiate<Node3D>();
        // Unique name avoids conflicts if multiple of the same type are placed.
        node.Name = $"{buildingId}_{(int)position.X}_{(int)position.Z}";
        AddChild(node);
        // Raise the node so its base sits on the terrain surface.
        node.GlobalPosition = position + Vector3.Up * (data.Height * 0.5f);

        GD.Print($"[Settlement] spawned {buildingId} at {position}");
    }
}
