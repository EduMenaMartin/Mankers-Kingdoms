using MankersKingdoms.Shared;
using Xunit;

namespace MankersKingdoms.Tests.Shared;

public class ClassKitRegistryTests
{
    // ── Catalog ───────────────────────────────────────────────────────────────

    [Fact]
    public void All_ContainsTwoKits()
    {
        Assert.Equal(2, ClassKitRegistry.All.Count);
    }

    [Fact]
    public void AllKits_HaveStableClassDotId()
    {
        foreach (var kit in ClassKitRegistry.All)
            Assert.StartsWith("class.", kit.ClassId);
    }

    [Fact]
    public void AllKits_HaveAtLeastOneStartingItem()
    {
        foreach (var kit in ClassKitRegistry.All)
            Assert.True(kit.StartingItems.Length > 0,
                $"{kit.ClassId} has no starting items");
    }

    [Fact]
    public void AllKits_StartingItemCounts_ArePositive()
    {
        foreach (var kit in ClassKitRegistry.All)
            foreach (var item in kit.StartingItems)
                Assert.True(item.Count > 0,
                    $"{kit.ClassId}: item {item.ItemId} has non-positive count");
    }

    // ── Fighter ───────────────────────────────────────────────────────────────

    [Fact]
    public void Fighter_FoundById()
    {
        var kit = ClassKitRegistry.Find("class.fighter");
        Assert.NotNull(kit);
        Assert.Equal("class.fighter", kit.ClassId);
    }

    [Fact]
    public void Fighter_HasLongsword()
    {
        var kit = ClassKitRegistry.Find("class.fighter")!;
        bool found = false;
        foreach (var item in kit.StartingItems)
            if (item.ItemId == "item.weapon.longsword") { found = true; break; }
        Assert.True(found, "Fighter kit must include item.weapon.longsword");
    }

    [Fact]
    public void Fighter_HasShield()
    {
        var kit = ClassKitRegistry.Find("class.fighter")!;
        bool found = false;
        foreach (var item in kit.StartingItems)
            if (item.ItemId == "item.armor.shield") { found = true; break; }
        Assert.True(found, "Fighter kit must include item.armor.shield");
    }

    [Fact]
    public void Fighter_HasNoRangedWeapon()
    {
        var kit = ClassKitRegistry.Find("class.fighter")!;
        foreach (var item in kit.StartingItems)
            Assert.NotEqual("item.weapon.shortbow", item.ItemId);
    }

    // ── Ranger ────────────────────────────────────────────────────────────────

    [Fact]
    public void Ranger_FoundById()
    {
        var kit = ClassKitRegistry.Find("class.ranger");
        Assert.NotNull(kit);
        Assert.Equal("class.ranger", kit.ClassId);
    }

    [Fact]
    public void Ranger_HasShortbow()
    {
        var kit = ClassKitRegistry.Find("class.ranger")!;
        bool found = false;
        foreach (var item in kit.StartingItems)
            if (item.ItemId == "item.weapon.shortbow") { found = true; break; }
        Assert.True(found, "Ranger kit must include item.weapon.shortbow");
    }

    [Fact]
    public void Ranger_HasArrows()
    {
        var kit = ClassKitRegistry.Find("class.ranger")!;
        bool found = false;
        foreach (var item in kit.StartingItems)
            if (item.ItemId == "item.arrow" && item.Count > 0) { found = true; break; }
        Assert.True(found, "Ranger kit must include item.arrow with a positive count");
    }

    [Fact]
    public void Ranger_HasNoMeleeWeapon()
    {
        var kit = ClassKitRegistry.Find("class.ranger")!;
        foreach (var item in kit.StartingItems)
            Assert.NotEqual("item.weapon.longsword", item.ItemId);
    }

    // ── Stats ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Fighter_Stats_MatchGddExample()
    {
        // combat.md §2.4: Fighter Str 16 → mod +1.
        var kit = ClassKitRegistry.Find("class.fighter")!;
        Assert.Equal(16, kit.Str);
    }

    [Fact]
    public void Ranger_Stats_MatchGddExample()
    {
        // combat.md §4.2: Ranger Dex 15 → mod +1.
        var kit = ClassKitRegistry.Find("class.ranger")!;
        Assert.Equal(15, kit.Dex);
    }

    [Fact]
    public void AllKits_StatsArePositive()
    {
        foreach (var kit in ClassKitRegistry.All)
        {
            Assert.True(kit.Str > 0, $"{kit.ClassId} has non-positive Str");
            Assert.True(kit.Dex > 0, $"{kit.ClassId} has non-positive Dex");
        }
    }

    // ── Lookup ────────────────────────────────────────────────────────────────

    [Fact]
    public void Unknown_ReturnsNull()
    {
        Assert.Null(ClassKitRegistry.Find("class.does_not_exist"));
    }

    [Fact]
    public void DefaultClassId_IsRecognised()
    {
        // GameSession.ChosenClassId defaults to "class.fighter" — must be a valid kit.
        Assert.NotNull(ClassKitRegistry.Find("class.fighter"));
    }
}
