using MankersKingdoms.Shared;
using Xunit;

namespace MankersKingdoms.Tests.Shared;

/// <summary>
/// Tests for SavingThrowResolver (combat.md §16, character-creation.md §12).
///
/// Formula: 1d20 + floor(skillLevel/10) + racialBonus ≥ difficulty → success.
/// </summary>
public class SavingThrowResolverTests
{
    // ── Basic formula ────────────────────────────────────────────────────────

    [Fact]
    public void NoSkill_NoRace_ExactlyMeetsDifficulty_Succeeds()
    {
        var (success, roll, racialBonus, total) = SavingThrowResolver.Resolve(
            d20Roll: 15, skillLevel: 0, race: null, category: "poison", difficulty: 15);
        Assert.True(success);
        Assert.Equal(15, roll);
        Assert.Equal(0,  racialBonus);
        Assert.Equal(15, total);
    }

    [Fact]
    public void NoSkill_NoRace_OneBelowDifficulty_Fails()
    {
        var (success, _, _, _) = SavingThrowResolver.Resolve(
            d20Roll: 14, skillLevel: 0, race: null, category: "poison", difficulty: 15);
        Assert.False(success);
    }

    [Fact]
    public void SkillBonus_FlooredAtTen_AddsOne()
    {
        // floor(10/10) = 1
        var (success, _, _, total) = SavingThrowResolver.Resolve(
            d20Roll: 10, skillLevel: 10, race: null, category: "magic", difficulty: 12);
        Assert.Equal(11, total);
        Assert.False(success);
    }

    [Fact]
    public void SkillLevel_19_StillAddsOne()
    {
        // floor(19/10) = 1
        var (_, _, _, total) = SavingThrowResolver.Resolve(
            d20Roll: 5, skillLevel: 19, race: null, category: "charm", difficulty: 99);
        Assert.Equal(6, total);
    }

    [Fact]
    public void SkillLevel_20_AddsTwoBonus()
    {
        // floor(20/10) = 2
        var (_, _, _, total) = SavingThrowResolver.Resolve(
            d20Roll: 5, skillLevel: 20, race: null, category: "charm", difficulty: 99);
        Assert.Equal(7, total);
    }

    // ── Racial bonuses ───────────────────────────────────────────────────────

    [Fact]
    public void Elf_VsSleep_Gets4Bonus()
    {
        var elf = RaceRegistry.Find("race.elf")!;
        var (success, _, racialBonus, total) = SavingThrowResolver.Resolve(
            d20Roll: 10, skillLevel: 0, race: elf, category: "sleep", difficulty: 15);
        Assert.Equal(4, racialBonus);
        Assert.Equal(14, total);
        Assert.False(success); // 14 < 15
    }

    [Fact]
    public void Elf_VsSleep_Roll11Succeeds()
    {
        var elf = RaceRegistry.Find("race.elf")!;
        var (success, _, _, total) = SavingThrowResolver.Resolve(
            d20Roll: 11, skillLevel: 0, race: elf, category: "sleep", difficulty: 15);
        Assert.Equal(15, total);
        Assert.True(success);
    }

    [Fact]
    public void Elf_VsCharm_Gets4Bonus()
    {
        var elf = RaceRegistry.Find("race.elf")!;
        var (_, _, racialBonus, _) = SavingThrowResolver.Resolve(
            d20Roll: 10, skillLevel: 0, race: elf, category: "charm", difficulty: 99);
        Assert.Equal(4, racialBonus);
    }

    [Fact]
    public void Elf_VsPoison_GetsNoBonus()
    {
        var elf = RaceRegistry.Find("race.elf")!;
        var (_, _, racialBonus, _) = SavingThrowResolver.Resolve(
            d20Roll: 10, skillLevel: 0, race: elf, category: "poison", difficulty: 99);
        Assert.Equal(0, racialBonus);
    }

    [Fact]
    public void Dwarf_VsPoison_Gets2Bonus()
    {
        var dwarf = RaceRegistry.Find("race.dwarf")!;
        var (_, _, racialBonus, _) = SavingThrowResolver.Resolve(
            d20Roll: 10, skillLevel: 0, race: dwarf, category: "poison", difficulty: 99);
        Assert.Equal(2, racialBonus);
    }

    [Fact]
    public void Dwarf_VsMagic_Gets2Bonus()
    {
        var dwarf = RaceRegistry.Find("race.dwarf")!;
        var (_, _, racialBonus, _) = SavingThrowResolver.Resolve(
            d20Roll: 10, skillLevel: 0, race: dwarf, category: "magic", difficulty: 99);
        Assert.Equal(2, racialBonus);
    }

    [Fact]
    public void Dwarf_VsCharm_GetsNoBonus()
    {
        var dwarf = RaceRegistry.Find("race.dwarf")!;
        var (_, _, racialBonus, _) = SavingThrowResolver.Resolve(
            d20Roll: 10, skillLevel: 0, race: dwarf, category: "charm", difficulty: 99);
        Assert.Equal(0, racialBonus);
    }

    [Fact]
    public void Halfling_VsMagic_Gets2Bonus()
    {
        var halfling = RaceRegistry.Find("race.halfling")!;
        var (_, _, racialBonus, _) = SavingThrowResolver.Resolve(
            d20Roll: 10, skillLevel: 0, race: halfling, category: "magic", difficulty: 99);
        Assert.Equal(2, racialBonus);
    }

    [Fact]
    public void Halfling_VsPoison_Gets2Bonus()
    {
        var halfling = RaceRegistry.Find("race.halfling")!;
        var (_, _, racialBonus, _) = SavingThrowResolver.Resolve(
            d20Roll: 10, skillLevel: 0, race: halfling, category: "poison", difficulty: 99);
        Assert.Equal(2, racialBonus);
    }

    [Fact]
    public void Human_GetsNoSavingThrowBonus()
    {
        var human = RaceRegistry.Find("race.human")!;
        var (_, _, racialBonus, _) = SavingThrowResolver.Resolve(
            d20Roll: 10, skillLevel: 0, race: human, category: "poison", difficulty: 99);
        Assert.Equal(0, racialBonus);
    }

    // ── Case-insensitive category matching ───────────────────────────────────

    [Fact]
    public void CategoryMatching_CaseInsensitive()
    {
        var elf = RaceRegistry.Find("race.elf")!;
        var lower = SavingThrowResolver.Resolve(d20Roll: 10, skillLevel: 0, race: elf, category: "sleep",  difficulty: 99);
        var upper = SavingThrowResolver.Resolve(d20Roll: 10, skillLevel: 0, race: elf, category: "SLEEP",  difficulty: 99);
        var mixed = SavingThrowResolver.Resolve(d20Roll: 10, skillLevel: 0, race: elf, category: "Sleep",  difficulty: 99);
        Assert.Equal(lower.racialBonus, upper.racialBonus);
        Assert.Equal(lower.racialBonus, mixed.racialBonus);
    }

    // ── Skill + racial combined ──────────────────────────────────────────────

    [Fact]
    public void DwarfWithSkill20_VsPoison_TotalIsRollPlus4()
    {
        // floor(20/10)=2 skillBonus + 2 racial = 4 bonus
        var dwarf = RaceRegistry.Find("race.dwarf")!;
        var (_, _, _, total) = SavingThrowResolver.Resolve(
            d20Roll: 8, skillLevel: 20, race: dwarf, category: "poison", difficulty: 99);
        Assert.Equal(8 + 2 + 2, total); // 12
    }
}
