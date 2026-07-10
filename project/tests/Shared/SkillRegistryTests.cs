using MankersKingdoms.Shared;
using Xunit;

namespace MankersKingdoms.Tests.Shared;

public class SkillRegistryTests
{
    // ── Registry completeness ─────────────────────────────────────────────────

    [Fact]
    public void All_ContainsSixSkills()
    {
        Assert.Equal(6, SkillRegistry.All.Count);
    }

    [Theory]
    [InlineData("skill.melee")]
    [InlineData("skill.ranged")]
    [InlineData("skill.athletics")]
    [InlineData("skill.woodcutting")]
    [InlineData("skill.foraging")]
    [InlineData("skill.cooking")]
    public void Find_ReturnsSkillForKnownId(string id)
    {
        var skill = SkillRegistry.Find(id);
        Assert.NotNull(skill);
        Assert.Equal(id, skill!.Id);
    }

    [Fact]
    public void Find_ReturnsNullForUnknownId()
    {
        Assert.Null(SkillRegistry.Find("skill.alchemy"));
    }

    // ── XP / level formula ────────────────────────────────────────────────────

    [Fact]
    public void GetCap_MeleeGovernsStr()
    {
        var skill = SkillRegistry.Find("skill.melee")!;
        var stats = new StatBlock(Str: 18, Dex: 10, Con: 10, Wis: 10);
        // SkillCap(18) = 18*99/18 = 99
        Assert.Equal(99, skill.GetCap(stats));
    }

    [Fact]
    public void GetCap_RangedGovernsDexter()
    {
        var skill = SkillRegistry.Find("skill.ranged")!;
        var stats = new StatBlock(Str: 10, Dex: 12, Con: 10, Wis: 10);
        // SkillCap(12) = 12*99/18 = 66
        Assert.Equal(66, skill.GetCap(stats));
    }

    [Fact]
    public void GetCap_AthleticsUsesHigherOfStrCon()
    {
        var skill = SkillRegistry.Find("skill.athletics")!;

        // Str=14 (cap=77), Con=10 (cap=55) → best = 77
        var statsStrHigher = new StatBlock(Str: 14, Dex: 10, Con: 10, Wis: 10);
        Assert.Equal(77, skill.GetCap(statsStrHigher));

        // Str=10 (cap=55), Con=16 (cap=88) → best = 88
        var statsConHigher = new StatBlock(Str: 10, Dex: 10, Con: 16, Wis: 10);
        Assert.Equal(88, skill.GetCap(statsConHigher));
    }

    [Fact]
    public void GetCap_WoodcuttingGovernsStr()
    {
        var skill = SkillRegistry.Find("skill.woodcutting")!;
        var stats = new StatBlock(Str: 9, Dex: 10, Con: 10, Wis: 10);
        // SkillCap(9) = 9*99/18 = 49
        Assert.Equal(49, skill.GetCap(stats));
    }

    // ── Demo gate: 75 chops → Woodcutting level 15 ───────────────────────────

    [Fact]
    public void WoodcuttingDemoGate_75ActionsReachLevel15()
    {
        var skill = SkillRegistry.Find("skill.woodcutting")!;
        // rawLevel = rawXp / XpPerLevel = 75 / 5 = 15
        int rawXp    = 75 * skill.XpPerAction;
        int rawLevel = rawXp / skill.XpPerLevel;
        Assert.Equal(15, rawLevel);
    }

    // ── Tool tier ─────────────────────────────────────────────────────────────

    [Fact]
    public void WoodcuttingHasBronzeAxeTierAtLevel15()
    {
        var skill = SkillRegistry.Find("skill.woodcutting")!;
        Assert.Single(skill.ToolTiers);
        Assert.Equal(15,                    skill.ToolTiers[0].MinLevel);
        Assert.Equal("item.tool.bronze_axe", skill.ToolTiers[0].GrantedItemId);
    }

    [Fact]
    public void OtherSkills_HaveNoToolTiers()
    {
        foreach (var skill in SkillRegistry.All)
        {
            if (skill.Id == "skill.woodcutting") continue;
            Assert.Empty(skill.ToolTiers);
        }
    }
}
