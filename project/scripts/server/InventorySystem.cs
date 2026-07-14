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
    /// hotbar and equipment slots and syncs both so the client HUD clears immediately.
    /// </summary>
    public bool RemoveItems(long peerId, string itemId, int count)
    {
        if (!Multiplayer.IsServer()) return false;
        if (!_inventories.TryGetValue(peerId, out var inv)) return false;
        if (!inv.Remove(itemId, count)) return false;

        if (!inv.Has(itemId))
        {
            inv.ClearHotbarSlotsFor(itemId);
            inv.ClearEquippedSlotsFor(itemId);
            SyncHotbarTo(peerId, inv);
            SyncEquippedSlotsTo(peerId, inv);
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
        inv.ClearEquippedSlotsFor(itemId);
        SyncHotbarTo(peerId, inv);
        SyncEquippedSlotsTo(peerId, inv);
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

        inv.Clear(); // clears items, hotbar, and equipment slots (PlayerInventory.Clear)
        SyncHotbarTo(peerId, inv);
        SyncEquippedSlotsTo(peerId, inv);
        SyncTo(peerId, inv);
        return copy;
    }

    // ── Save / Load (M8) ─────────────────────────────────────────────────────

    /// <summary>
    /// Clears a peer's inventory, restores saved items, hotbar slots, and equipped slots,
    /// then syncs all to client. Called by SaveSystem.TryLoad().
    /// Equipment slot parameters default to null (slot empty) for saves predating §10.
    /// </summary>
    public void RestoreInventoryFromSave(
        long peerId,
        System.Collections.Generic.Dictionary<string, int> items,
        string?[] hotbarSlots,
        string? equippedMainHand  = null,
        string? equippedOffHand   = null,
        string? equippedBodyArmor = null)
    {
        if (!Multiplayer.IsServer()) return;

        if (!_inventories.TryGetValue(peerId, out var inv))
            _inventories[peerId] = inv = new PlayerInventory();

        inv.Clear();
        foreach (var (id, cnt) in items)
            inv.Add(id, cnt);

        for (int i = 0; i < 9 && i < hotbarSlots.Length; i++)
            inv.SetHotbarSlot(i, hotbarSlots[i]);

        inv.SetEquipped(EquipSlot.MainHand,  equippedMainHand);
        inv.SetEquipped(EquipSlot.OffHand,   equippedOffHand);
        inv.SetEquipped(EquipSlot.BodyArmor, equippedBodyArmor);

        SyncTo(peerId, inv);
        SyncHotbarTo(peerId, inv);
        SyncEquippedSlotsTo(peerId, inv);
        GD.Print($"[Inventory] peer {peerId} inventory restored from save ({inv.Items.Count} item type(s))");
    }

    /// <summary>Pushes current inventory, hotbar, and equipped slots to a peer. Used for reconnect replay.</summary>
    public void SyncInventoryAndHotbarTo(long peerId)
    {
        if (!_inventories.TryGetValue(peerId, out var inv)) return;
        SyncTo(peerId, inv);
        SyncHotbarTo(peerId, inv);
        SyncEquippedSlotsTo(peerId, inv);
    }

    // ── Equipment slot RPC + server API ──────────────────────────────────────

    /// <summary>
    /// Server-side equip without RPC validation overhead.
    /// Called by HealthSystem when distributing a class kit (auto-equip starting gear).
    /// </summary>
    public void EquipItem(long peerId, EquipSlot slot, string itemId)
    {
        if (!Multiplayer.IsServer()) return;
        if (!_inventories.TryGetValue(peerId, out var inv)) return;
        if (!inv.Has(itemId)) return;

        inv.SetEquipped(slot, itemId);
        SyncEquippedSlotsTo(peerId, inv);
        GD.Print($"[Inventory] peer {peerId} auto-equipped {itemId} → {slot}");
    }

    /// <summary>
    /// Client requests equipping or unequipping an item.
    /// Pass an empty string for itemId to unequip the slot.
    /// Server validates item ownership and slot compatibility before applying.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestEquipItem(int slotInt, string itemId)
    {
        if (!Multiplayer.IsServer()) return;

        long sender = Multiplayer.GetRemoteSenderId();
        if (sender == 0) sender = 1L;

        if (!_inventories.TryGetValue(sender, out var inv)) return;

        var     slot     = (EquipSlot)slotInt;
        string? resolved = string.IsNullOrEmpty(itemId) ? null : itemId;

        if (resolved != null)
        {
            if (!inv.Has(resolved))
            {
                GD.Print($"[Inventory] peer {sender}: {resolved} not in inventory — equip rejected");
                return;
            }
            if (!IsCompatibleWithSlot(slot, resolved))
            {
                GD.Print($"[Inventory] peer {sender}: {resolved} incompatible with slot {slot}");
                return;
            }
        }

        // Two-handed weapon exclusivity (inventory.md §10.3): WeaponData has no TwoHanded field
        // in v1 — no starting weapon is two-handed — so this is a placeholder comment only.
        // Add `bool TwoHanded` to WeaponData and enforce here when relevant weapons are added.

        inv.SetEquipped(slot, resolved);
        SyncEquippedSlotsTo(sender, inv);
        GD.Print($"[Inventory] peer {sender} → slot {slot} = {resolved ?? "(empty)"}");
    }

    private static bool IsCompatibleWithSlot(EquipSlot slot, string itemId) => slot switch
    {
        EquipSlot.MainHand  => WeaponRegistry.Find(itemId) != null,
        EquipSlot.OffHand   => itemId == "item.armor.shield" || WeaponRegistry.Find(itemId) != null,
        EquipSlot.BodyArmor => ArmorRegistry.Find(itemId) != null && itemId != "item.armor.shield",
        _                   => false
    };

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

    // ── Sync helpers ──────────────────────────────────────────────────────────

    public void SyncEquippedSlotsTo(long peerId, PlayerInventory inv)
    {
        foreach (EquipSlot slot in System.Enum.GetValues<EquipSlot>())
        {
            string content = inv.GetEquipped(slot) ?? "";
            if (peerId == 1)
                ApplyEquippedSlot((int)slot, content);
            else
                RpcId(peerId, MethodName.ApplyEquippedSlot, (int)slot, content);
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

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ApplyEquippedSlot(int slotInt, string itemId)
    {
        LocalState.SetEquipped((EquipSlot)slotInt, string.IsNullOrEmpty(itemId) ? null : itemId);
    }
}
