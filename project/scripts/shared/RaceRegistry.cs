using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MankersKingdoms.Shared;

/// <summary>
/// Hardcoded registry of the four vertical-slice races.
/// Full data-file loading (data/base/races/*.json) is a post-slice expansion (ADR-0009).
///
/// Human:    neutral, player picks +1 to any stat.
/// Dwarf:    Con +1, Cha −1 (dormant — Cha not in StatBlock until 1.0); saving throws
///           +2 vs poison/magic; +2 AB vs Goblin and Orc.
/// Elf:      Dex +1, Con −1; saving throws +4 vs sleep/charm.
/// Halfling: Dex +1, Str −1; saving throws +2 vs magic/poison.
///
/// See docs/gdd/character-creation.md §3 and §12.
/// </summary>
public static class RaceRegistry
{
    private static readonly ReadOnlyDictionary<string, int> _empty =
        new(new Dictionary<string, int>());

    public static readonly IReadOnlyList<RaceData> All = new[]
    {
        new RaceData(
            RaceId:             "race.human",
            DisplayNameKey:     "race.human.name",
            StatModifiers:      _empty,
            ChoiceModifier:     1,          // +1 to any stat of player's choice
            SavingThrowBonuses: _empty,
            CombatBonusVs:      _empty
        ),
        new RaceData(
            RaceId:             "race.dwarf",
            DisplayNameKey:     "race.dwarf.name",
            // Cha −1 is dormant: Charisma not in StatBlock until 1.0 (character-creation.md §3 + §9)
            StatModifiers:      new ReadOnlyDictionary<string, int>(new Dictionary<string, int>
                { ["Con"] = 1 }),
            ChoiceModifier:     0,
            SavingThrowBonuses: new ReadOnlyDictionary<string, int>(new Dictionary<string, int>
                { ["poison"] = 2, ["magic"] = 2 }),
            // Tag is a substring match against monster ID (e.g. "monster.goblin.scout" ⊇ "goblin")
            CombatBonusVs:      new ReadOnlyDictionary<string, int>(new Dictionary<string, int>
                { ["goblin"] = 2, ["orc"] = 2 })
        ),
        new RaceData(
            RaceId:             "race.elf",
            DisplayNameKey:     "race.elf.name",
            StatModifiers:      new ReadOnlyDictionary<string, int>(new Dictionary<string, int>
                { ["Dex"] = 1, ["Con"] = -1 }),
            ChoiceModifier:     0,
            SavingThrowBonuses: new ReadOnlyDictionary<string, int>(new Dictionary<string, int>
                { ["sleep"] = 4, ["charm"] = 4 }),
            CombatBonusVs:      _empty
        ),
        new RaceData(
            RaceId:             "race.halfling",
            DisplayNameKey:     "race.halfling.name",
            StatModifiers:      new ReadOnlyDictionary<string, int>(new Dictionary<string, int>
                { ["Dex"] = 1, ["Str"] = -1 }),
            ChoiceModifier:     0,
            SavingThrowBonuses: new ReadOnlyDictionary<string, int>(new Dictionary<string, int>
                { ["magic"] = 2, ["poison"] = 2 }),
            CombatBonusVs:      _empty
        ),
    };

    private static readonly Dictionary<string, RaceData> _byId = new();

    static RaceRegistry()
    {
        foreach (var race in All)
            _byId[race.RaceId] = race;
    }

    /// <summary>Returns the RaceData for the given raceId, or null if not found.</summary>
    public static RaceData? Find(string raceId) =>
        _byId.TryGetValue(raceId, out var r) ? r : null;
}
