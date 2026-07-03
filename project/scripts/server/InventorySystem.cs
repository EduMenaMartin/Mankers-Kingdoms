using System.Collections.Generic;
using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Server;

/// <summary>
/// Server-authoritative inventory manager.
/// One PlayerInventory per connected peer, keyed by peer ID.
/// On every change the affected peer receives a full snapshot via RPC — simple and
/// correct for the prototype; delta updates are a future optimization.
///
/// Node must appear BEFORE TreeSystem (and any future system that calls AddItem)
/// in GameWorld.tscn so Instance is set when those systems call _Ready.
/// </summary>
public partial class InventorySystem : Node
{
    public static InventorySystem Instance { get; private set; } = null!;

    // Server-only: authoritative inventory per peer. SortedDictionary: ADR-0011.
    private readonly SortedDictionary<long, PlayerInventory> _inventories = new();

    public override void _Ready()
    {
        Instance = this;
    }

    // ── Server API (called by other server-side systems) ─────────────────────

    public void AddItem(long peerId, string itemId, int count)
    {
        if (!Multiplayer.IsServer()) return;

        if (!_inventories.TryGetValue(peerId, out var inv))
            _inventories[peerId] = inv = new PlayerInventory();

        inv.Add(itemId, count);
        GD.Print($"[Inventory] peer {peerId} +{count}× {itemId} → {inv.Count(itemId)} total");
        SyncTo(peerId, inv);
    }

    /// <summary>
    /// Attempts to remove items. Returns false (and makes no change) if insufficient stock.
    /// </summary>
    public bool RemoveItems(long peerId, string itemId, int count)
    {
        if (!Multiplayer.IsServer()) return false;
        if (!_inventories.TryGetValue(peerId, out var inv)) return false;
        if (!inv.Remove(itemId, count)) return false;
        SyncTo(peerId, inv);
        return true;
    }

    public bool HasItems(long peerId, string itemId, int count = 1)
    {
        if (!_inventories.TryGetValue(peerId, out var inv)) return false;
        return inv.Has(itemId, count);
    }

    public PlayerInventory GetInventory(long peerId)
    {
        _inventories.TryGetValue(peerId, out var inv);
        return inv ?? new PlayerInventory();
    }

    /// <summary>Drops all items from a peer's inventory (used on death).</summary>
    public PlayerInventory TakeAll(long peerId)
    {
        if (!_inventories.TryGetValue(peerId, out var inv))
            return new PlayerInventory();

        var copy = new PlayerInventory();
        foreach (var (id, cnt) in inv.Items)
            copy.Add(id, cnt);

        inv.Clear();
        SyncTo(peerId, inv);
        return copy;
    }

    // ── Sync helpers ──────────────────────────────────────────────────────────

    private void SyncTo(long peerId, PlayerInventory inv)
    {
        var dict = new Godot.Collections.Dictionary<string, int>();
        foreach (var (id, count) in inv.Items)
            dict[id] = count;

        // Peer 1 is the server/host itself — RpcId to yourself is rejected, call directly.
        if (peerId == 1)
            ApplyInventoryState(dict);
        else
            RpcId(peerId, MethodName.ApplyInventoryState, dict);
    }

    // ── Client-facing RPC ─────────────────────────────────────────────────────

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ApplyInventoryState(Godot.Collections.Dictionary<string, int> items)
    {
        var inv = new PlayerInventory();
        foreach (var (id, count) in items)
            inv.Add(id, count);

        LocalState.SetInventory(inv);
    }
}
