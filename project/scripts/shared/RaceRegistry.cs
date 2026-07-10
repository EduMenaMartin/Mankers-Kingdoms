using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MankersKingdoms.Shared;

/// <summary>
/// Hardcoded registry of the four vertical-slice races.
/// Full data-file loading (data/base/races/*.json) is a post-slice expansion (ADR-0009).
///
/// Human:   neutral, player picks +1 to any stat.
/// Dwarf:   Con +1, Wis −1 (stoic endurance, less attunement to nature).
/// Elf:     Dex +1, Con −1 (grace under fire, fragile constitution).
/// Halfling: Dex +1, Str −1 (nimble, underpowered).
///
/// See docs/gdd/character-creation.md §3.
/// </summary>
public static class RaceRegistry
{
    private static readonly ReadOnlyDictionary<string, int> _empty =
        new(new Dictionary<string, int>());

    public static readonly IReadOnlyList<RaceData> All = new[]
    {
        new RaceData(
            RaceId:           "race.human",
            DisplayNameKey:   "race.human.name",
            StatModifiers:    _empty,
            ChoiceModifier:   1          // +1 to any stat of player's choice
        ),
        new RaceData(
            RaceId:           "race.dwarf",
            DisplayNameKey:   "race.dwarf.name",
            StatModifiers:    new ReadOnlyDictionary<string, int>(new Dictionary<string, int>
                { ["Con"] = 1, ["Wis"] = -1 }),
            ChoiceModifier:   0
        ),
        new RaceData(
            RaceId:           "race.elf",
            DisplayNameKey:   "race.elf.name",
            StatModifiers:    new ReadOnlyDictionary<string, int>(new Dictionary<string, int>
                { ["Dex"] = 1, ["Con"] = -1 }),
            ChoiceModifier:   0
        ),
        new RaceData(
            RaceId:           "race.halfling",
            DisplayNameKey:   "race.halfling.name",
            StatModifiers:    new ReadOnlyDictionary<string, int>(new Dictionary<string, int>
                { ["Dex"] = 1, ["Str"] = -1 }),
            ChoiceModifier:   0
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
