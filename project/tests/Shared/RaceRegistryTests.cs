using MankersKingdoms.Shared;
using Xunit;

namespace MankersKingdoms.Tests.Shared;

public class RaceRegistryTests
{
    // ── Registry completeness ────────────────────────────────────────────────

    [Fact]
    public void All_Contains4Races()
    {
        Assert.Equal(4, RaceRegistry.All.Count);
    }

    [Theory]
    [InlineData("race.human")]
    [InlineData("race.dwarf")]
    [InlineData("race.elf")]
    [InlineData("race.halfling")]
    public void Find_KnownId_ReturnsRace(string raceId)
    {
        Assert.NotNull(RaceRegistry.Find(raceId));
    }

    [Fact]
    public void Find_UnknownId_ReturnsNull()
    {
        Assert.Null(RaceRegistry.Find("race.orc"));
    }

    // ── Stat modifiers (docs/gdd/character-creation.md §3) ──────────────────

    [Fact]
    public void Human_NoStatModifiers_PureRolledStats()
    {
        var race   = RaceRegistry.Find("race.human")!;
        var rolled = new StatBlock(10, 10, 10, 10);
        // Choice: +1 to Str
        var result = race.Apply(rolled, "Str");
        Assert.Equal(11, result.Str);
        Assert.Equal(10, result.Dex);
        Assert.Equal(10, result.Con);
        Assert.Equal(10, result.Wis);
    }

    [Fact]
    public void Human_ChoiceModifierIs1()
    {
        Assert.Equal(1, RaceRegistry.Find("race.human")!.ChoiceModifier);
    }

    [Fact]
    public void Dwarf_ConPlusOne_WisMinusOne()
    {
        var race   = RaceRegistry.Find("race.dwarf")!;
        var rolled = new StatBlock(10, 10, 10, 10);
        var result = race.Apply(rolled);
        Assert.Equal(10, result.Str);
        Assert.Equal(10, result.Dex);
        Assert.Equal(11, result.Con);
        Assert.Equal(9,  result.Wis);
    }

    [Fact]
    public void Elf_DexPlusOne_ConMinusOne()
    {
        var race   = RaceRegistry.Find("race.elf")!;
        var rolled = new StatBlock(10, 10, 10, 10);
        var result = race.Apply(rolled);
        Assert.Equal(10, result.Str);
        Assert.Equal(11, result.Dex);
        Assert.Equal(9,  result.Con);
        Assert.Equal(10, result.Wis);
    }

    [Fact]
    public void Halfling_DexPlusOne_StrMinusOne()
    {
        var race   = RaceRegistry.Find("race.halfling")!;
        var rolled = new StatBlock(10, 10, 10, 10);
        var result = race.Apply(rolled);
        Assert.Equal(9,  result.Str);
        Assert.Equal(11, result.Dex);
        Assert.Equal(10, result.Con);
        Assert.Equal(10, result.Wis);
    }

    // ── Clamp behaviour ──────────────────────────────────────────────────────

    [Fact]
    public void Apply_DoesNotExceed18()
    {
        var race   = RaceRegistry.Find("race.elf")!;   // Dex +1
        var rolled = new StatBlock(10, 18, 10, 10);
        var result = race.Apply(rolled);
        Assert.Equal(18, result.Dex);                  // clamped, not 19
    }

    [Fact]
    public void Apply_DoesNotGoBelowThree()
    {
        var race   = RaceRegistry.Find("race.halfling")!; // Str −1
        var rolled = new StatBlock(3, 10, 10, 10);
        var result = race.Apply(rolled);
        Assert.Equal(3, result.Str);                      // clamped, not 2
    }

    // ── ClassKitRegistry SkillBumps (M5 addition) ───────────────────────────

    [Fact]
    public void ClassKit_Fighter_HasMeleeAndAthleticsSkillBumps()
    {
        var kit = ClassKitRegistry.Find("class.fighter")!;
        Assert.Contains(kit.SkillBumps, b => b.SkillId == "skill.melee"     && b.Amount == 5);
        Assert.Contains(kit.SkillBumps, b => b.SkillId == "skill.athletics" && b.Amount == 3);
    }

    [Fact]
    public void ClassKit_Ranger_HasRangedAndForagingSkillBumps()
    {
        var kit = ClassKitRegistry.Find("class.ranger")!;
        Assert.Contains(kit.SkillBumps, b => b.SkillId == "skill.ranged"   && b.Amount == 5);
        Assert.Contains(kit.SkillBumps, b => b.SkillId == "skill.foraging" && b.Amount == 3);
    }

    [Fact]
    public void ClassKit_NoLongerHasStrOrDexFields()
    {
        // Regression: Str/Dex were removed in M5; stats are now player-rolled.
        // This test simply confirms ClassKitData compiles and is loaded correctly
        // without those fields — the registry load itself is the assertion.
        var fighter = ClassKitRegistry.Find("class.fighter");
        var ranger  = ClassKitRegistry.Find("class.ranger");
        Assert.NotNull(fighter);
        Assert.NotNull(ranger);
        Assert.Equal(2, ClassKitRegistry.All.Count);
    }
}
