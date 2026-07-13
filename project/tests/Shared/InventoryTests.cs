using MankersKingdoms.Shared;
using Xunit;

namespace MankersKingdoms.Tests.Shared;

public class InventoryTests
{
    [Fact]
    public void Add_IncreasesCount()
    {
        var inv = new PlayerInventory();
        inv.Add("resource.wood", 3);
        Assert.Equal(3, inv.Count("resource.wood"));
    }

    [Fact]
    public void Add_Stacks()
    {
        var inv = new PlayerInventory();
        inv.Add("resource.wood", 2);
        inv.Add("resource.wood", 5);
        Assert.Equal(7, inv.Count("resource.wood"));
    }

    [Fact]
    public void Remove_Sufficient_ReturnsTrue()
    {
        var inv = new PlayerInventory();
        inv.Add("resource.wood", 5);
        Assert.True(inv.Remove("resource.wood", 3));
        Assert.Equal(2, inv.Count("resource.wood"));
    }

    [Fact]
    public void Remove_Insufficient_ReturnsFalseAndMakesNoChange()
    {
        var inv = new PlayerInventory();
        inv.Add("resource.wood", 2);
        Assert.False(inv.Remove("resource.wood", 5));
        Assert.Equal(2, inv.Count("resource.wood"));
    }

    [Fact]
    public void Remove_Exact_LeavesZeroAndCleansUp()
    {
        var inv = new PlayerInventory();
        inv.Add("resource.wood", 3);
        inv.Remove("resource.wood", 3);
        Assert.Equal(0, inv.Count("resource.wood"));
        Assert.Empty(inv.Items);
    }

    [Fact]
    public void Has_ReturnsTrueWhenSufficient()
    {
        var inv = new PlayerInventory();
        inv.Add("resource.wood", 4);
        Assert.True(inv.Has("resource.wood", 4));
        Assert.False(inv.Has("resource.wood", 5));
    }

    [Fact]
    public void MultipleItemTypes_StoredIndependently()
    {
        var inv = new PlayerInventory();
        inv.Add("resource.wood", 3);
        inv.Add("item.berry",    2);
        Assert.Equal(3, inv.Count("resource.wood"));
        Assert.Equal(2, inv.Count("item.berry"));
    }

    [Fact]
    public void Clear_RemovesAllItems()
    {
        var inv = new PlayerInventory();
        inv.Add("resource.wood", 5);
        inv.Add("item.berry",    2);
        inv.Clear();
        Assert.Empty(inv.Items);
    }

    // Regression: RequestSetClass used RemoveItems(999) to clear the prior kit, but
    // Remove requires having at least count items — so it silently failed, leaving the
    // old kit in place. The kit was then added again, doubling it.
    [Fact]
    public void ForceRemove_ClearsItemRegardlessOfCount()
    {
        var inv = new PlayerInventory();
        inv.Add("item.weapon.shortbow", 1);
        inv.ForceRemove("item.weapon.shortbow");
        Assert.Equal(0, inv.Count("item.weapon.shortbow"));
        Assert.Empty(inv.Items);
    }

    [Fact]
    public void ForceRemove_NoOpsWhenItemAbsent()
    {
        var inv = new PlayerInventory();
        inv.Add("resource.wood", 3);
        inv.ForceRemove("item.weapon.shortbow"); // not in inventory — should not throw
        Assert.Equal(3, inv.Count("resource.wood"));
    }

    // ── Hotbar regression tests ───────────────────────────────────────────────
    // Bug: hotbar slot was not cleared when the last stack of an item was consumed,
    // leaving the slot label showing the item name after it was gone from inventory.

    [Fact]
    public void ClearHotbarSlotsFor_NullsMatchingSlots()
    {
        var inv = new PlayerInventory();
        inv.Add("item.bandage", 1);
        inv.SetHotbarSlot(2, "item.bandage");
        inv.ClearHotbarSlotsFor("item.bandage");
        Assert.Null(inv.GetHotbarSlot(2));
    }

    [Fact]
    public void ClearHotbarSlotsFor_LeavesOtherSlotsUntouched()
    {
        var inv = new PlayerInventory();
        inv.Add("item.bandage",  1);
        inv.Add("resource.wood", 3);
        inv.SetHotbarSlot(0, "item.bandage");
        inv.SetHotbarSlot(1, "resource.wood");
        inv.ClearHotbarSlotsFor("item.bandage");
        Assert.Null(inv.GetHotbarSlot(0));
        Assert.Equal("resource.wood", inv.GetHotbarSlot(1));
    }

    [Fact]
    public void Clear_AlsoClearsAllHotbarSlots()
    {
        var inv = new PlayerInventory();
        inv.Add("item.bandage",  2);
        inv.Add("resource.wood", 5);
        inv.SetHotbarSlot(0, "item.bandage");
        inv.SetHotbarSlot(3, "resource.wood");
        inv.Clear();
        Assert.Empty(inv.Items);
        Assert.Null(inv.GetHotbarSlot(0));
        Assert.Null(inv.GetHotbarSlot(3));
    }
}
