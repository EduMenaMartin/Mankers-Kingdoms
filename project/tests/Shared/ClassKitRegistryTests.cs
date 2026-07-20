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

    // ── Skill bumps (M5: stats are player-rolled; class gives skill bumps) ───

    [Fact]
    public void Fighter_HasMeleeSkillBump()
    {
        var kit = ClassKitRegistry.Find("class.fighter")!;
        Assert.Contains(kit.SkillBumps, b => b.SkillId == "skill.melee" && b.Amount == 5);
    }

    [Fact]
    public void Fighter_HasAthleticsSkillBump()
    {
        var kit = ClassKitRegistry.Find("class.fighter")!;
        Assert.Contains(kit.SkillBumps, b => b.SkillId == "skill.athletics" && b.Amount == 3);
    }

    [Fact]
    public void Ranger_HasRangedSkillBump()
    {
        var kit = ClassKitRegistry.Find("class.ranger")!;
        Assert.Contains(kit.SkillBumps, b => b.SkillId == "skill.ranged" && b.Amount == 5);
    }

    [Fact]
    public void Ranger_HasForagingSkillBump()
    {
        var kit = ClassKitRegistry.Find("class.ranger")!;
        Assert.Contains(kit.SkillBumps, b => b.SkillId == "skill.foraging" && b.Amount == 3);
    }

    [Fact]
    public void AllKits_HaveAtLeastOneSkillBump()
    {
        foreach (var kit in ClassKitRegistry.All)
            Assert.True(kit.SkillBumps.Length > 0, $"{kit.ClassId} has no skill bumps");
    }

    [Fact]
    public void AllKits_SkillBumpAmounts_ArePositive()
    {
        foreach (var kit in ClassKitRegistry.All)
            foreach (var bump in kit.SkillBumps)
                Assert.True(bump.Amount > 0, $"{kit.ClassId}: skill bump {bump.SkillId} has non-positive amount");
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

    // ── Class traits: block bonus + ranged crit threshold (combat.md §17) ────

    [Fact]
    public void Fighter_ActiveBlockBonus_Is6()
    {
        // §17.1: Fighter block bonus +6 > standard +4.
        var kit = ClassKitRegistry.Find("class.fighter")!;
        Assert.Equal(6, kit.ActiveBlockBonus);
    }

    [Fact]
    public void Fighter_RangedCritThreshold_IsStandard24()
    {
        // Fighter has no ranged crit trait; threshold is standard 24.
        var kit = ClassKitRegistry.Find("class.fighter")!;
        Assert.Equal(24, kit.RangedCritThreshold);
    }

    [Fact]
    public void Ranger_ActiveBlockBonus_IsStandard4()
    {
        // Ranger has no block trait; standard value applies.
        var kit = ClassKitRegistry.Find("class.ranger")!;
        Assert.Equal(4, kit.ActiveBlockBonus);
    }

    [Fact]
    public void Ranger_RangedCritThreshold_Is22()
    {
        // §17.2: Ranger crit threshold 22 < standard 24 — harder to block crits.
        var kit = ClassKitRegistry.Find("class.ranger")!;
        Assert.Equal(22, kit.RangedCritThreshold);
    }

    [Fact]
    public void AllKits_ActiveBlockBonus_AtLeast4()
    {
        // Standard minimum is 4; no kit should be weaker than baseline.
        foreach (var kit in ClassKitRegistry.All)
            Assert.True(kit.ActiveBlockBonus >= 4,
                $"{kit.ClassId}: ActiveBlockBonus {kit.ActiveBlockBonus} is below the standard 4");
    }

    [Fact]
    public void AllKits_RangedCritThreshold_AtMost24()
    {
        // Standard max is 24; no kit should require a higher roll than baseline.
        foreach (var kit in ClassKitRegistry.All)
            Assert.True(kit.RangedCritThreshold <= 24,
                $"{kit.ClassId}: RangedCritThreshold {kit.RangedCritThreshold} exceeds the standard 24");
    }
}
