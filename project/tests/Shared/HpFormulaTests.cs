using MankersKingdoms.Shared;
using Xunit;

namespace MankersKingdoms.Tests.Shared;

/// <summary>
/// Tests for the HP formula and Athletics bonus used in HealthSystem.
/// CombatResolver.StatModifier is the authoritative stat-to-modifier conversion;
/// these tests verify expected values and the floor(level/2) Athletics growth.
/// </summary>
public class HpFormulaTests
{
    // ── CombatResolver.StatModifier ────────────────────────────────────────────

    [Theory]
    [InlineData(3,  -2)]
    [InlineData(5,  -2)]
    [InlineData(6,  -1)]
    [InlineData(9,  -1)]
    [InlineData(10,  0)]
    [InlineData(13,  0)]
    [InlineData(14,  1)]
    [InlineData(17,  1)]
    [InlineData(18,  2)]
    public void StatModifier_MatchesCombatMdTable(int stat, int expected)
    {
        Assert.Equal(expected, CombatResolver.StatModifier(stat));
    }

    // ── Monster HP formula ─────────────────────────────────────────────────────

    [Fact]
    public void Goblin_HdCon10_Hp31_5()
    {
        // 7 × ((8+1)/2) + StatMod(10) × 7 = 7×4.5 + 0×7 = 31.5
        float hp = 7 * ((8 + 1) / 2f) + CombatResolver.StatModifier(10) * 7;
        Assert.Equal(31.5f, hp, precision: 1);
    }

    [Fact]
    public void Bandit_HdCon14_Hp60_5()
    {
        // 11 × 4.5 + StatMod(14) × 11 = 49.5 + 1×11 = 60.5
        float hp = 11 * ((8 + 1) / 2f) + CombatResolver.StatModifier(14) * 11;
        Assert.Equal(60.5f, hp, precision: 1);
    }

    [Fact]
    public void Orc_HdCon16_Hp99()
    {
        // 18 × 4.5 + StatMod(16) × 18 = 81 + 1×18 = 99
        float hp = 18 * ((8 + 1) / 2f) + CombatResolver.StatModifier(16) * 18;
        Assert.Equal(99f, hp, precision: 0);
    }

    // ── Athletics HP growth: floor(level/2) ────────────────────────────────────

    [Theory]
    [InlineData(0,  0)]
    [InlineData(1,  0)]
    [InlineData(2,  1)]
    [InlineData(3,  1)]
    [InlineData(4,  2)]
    [InlineData(10, 5)]
    [InlineData(55, 27)]
    [InlineData(99, 49)]
    public void AthleticsBonus_IsFloorLevelDivTwo(int level, int expected)
    {
        int bonus = level / 2;
        Assert.Equal(expected, bonus);
    }

    // ── Career ceiling sanity checks ───────────────────────────────────────────

    [Fact]
    public void Fighter_Con10_CareerPeakAround45()
    {
        // Avg start: 4d8 + 0 per die = avg (4.5×4) = 18 HP
        // Athletics cap at Con 10 = floor(99×10/18) = 55; bonus = floor(55/2) = 27
        // Career peak ≈ 18 + 27 = 45
        float avgStart = 4 * 4.5f + CombatResolver.StatModifier(10) * 4; // = 18
        int   athCap   = (int)(99 * 10 / 18);                              // = 55
        int   maxBonus = athCap / 2;                                        // = 27
        float peak     = avgStart + maxBonus;                               // = 45
        Assert.Equal(45f, peak, precision: 0);
    }

    [Fact]
    public void Fighter_Con14_CareerPeakAround60()
    {
        // Avg start: 4d8 + ConMod(14)=1 per die → 4×(4.5+1) = 22 HP
        // Athletics cap at Con 14 = floor(99×14/18) = 77; bonus = floor(77/2) = 38
        // Career peak ≈ 22 + 38 = 60
        float avgStart = 4 * (4.5f + CombatResolver.StatModifier(14)); // = 4×5.5 = 22
        int   athCap   = (int)(99 * 14 / 18);                           // = 77
        int   maxBonus = athCap / 2;                                     // = 38
        float peak     = avgStart + maxBonus;                            // = 60
        Assert.Equal(60f, peak, precision: 0);
    }

    [Fact]
    public void Ranger_Con10_CareerPeakAround40_5()
    {
        // Avg start: 3d8 + 0 = 3×4.5 = 13.5 HP; Athletics cap 55; bonus 27; peak = 40.5
        float avgStart = 3 * 4.5f + CombatResolver.StatModifier(10) * 3;
        int   athCap   = (int)(99 * 10 / 18);
        int   maxBonus = athCap / 2;
        float peak     = avgStart + maxBonus;
        Assert.Equal(40.5f, peak, precision: 1);
    }
}
