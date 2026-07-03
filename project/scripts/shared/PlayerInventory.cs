using System.Collections.Generic;

namespace MankersKingdoms.Shared;

/// <summary>
/// Pure-C# inventory bag: maps item IDs to counts.
/// No Godot dependency — testable in xUnit.
/// </summary>
public sealed class PlayerInventory
{
    private readonly SortedDictionary<string, int> _items = new();

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

    public int Count(string itemId) =>
        _items.TryGetValue(itemId, out int c) ? c : 0;

    public bool Has(string itemId, int count = 1) =>
        Count(itemId) >= count;

    public void Clear() => _items.Clear();
}
