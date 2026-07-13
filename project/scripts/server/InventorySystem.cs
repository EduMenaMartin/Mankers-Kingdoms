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
    /// If the last stack of <paramref name="itemId"/> is consumed, evicts it from all
    /// hotbar slots and syncs the hotbar so the client HUD clears immediately.
    /// </summary>
    public bool RemoveItems(long peerId, string itemId, int count)
    {
        if (!Multiplayer.IsServer()) return false;
        if (!_inventories.TryGetValue(peerId, out var inv)) return false;
        if (!inv.Remove(itemId, count)) return false;

        if (!inv.Has(itemId))
        {
            inv.ClearHotbarSlotsFor(itemId);
            SyncHotbarTo(peerId, inv);
        }

        SyncTo(peerId, inv);
        return true;
    }

    /// <summary>
    /// Removes all of <paramref name="itemId"/> for a peer regardless of quantity.
    /// Used by kit-clearing logic in RequestSetClass; not for crafting.
    /// Also evicts the item from any hotbar slot.
    /// </summary>
    public void ClearItem(long peerId, string itemId)
    {
        if (!Multiplayer.IsServer()) return;
        if (!_inventories.TryGetValue(peerId, out var inv)) return;
        inv.ForceRemove(itemId);
        inv.ClearHotbarSlotsFor(itemId);
        SyncHotbarTo(peerId, inv);
        SyncTo(peerId, inv);
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

    /// <summary>
    /// Drops all items from a peer's inventory (used on death).
    /// Clear() also resets hotbar slot references; hotbar is synced so the
    /// client HUD shows empty slots immediately after the death drop.
    /// </summary>
    public PlayerInventory TakeAll(long peerId)
    {
        if (!_inventories.TryGetValue(peerId, out var inv))
            return new PlayerInventory();

        var copy = new PlayerInventory();
        foreach (var (id, cnt) in inv.Items)
            copy.Add(id, cnt);

        inv.Clear(); // also clears hotbar slot refs (PlayerInventory.Clear)
        SyncHotbarTo(peerId, inv);
        SyncTo(peerId, inv);
        return copy;
    }

    // ── Save / Load (M8) ─────────────────────────────────────────────────────

    /// <summary>
    /// Clears a peer's inventory, restores saved items and hotbar slots, then syncs to client.
    /// Called by SaveSystem.TryLoad().
    /// </summary>
    public void RestoreInventoryFromSave(
        long peerId,
        System.Collections.Generic.Dictionary<string, int> items,
        string?[] hotbarSlots)
    {
        if (!Multiplayer.IsServer()) return;

        if (!_inventories.TryGetValue(peerId, out var inv))
            _inventories[peerId] = inv = new PlayerInventory();

        inv.Clear();
        foreach (var (id, cnt) in items)
            inv.Add(id, cnt);

        for (int i = 0; i < 9 && i < hotbarSlots.Length; i++)
            inv.SetHotbarSlot(i, hotbarSlots[i]);

        SyncTo(peerId, inv);
        SyncHotbarTo(peerId, inv);
        GD.Print($"[Inventory] peer {peerId} inventory restored from save ({inv.Items.Count} item type(s))");
    }

    /// <summary>Pushes current inventory and hotbar to a peer. Used for reconnect replay.</summary>
    public void SyncInventoryAndHotbarTo(long peerId)
    {
        if (!_inventories.TryGetValue(peerId, out var inv)) return;
        SyncTo(peerId, inv);
        SyncHotbarTo(peerId, inv);
    }

    // ── Hotbar RPCs ───────────────────────────────────────────────────────────

    /// <summary>
    /// Client requests that <paramref name="itemId"/> be assigned to hotbar
    /// <paramref name="slot"/> (0–8). Pass an empty string to clear the slot.
    /// Server validates the player owns the item before accepting.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestAssignHotbar(int slot, string itemId)
    {
        if (!Multiplayer.IsServer()) return;
        if (slot < 0 || slot >= 9) return;

        long sender = Multiplayer.GetRemoteSenderId();
        if (sender == 0) sender = 1L;

        if (!_inventories.TryGetValue(sender, out var inv)) return;

        string? resolved = string.IsNullOrEmpty(itemId) ? null : itemId;

        // If assigning (not clearing), item must be in inventory.
        if (resolved != null && !inv.Has(resolved))
        {
            GD.Print($"[Inventory] peer {sender} tried to assign {resolved} to hotbar slot {slot} — not in inventory");
            return;
        }

        inv.SetHotbarSlot(slot, resolved);
        GD.Print($"[Inventory] peer {sender} hotbar slot {slot} = {resolved ?? "(empty)"}");
        SyncHotbarTo(sender, inv);
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

    private void SyncHotbarTo(long peerId, PlayerInventory inv)
    {
        for (int i = 0; i < 9; i++)
        {
            string slotContent = inv.GetHotbarSlot(i) ?? "";
            if (peerId == 1)
                ApplyHotbarSlot(i, slotContent);
            else
                RpcId(peerId, MethodName.ApplyHotbarSlot, i, slotContent);
        }
    }

    // ── Client-facing RPCs ────────────────────────────────────────────────────

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ApplyInventoryState(Godot.Collections.Dictionary<string, int> items)
    {
        var inv = new PlayerInventory();
        foreach (var (id, count) in items)
            inv.Add(id, count);

        LocalState.SetInventory(inv);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ApplyHotbarSlot(int slot, string itemId)
    {
        LocalState.SetHotbarSlot(slot, string.IsNullOrEmpty(itemId) ? null : itemId);
    }
}
