using MankersKingdoms.Shared;
using Xunit;

namespace MankersKingdoms.Tests.Shared;

public class MonsterRegistryTests
{
    [Fact]
    public void All_ContainsFourMonsters()
    {
        Assert.Equal(4, MonsterRegistry.All.Count);
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
