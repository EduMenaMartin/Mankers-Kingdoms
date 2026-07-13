using System.Collections.Generic;

namespace MankersKingdoms.Shared;

/// <summary>
/// Pure-C# inventory bag: maps item IDs to counts, plus 9 hotbar slot references.
/// No Godot dependency — testable in xUnit.
/// </summary>
public sealed class PlayerInventory
{
    private readonly SortedDictionary<string, int> _items = new();

    // ── Hotbar (9 slots, indices 0–8) ─────────────────────────────────────────

    private readonly string?[] _hotbarSlots = new string?[9];

    /// <summary>Read-only view of the 9 hotbar slot itemIds (null = empty slot).</summary>
    public IReadOnlyList<string?> HotbarSlots => _hotbarSlots;

    /// <summary>
    /// Assigns <paramref name="itemId"/> to the given hotbar slot.
    /// If the same itemId is already in another slot, that slot is first cleared
    /// (one item in one slot at a time).
    /// Pass null to clear the slot.
    /// </summary>
    public void SetHotbarSlot(int slot, string? itemId)
    {
        if (slot < 0 || slot >= 9) return;
        if (itemId != null)
        {
            for (int i = 0; i < 9; i++)
                if (_hotbarSlots[i] == itemId) _hotbarSlots[i] = null;
        }
        _hotbarSlots[slot] = itemId;
    }

    /// <summary>Returns the itemId assigned to <paramref name="slot"/>, or null if empty.</summary>
    public string? GetHotbarSlot(int slot) =>
        (slot >= 0 && slot < 9) ? _hotbarSlots[slot] : null;

    public IReadOnlyDictionary<string, int> Items => _items;

    public void Add(string itemId, int count)
    {
        if (count <= 0) return;
        _items.TryGetValue(itemId, out int existing);
        _items[itemId] = existing + count;
    }

    /// <summary>
    /// Removes <paramref name="count"/> of <paramref name="itemId"/>.
    /// Returns false and makes no change if the inventory holds fewer than requested.
    /// </summary>
    public bool Remove(string itemId, int count)
    {
        if (count <= 0) return true;
        if (!_items.TryGetValue(itemId, out int existing) || existing < count)
            return false;

        int remaining = existing - count;
        if (remaining == 0)
            _items.Remove(itemId);
        else
            _items[itemId] = remaining;

        return true;
    }

    /// <summary>
    /// Removes all of <paramref name="itemId"/> regardless of how many are held.
    /// No-ops silently if the item is not present.
    /// Use this for kit-clearing, not for crafting (which should fail if stock is too low).
    /// </summary>
    public void ForceRemove(string itemId) => _items.Remove(itemId);

    public int Count(string itemId) =>
        _items.TryGetValue(itemId, out int c) ? c : 0;

    public bool Has(string itemId, int count = 1) =>
        Count(itemId) >= count;

    /// <summary>
    /// Clears any hotbar slot that references <paramref name="itemId"/>.
    /// Called by InventorySystem after an item stack reaches zero, so no hotbar
    /// slot can outlive the item it points to.
    /// </summary>
    public void ClearHotbarSlotsFor(string itemId)
    {
        for (int i = 0; i < 9; i++)
            if (_hotbarSlots[i] == itemId) _hotbarSlots[i] = null;
    }

    /// <summary>Removes all items and clears all hotbar slot references.</summary>
    public void Clear()
    {
        _items.Clear();
        for (int i = 0; i < 9; i++) _hotbarSlots[i] = null;
    }
}
