using MankersKingdoms.Shared;
using Xunit;

namespace MankersKingdoms.Tests.Shared;

public class StatBlockTests
{
    // ── SkillCap formula (ADR-0019) ───────────────────────────────────────────

    [Fact]
    public void SkillCap_Stat18_Returns99()
    {
        Assert.Equal(99, StatBlock.SkillCap(18));
    }

    [Fact]
    public void SkillCap_Stat10_Returns55()
    {
        // floor(99 × 10 / 18) = floor(55.0) = 55
        Assert.Equal(55, StatBlock.SkillCap(10));
    }

    [Fact]
    public void SkillCap_Stat3_Returns16()
    {
        // floor(99 × 3 / 18) = floor(16.5) = 16
        Assert.Equal(16, StatBlock.SkillCap(3));
    }

    [Fact]
    public void SkillCap_Stat16_Returns88()
    {
        // floor(99 × 16 / 18) = floor(88.0) = 88
        Assert.Equal(88, StatBlock.SkillCap(16));
    }

    // ── Clamped ──────────────────────────────────────────────────────────────

    [Fact]
    public void Clamped_OverMaxStat_ClampsTo18()
    {
        var block = new StatBlock(20, 18, 3, 18).Clamped();
        Assert.Equal(18, block.Str);
    }

    [Fact]
    public void Clamped_UnderMinStat_ClampsTo3()
    {
        var block = new StatBlock(3, 2, 3, 3).Clamped();
        Assert.Equal(3, block.Dex);
    }

    [Fact]
    public void Clamped_ValidStats_Unchanged()
    {
        var original = new StatBlock(12, 14, 10, 8);
        var clamped  = original.Clamped();
        Assert.Equal(original, clamped);
    }
}
