using MankersKingdoms.Shared;
using Xunit;

namespace MankersKingdoms.Tests.Shared;

public class FoodRegistryTests
{
    [Fact]
    public void Berry_FindByRaw_ReturnsDefinition()
    {
        var food = FoodRegistry.FindByRaw("item.berry");
        Assert.NotNull(food);
        Assert.Equal("item.berry", food.RawItemId);
    }

    [Fact]
    public void Berry_FindByCooked_ReturnsDefinition()
    {
        var food = FoodRegistry.FindByCooked("item.cooked_berry");
        Assert.NotNull(food);
        Assert.Equal("item.cooked_berry", food.CookedItemId);
    }

    [Fact]
    public void Berry_BaseHunger_IsTen()
    {
        var food = FoodRegistry.FindByRaw("item.berry")!;
        Assert.Equal(10f, food.BaseHunger);
    }

    [Fact]
    public void Berry_CookedHunger_IsBaseTimesMultiplier()
    {
        var food = FoodRegistry.FindByRaw("item.berry")!;
        Assert.Equal(food.BaseHunger * food.CookMultiplier, food.CookedHunger);
    }

    [Fact]
    public void Berry_CookedHunger_IsForty()
    {
        var food = FoodRegistry.FindByRaw("item.berry")!;
        Assert.Equal(40f, food.CookedHunger);
    }

    [Fact]
    public void Berry_IsNotToxicRaw()
    {
        var food = FoodRegistry.FindByRaw("item.berry")!;
        Assert.False(food.IsToxicRaw);
    }

    [Fact]
    public void UnknownItem_FindByRaw_ReturnsNull()
    {
        Assert.Null(FoodRegistry.FindByRaw("item.does_not_exist"));
    }

    [Fact]
    public void UnknownItem_FindByCooked_ReturnsNull()
    {
        Assert.Null(FoodRegistry.FindByCooked("item.does_not_exist"));
    }

    [Fact]
    public void ToxicFood_CookedHunger_IsZeroWhenNoCookedId()
    {
        // Verify that a food with no cooked form reports CookedHunger = 0.
        var toxicFood = new FoodData(
            RawItemId:      "item.test_toxic",
            CookedItemId:   null,
            BaseHunger:     5f,
            CookMultiplier: 1f,
            IsToxicRaw:     true,
            PoisonDuration: 30f
        );
        Assert.Equal(0f, toxicFood.CookedHunger);
        Assert.True(toxicFood.IsToxicRaw);
        Assert.Equal(30f, toxicFood.PoisonDuration);
    }
}
