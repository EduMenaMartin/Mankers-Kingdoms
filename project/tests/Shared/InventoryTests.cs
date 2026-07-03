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
}
