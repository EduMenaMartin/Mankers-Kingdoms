using MankersKingdoms.Shared;
using Xunit;

namespace MankersKingdoms.Tests.Shared;

public class MonsterRegistryTests
{
    [Fact]
    public void All_ContainsFiveMonsters()
    {
        // Wolf, Goblin, Bandit, BanditArcher, Orc
        Assert.Equal(5, MonsterRegistry.All.Count);
    }

    [Fact]
    public void Orc_FoundById()
    {
        var m = MonsterRegistry.Find("monster.orc");
        Assert.NotNull(m);
        Assert.Equal("monster.orc", m.Id);
    }

    [Fact]
    public void Orc_HasExpectedHpFromHdFormula()
    {
        // 18 HD d8, Con 16 → ConMod = floor((16-10)/4) = 1
        // MaxHp = 18 × ((8+1)/2) + 1×18 = 18×4.5 + 18 = 81 + 18 = 99
        var m = MonsterRegistry.Find("monster.orc")!;
        Assert.Equal(99f, m.MaxHp, precision: 0);
    }

    [Fact]
    public void Goblin_HpMatchesHdFormula()
    {
        // 7 HD d8, Con 10 → ConMod = 0 → MaxHp = 7 × 4.5 = 31.5
        var m = MonsterRegistry.Find("monster.goblin")!;
        Assert.Equal(31.5f, m.MaxHp, precision: 1);
    }

    [Fact]
    public void Bandit_HpMatchesHdFormula()
    {
        // 11 HD d8, Con 14 → ConMod = floor((14-10)/4) = 1 → MaxHp = 11×4.5 + 11×1 = 49.5+11 = 60.5
        var m = MonsterRegistry.Find("monster.bandit")!;
        Assert.Equal(60.5f, m.MaxHp, precision: 1);
    }

    [Fact]
    public void Orc_IsMeleeElite()
    {
        var m = MonsterRegistry.Find("monster.orc")!;
        Assert.False(m.IsRanged);
        Assert.Equal(5, m.AttackBonus);
        Assert.Equal(14, m.TargetNumber);
        Assert.Equal("1d10", m.DamageDice);
    }

    [Fact]
    public void Humanoids_HaveHitDiceData()
    {
        // All humanoids should have HitDiceCount > 0 now.
        string[] humanoidIds = ["monster.goblin", "monster.bandit", "monster.bandit_archer", "monster.orc"];
        foreach (var id in humanoidIds)
        {
            var m = MonsterRegistry.Find(id)!;
            Assert.True(m.HitDiceCount > 0, $"{id} should have HitDiceCount > 0");
            Assert.True(m.ConstitutionScore.HasValue, $"{id} should have a ConstitutionScore");
        }
    }

    [Fact]
    public void Wolf_HasNoHitDiceData()
    {
        // Beasts use flat MaxHp, not the HD formula.
        var m = MonsterRegistry.Find("monster.wolf")!;
        Assert.Equal(0, m.HitDiceCount);
        Assert.Null(m.ConstitutionScore);
    }

    [Fact]
    public void Wolf_FoundById()
    {
        var m = MonsterRegistry.Find("monster.wolf");
        Assert.NotNull(m);
        Assert.Equal("monster.wolf", m.Id);
    }

    [Fact]
    public void Wolf_IsMelee()
    {
        var m = MonsterRegistry.Find("monster.wolf")!;
        Assert.False(m.IsRanged);
        Assert.Null(m.RangedWeaponId);
    }

    [Fact]
    public void BanditArcher_IsRanged()
    {
        var m = MonsterRegistry.Find("monster.bandit_archer")!;
        Assert.True(m.IsRanged);
    }

    [Fact]
    public void BanditArcher_HasRangedWeaponId()
    {
        var m = MonsterRegistry.Find("monster.bandit_archer")!;
        Assert.Equal("item.weapon.shortbow", m.RangedWeaponId);
    }

    [Fact]
    public void AllMonsters_HavePositiveHpAndSpeed()
    {
        foreach (var m in MonsterRegistry.All)
        {
            Assert.True(m.MaxHp > 0f,     $"{m.Id} has zero or negative MaxHp");
            Assert.True(m.MoveSpeed > 0f,  $"{m.Id} has zero or negative MoveSpeed");
        }
    }

    [Fact]
    public void AllMonsters_HavePositiveAggroAndAttackRange()
    {
        foreach (var m in MonsterRegistry.All)
        {
            Assert.True(m.AggroRange  > 0f, $"{m.Id} has zero or negative AggroRange");
            Assert.True(m.AttackRange > 0f, $"{m.Id} has zero or negative AttackRange");
        }
    }

    [Fact]
    public void AllMonsters_HavePositiveAttackBonus()
    {
        foreach (var m in MonsterRegistry.All)
            Assert.True(m.AttackBonus > 0, $"{m.Id} has non-positive AttackBonus");
    }

    [Fact]
    public void AllMonsters_HaveTargetNumberAbove10()
    {
        // Minimum TN is 10 (no armour, no Dex bonus). All authored values should be ≥ 10.
        foreach (var m in MonsterRegistry.All)
            Assert.True(m.TargetNumber >= 10, $"{m.Id} has TargetNumber below 10");
    }

    [Fact]
    public void AllMeleeMonsters_HaveNonEmptyDamageDice()
    {
        foreach (var m in MonsterRegistry.All)
        {
            if (!m.IsRanged)
                Assert.False(string.IsNullOrEmpty(m.DamageDice),
                    $"{m.Id} is melee but has empty DamageDice");
        }
    }

    [Fact]
    public void Wolf_HasFlatAuthoredStats()
    {
        // Matches the exact example in combat.md §6.2.
        var m = MonsterRegistry.Find("monster.wolf")!;
        Assert.Equal(3,  m.AttackBonus);
        Assert.Equal(12, m.TargetNumber);
        Assert.Equal("1d6",     m.DamageDice);
        Assert.Equal("piercing", m.DamageType);
    }

    [Fact]
    public void AllMonsters_HaveNonEmptyLootTable()
    {
        foreach (var m in MonsterRegistry.All)
            Assert.NotEmpty(m.LootTable);
    }

    [Fact]
    public void Unknown_ReturnsNull()
    {
        Assert.Null(MonsterRegistry.Find("monster.does_not_exist"));
    }
}
