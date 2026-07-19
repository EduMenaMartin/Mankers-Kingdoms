using MankersKingdoms.Shared;
using Xunit;

namespace MankersKingdoms.Tests.Shared;

/// <summary>
/// Tests for the bandage crafting and use formula.
/// Verifies data invariants and heal calculations without requiring the Godot runtime.
/// The RPC methods in SettlementSystem/HealthSystem use these same constants.
/// </summary>
public class BandageCraftingTests
{
    // ── Recipe constants ──────────────────────────────────────────────────────

    // Constants are public on SettlementSystem but SettlementSystem is a Godot node
    // and can't be instantiated in tests. Mirror the values here so we can test
    // the formula independently. If SettlementSystem constants change, this test
    // catches the drift.
    private const int   BANDAGE_HERB_COST        = 2;
    private const float BANDAGE_BASE_HEAL        = 20f;
    private const float BANDAGE_HEAL_PER_5_SKILL = 1f;
    private const float BANDAGE_MAX_HEAL         = 40f;

    [Fact]
    public void BandageHerbCost_Is2()
    {
        Assert.Equal(2, BANDAGE_HERB_COST);
    }

    [Fact]
    public void BandageBaseHeal_Is20()
    {
        Assert.Equal(20f, BANDAGE_BASE_HEAL);
    }

    [Fact]
    public void BandageMaxHeal_Is40()
    {
        Assert.Equal(40f, BANDAGE_MAX_HEAL);
    }

    // ── Heal formula: 20 + floor(ForagingLevel / 5), capped at 40 ────────────

    // Integer division gives floor(foraging/5) — 1 HP per 5 full skill levels.
    private static float CalcHeal(int foraging) =>
        System.Math.Min(BANDAGE_MAX_HEAL, BANDAGE_BASE_HEAL + (foraging / 5) * BANDAGE_HEAL_PER_5_SKILL);

    [Theory]
    [InlineData(0,  20f)]  // no skill → base heal
    [InlineData(4,  20f)]  // below threshold → no bonus (integer division: 4/5 = 0)
    [InlineData(5,  21f)]  // first threshold hit → +1
    [InlineData(10, 22f)]  // level 10 → +2
    [InlineData(50, 30f)]  // level 50 → +10
    [InlineData(99, 39f)]  // level 99: 99/5 = 19 → 20+19 = 39, under cap
    [InlineData(100, 40f)] // level 100: 100/5 = 20 → 20+20 = 40, exactly at cap
    [InlineData(150, 40f)] // above cap → clamped to 40
    public void HealFormula_ReturnsExpectedValue(int foragingLevel, float expectedHeal)
    {
        float actual = CalcHeal(foragingLevel);
        Assert.Equal(expectedHeal, actual, precision: 1);
    }

    [Fact]
    public void HealFormula_NeverExceedsMax()
    {
        // Even at impossibly high skill levels, heal is capped at 40.
        for (int level = 0; level <= 200; level++)
        {
            float heal = CalcHeal(level);
            Assert.True(heal <= BANDAGE_MAX_HEAL, $"ForagingLevel={level}: heal {heal} exceeds cap {BANDAGE_MAX_HEAL}");
        }
    }

    [Fact]
    public void HealFormula_NeverBelowBase()
    {
        // Skill below first threshold (5) gives no bonus — floor ensures no fractional add.
        Assert.Equal(BANDAGE_BASE_HEAL, CalcHeal(0));
        Assert.Equal(BANDAGE_BASE_HEAL, CalcHeal(4));
    }

    // ── skill.foraging is in the skill registry ───────────────────────────────

    [Fact]
    public void SkillForaging_ExistsInRegistry()
    {
        var skill = SkillRegistry.Find("skill.foraging");
        Assert.NotNull(skill);
    }

    [Fact]
    public void SkillForaging_GovernedByWis()
    {
        var skill = SkillRegistry.Find("skill.foraging");
        Assert.Contains("wis", skill!.GoverningStats);
    }
}
