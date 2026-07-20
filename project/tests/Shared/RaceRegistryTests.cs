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

    /// <summary>
    /// Dwarf: Con +1. Cha −1 is dormant (Cha not in StatBlock until 1.0 — character-creation.md §9).
    /// Wis is unchanged. Test renamed to reflect the live modifier only.
    /// </summary>
    [Fact]
    public void Dwarf_ConPlusOne_ChaPenaltyDormant()
    {
        var race   = RaceRegistry.Find("race.dwarf")!;
        var rolled = new StatBlock(10, 10, 10, 10);
        var result = race.Apply(rolled);
        Assert.Equal(10, result.Str);
        Assert.Equal(10, result.Dex);
        Assert.Equal(11, result.Con);
        Assert.Equal(10, result.Wis); // Wis unchanged; penalty is Cha (dormant)
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

    // ── Racial saving throw bonuses (character-creation.md §12) ─────────────

    [Fact]
    public void Elf_HasSleepAndCharmSavingThrowBonuses()
    {
        var race = RaceRegistry.Find("race.elf")!;
        Assert.True(race.SavingThrowBonuses.TryGetValue("sleep", out int sleep) && sleep == 4,
            "Elf should have +4 saving throw vs sleep");
        Assert.True(race.SavingThrowBonuses.TryGetValue("charm", out int charm) && charm == 4,
            "Elf should have +4 saving throw vs charm");
    }

    [Fact]
    public void Dwarf_HasPoisonAndMagicSavingThrowBonuses()
    {
        var race = RaceRegistry.Find("race.dwarf")!;
        Assert.True(race.SavingThrowBonuses.TryGetValue("poison", out int poison) && poison == 2,
            "Dwarf should have +2 saving throw vs poison");
        Assert.True(race.SavingThrowBonuses.TryGetValue("magic", out int magic) && magic == 2,
            "Dwarf should have +2 saving throw vs magic");
    }

    [Fact]
    public void Halfling_HasMagicAndPoisonSavingThrowBonuses()
    {
        var race = RaceRegistry.Find("race.halfling")!;
        Assert.True(race.SavingThrowBonuses.TryGetValue("magic", out int magic) && magic == 2,
            "Halfling should have +2 saving throw vs magic");
        Assert.True(race.SavingThrowBonuses.TryGetValue("poison", out int poison) && poison == 2,
            "Halfling should have +2 saving throw vs poison");
    }

    [Fact]
    public void Human_HasNoSavingThrowBonuses()
    {
        var race = RaceRegistry.Find("race.human")!;
        Assert.Empty(race.SavingThrowBonuses);
    }

    // ── Racial combat bonuses (character-creation.md §12) ───────────────────

    [Fact]
    public void Dwarf_HasCombatBonusVsGoblinAndOrc()
    {
        var race = RaceRegistry.Find("race.dwarf")!;
        Assert.True(race.CombatBonusVs.TryGetValue("goblin", out int goblin) && goblin == 2,
            "Dwarf should have +2 AB vs goblin");
        Assert.True(race.CombatBonusVs.TryGetValue("orc", out int orc) && orc == 2,
            "Dwarf should have +2 AB vs orc");
    }

    [Fact]
    public void NonDwarf_HasNoCombatBonuses()
    {
        foreach (var raceId in new[] { "race.human", "race.elf", "race.halfling" })
        {
            var race = RaceRegistry.Find(raceId)!;
            Assert.Empty(race.CombatBonusVs);
        }
    }
}
