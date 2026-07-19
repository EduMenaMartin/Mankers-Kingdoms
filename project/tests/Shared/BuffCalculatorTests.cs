using System.Collections.Generic;
using MankersKingdoms.Shared;
using Xunit;

namespace MankersKingdoms.Tests.Shared;

/// <summary>
/// Tests for BuffCalculator's condensation rules (the core correctness requirement).
///
/// Key invariants:
///   Additive:       effective = base + Σ(amounts)        — applied once, not chained
///   Multiplicative: effective = base × (1 + Σ(amounts))  — sum deltas THEN multiply
///
/// The multiplicative test is the critical one:
///   Two "+50%" buffs (amount=0.5 each) must give ×2.0, not ×1.5×1.5=×2.25.
/// </summary>
public class BuffCalculatorTests
{
    private static ActiveBuff Additive(BuffStat stat, float amount, double expiresAt) =>
        new(stat, amount, BuffAmountType.Additive, expiresAt);

    private static ActiveBuff Multiplicative(BuffStat stat, float amount, double expiresAt) =>
        new(stat, amount, BuffAmountType.Multiplicative, expiresAt);

    // ── Additive condensation ─────────────────────────────────────────────────

    [Fact]
    public void GetAdditiveModifier_NoneActive_ReturnsZero()
    {
        var buffs = new List<ActiveBuff>();
        Assert.Equal(0f, BuffCalculator.GetAdditiveModifier(buffs, BuffStat.AttackBonus, currentTime: 1.0));
    }

    [Fact]
    public void GetAdditiveModifier_SingleBuff_ReturnsAmount()
    {
        var buffs = new List<ActiveBuff>
        {
            Additive(BuffStat.ArmorValue, -2f, expiresAt: 10.0),
        };
        Assert.Equal(-2f, BuffCalculator.GetAdditiveModifier(buffs, BuffStat.ArmorValue, currentTime: 0.0));
    }

    [Fact]
    public void GetAdditiveModifier_TwoBuffsSameStat_SumsCorrectly()
    {
        // Two armor debuffs: -2 + -3 = -5 (applied once, not chained).
        var buffs = new List<ActiveBuff>
        {
            Additive(BuffStat.ArmorValue, -2f, expiresAt: 10.0),
            Additive(BuffStat.ArmorValue, -3f, expiresAt: 10.0),
        };
        Assert.Equal(-5f, BuffCalculator.GetAdditiveModifier(buffs, BuffStat.ArmorValue, currentTime: 0.0));
    }

    [Fact]
    public void GetAdditiveModifier_ExpiredBuff_ExcludedFromSum()
    {
        var buffs = new List<ActiveBuff>
        {
            Additive(BuffStat.ArmorValue, -2f, expiresAt: 5.0),   // expires before query
            Additive(BuffStat.ArmorValue, -1f, expiresAt: 15.0),  // still active
        };
        Assert.Equal(-1f, BuffCalculator.GetAdditiveModifier(buffs, BuffStat.ArmorValue, currentTime: 10.0));
    }

    [Fact]
    public void GetAdditiveModifier_DifferentStat_NotIncluded()
    {
        var buffs = new List<ActiveBuff>
        {
            Additive(BuffStat.AttackBonus, -2f, expiresAt: 10.0),
        };
        // Querying ArmorValue — the AttackBonus buff must not bleed through.
        Assert.Equal(0f, BuffCalculator.GetAdditiveModifier(buffs, BuffStat.ArmorValue, currentTime: 0.0));
    }

    // ── Multiplicative condensation ───────────────────────────────────────────

    [Fact]
    public void GetMultiplicativeModifier_NoneActive_ReturnsOne()
    {
        // No buffs → neutral multiplier (×1.0).
        var buffs = new List<ActiveBuff>();
        Assert.Equal(1f, BuffCalculator.GetMultiplicativeModifier(buffs, BuffStat.IncomingDamage, currentTime: 1.0));
    }

    [Fact]
    public void GetMultiplicativeModifier_SingleVulnerability_ReturnsOnePointFive()
    {
        // Overextended: +0.5 → 1 + 0.5 = 1.5× incoming damage.
        var buffs = new List<ActiveBuff>
        {
            Multiplicative(BuffStat.IncomingDamage, 0.5f, expiresAt: 10.0),
        };
        Assert.Equal(1.5f, BuffCalculator.GetMultiplicativeModifier(buffs, BuffStat.IncomingDamage, currentTime: 0.0),
            precision: 5);
    }

    [Fact]
    public void GetMultiplicativeModifier_TwoVulnerabilityBuffs_SumsBeforeMultiply()
    {
        // THE KEY TEST: two +50% buffs must combine to ×2.0, not ×2.25 (1.5×1.5).
        // Correct:   1 + (0.5 + 0.5) = 2.0
        // Incorrect: 1.5 × 1.5       = 2.25
        var buffs = new List<ActiveBuff>
        {
            Multiplicative(BuffStat.IncomingDamage, 0.5f, expiresAt: 10.0),
            Multiplicative(BuffStat.IncomingDamage, 0.5f, expiresAt: 10.0),
        };
        float result = BuffCalculator.GetMultiplicativeModifier(buffs, BuffStat.IncomingDamage, currentTime: 0.0);
        Assert.Equal(2.0f, result, precision: 5);
    }

    [Fact]
    public void GetMultiplicativeModifier_SpeedDebuff_ReducesBelow1()
    {
        // Stumble: -0.5 → 1 + (-0.5) = 0.5× speed (half speed).
        var buffs = new List<ActiveBuff>
        {
            Multiplicative(BuffStat.MoveSpeed, -0.5f, expiresAt: 10.0),
        };
        Assert.Equal(0.5f, BuffCalculator.GetMultiplicativeModifier(buffs, BuffStat.MoveSpeed, currentTime: 0.0),
            precision: 5);
    }

    [Fact]
    public void GetMultiplicativeModifier_ExpiredBuff_ExcludedFromProduct()
    {
        var buffs = new List<ActiveBuff>
        {
            Multiplicative(BuffStat.IncomingDamage, 0.5f, expiresAt: 3.0),  // expired
            Multiplicative(BuffStat.IncomingDamage, 0.5f, expiresAt: 15.0), // active
        };
        // Only the second buff is active → 1 + 0.5 = 1.5
        float result = BuffCalculator.GetMultiplicativeModifier(buffs, BuffStat.IncomingDamage, currentTime: 10.0);
        Assert.Equal(1.5f, result, precision: 5);
    }

    // ── IsActive (boolean gate) ───────────────────────────────────────────────

    [Fact]
    public void IsActive_NoBuff_ReturnsFalse()
    {
        var buffs = new List<ActiveBuff>();
        Assert.False(BuffCalculator.IsActive(buffs, BuffStat.Stun, currentTime: 0.0));
    }

    [Fact]
    public void IsActive_ActiveStunBuff_ReturnsTrue()
    {
        var buffs = new List<ActiveBuff> { Additive(BuffStat.Stun, 1f, expiresAt: 5.0) };
        Assert.True(BuffCalculator.IsActive(buffs, BuffStat.Stun, currentTime: 0.0));
    }

    [Fact]
    public void IsActive_ExpiredStunBuff_ReturnsFalse()
    {
        var buffs = new List<ActiveBuff> { Additive(BuffStat.Stun, 1f, expiresAt: 2.0) };
        Assert.False(BuffCalculator.IsActive(buffs, BuffStat.Stun, currentTime: 5.0));
    }

    [Fact]
    public void IsActive_DisarmBuff_WrongStatQuery_ReturnsFalse()
    {
        var buffs = new List<ActiveBuff> { Additive(BuffStat.Disarm, 1f, expiresAt: 5.0) };
        Assert.False(BuffCalculator.IsActive(buffs, BuffStat.Stun, currentTime: 0.0));
    }

    // ── Bleed damage ──────────────────────────────────────────────────────────

    [Fact]
    public void GetBleedDamagePerTick_NoBleed_ReturnsZero()
    {
        var buffs = new List<ActiveBuff>();
        Assert.Equal(0f, BuffCalculator.GetBleedDamagePerTick(buffs, currentTime: 0.0));
    }

    [Fact]
    public void GetBleedDamagePerTick_TwoStacksSum()
    {
        // Two bleed sources (e.g. two crits) stack additively.
        var buffs = new List<ActiveBuff>
        {
            Additive(BuffStat.BleedDamage, 2f, expiresAt: 10.0),
            Additive(BuffStat.BleedDamage, 2f, expiresAt: 10.0),
        };
        Assert.Equal(4f, BuffCalculator.GetBleedDamagePerTick(buffs, currentTime: 0.0));
    }

    [Fact]
    public void GetBleedDamagePerTick_ExpiredStackExcluded()
    {
        var buffs = new List<ActiveBuff>
        {
            Additive(BuffStat.BleedDamage, 2f, expiresAt: 3.0),  // expired
            Additive(BuffStat.BleedDamage, 2f, expiresAt: 20.0), // active
        };
        Assert.Equal(2f, BuffCalculator.GetBleedDamagePerTick(buffs, currentTime: 10.0));
    }

    // ── Mixed stat isolation ──────────────────────────────────────────────────

    [Fact]
    public void MultipleStatsPresent_EachQueriedIndependently()
    {
        var buffs = new List<ActiveBuff>
        {
            Additive(BuffStat.ArmorValue,  -2f, expiresAt: 10.0),
            Additive(BuffStat.AttackBonus, -3f, expiresAt: 10.0),
            Multiplicative(BuffStat.IncomingDamage, 0.5f, expiresAt: 10.0),
        };
        Assert.Equal(-2f, BuffCalculator.GetAdditiveModifier(buffs, BuffStat.ArmorValue, currentTime: 0.0));
        Assert.Equal(-3f, BuffCalculator.GetAdditiveModifier(buffs, BuffStat.AttackBonus, currentTime: 0.0));
        Assert.Equal(1.5f, BuffCalculator.GetMultiplicativeModifier(buffs, BuffStat.IncomingDamage, currentTime: 0.0),
            precision: 5);
        // Stats not present return neutral values.
        Assert.Equal(0f,  BuffCalculator.GetAdditiveModifier(buffs, BuffStat.BleedDamage, currentTime: 0.0));
        Assert.Equal(1f,  BuffCalculator.GetMultiplicativeModifier(buffs, BuffStat.MoveSpeed, currentTime: 0.0));
    }
}
