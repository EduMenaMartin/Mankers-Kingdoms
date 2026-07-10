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
    private readonly SortedDictionary<long, Vector3> _markers   = new();
    private readonly SortedDictionary<string, int>   _stockpile = new();

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
        {
            LocalState.SetFounder();
            LocalState.SetMarkerWorldPos(position.X, position.Z);
        }

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

        // Must be the settlement founder — see docs/gdd/settlements.md §1.3.
        // (Place buildings: Founder ✅, Guest ❌)
        if (!IsFounder(sender))
        {
            GD.PrintErr($"[Settlement] peer {sender} tried to place building — not the founder");
            return;
        }
        // Guaranteed to exist after IsFounder check; also needed for territory distance.
        _markers.TryGetValue(sender, out var marker);

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
                // Notify the requesting peer so the client can show a HUD message.
                if (sender == Multiplayer.GetUniqueId())
                    LocalState.NotifyRejection(itemId);
                else
                    RpcId(sender, MethodName.ClientNotifyRejection, itemId);
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

    /// <summary>
    /// Sent from server to one specific client when their placement request is rejected.
    /// Passes the missing item ID (e.g. "resource.wood") so LocalState can compose
    /// "Not enough wood." without an extra loc entry per item.
    /// Routes through LocalState to avoid a server/ → client/ import.
    /// See docs/gdd/settlements.md §1.4 for enforcement model.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ClientNotifyRejection(string missingItemId) =>
        LocalState.NotifyRejection(missingItemId);

    // ── Settlement stockpile ──────────────────────────────────────────────────

    /// <summary>
    /// Adds items to the settlement stockpile and broadcasts the new state to all peers.
    /// Called by TreeSystem.FellTreeForNpc when an NPC chops a tree.
    /// Server-side only.
    /// </summary>
    public void AddToStockpile(string itemId, int count)
    {
        _stockpile.TryGetValue(itemId, out int existing);
        _stockpile[itemId] = existing + count;
        GD.Print($"[Settlement] stockpile +{count} {itemId} → {_stockpile[itemId]}");
        BroadcastStockpile();
    }

    /// <summary>
    /// Moves all stockpile contents into the requesting founder's inventory and clears the stockpile.
    /// Only the founder peer can take from their own stockpile.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestTakeFromStockpile()
    {
        if (!Multiplayer.IsServer()) return;

        long sender = Multiplayer.GetRemoteSenderId();
        if (sender == 0) sender = 1L;

        if (!IsFounder(sender))
        {
            GD.PrintErr($"[Settlement] peer {sender} tried to take from stockpile — not a founder");
            return;
        }

        foreach (var (itemId, count) in _stockpile)
        {
            InventorySystem.Instance.AddItem(sender, itemId, count);
            GD.Print($"[Settlement] peer {sender} took {count}× {itemId} from stockpile");
        }
        _stockpile.Clear();
        BroadcastStockpile();
    }

    private void BroadcastStockpile()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(_stockpile);
        Rpc(MethodName.ClientUpdateStockpile, json);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ClientUpdateStockpile(string stockpileJson) =>
        LocalState.SetStockpile(stockpileJson);

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SpawnBuilding(string buildingId, Vector3 position)
    {
        var data = BuildingRegistry.Find(buildingId);
        if (data == null) return;

        var scene = GD.Load<PackedScene>(data.ScenePath);
        if (scene == null)
        {
            GD.PrintErr($"[Settlement] scene not found: {data.ScenePath} — editor task pending");
            return;
        }
        var node  = scene.Instantiate<Node3D>();
        // Unique name avoids conflicts if multiple of the same type are placed.
        node.Name = $"{buildingId}_{(int)position.X}_{(int)position.Z}";
        AddChild(node);
        // Raise the node so its base sits on the terrain surface.
        node.GlobalPosition = position + Vector3.Up * (data.Height * 0.5f);

        GD.Print($"[Settlement] spawned {buildingId} at {position}");
    }
}
