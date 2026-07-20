using System.Collections.Generic;

namespace MankersKingdoms.Shared;

/// <summary>
/// Immutable definition of a playable race.
/// Loaded from data/base/races/*.json via RaceRegistry.
///
/// RaceId:             stable string ID, e.g. "race.human". Referenced by GameSession.ChosenRaceId.
/// DisplayNameKey:     Loc key for the race name shown in CharacterCreateScreen.
/// StatModifiers:      additive deltas applied to the rolled StatBlock (e.g. Dex +1, Con −1).
///                     Keys match StatBlock fields (case-insensitive): "Str", "Dex", "Con", "Wis".
/// ChoiceModifier:     Human bonus — player picks any one stat to bump by +1 at char creation.
///                     0 means no choice (all other races).
/// SavingThrowBonuses: additive bonus on saving throw rolls keyed by category tag.
///                     E.g. {"sleep"=4, "charm"=4} for Elf (character-creation.md §12).
///                     Empty for races with no saving-throw trait.
/// CombatBonusVs:      additive Attack Bonus when the target monster ID contains the given tag.
///                     E.g. {"goblin"=2, "orc"=2} for Dwarf. Tag is matched as a substring
///                     of the monster's ID (e.g. "monster.goblin.scout" contains "goblin").
///                     Empty for races with no combat bonus.
///
/// See docs/gdd/character-creation.md §3 and §12.
/// </summary>
public sealed record RaceData(
    string                           RaceId,
    string                           DisplayNameKey,
    IReadOnlyDictionary<string, int> StatModifiers,
    int                              ChoiceModifier,
    IReadOnlyDictionary<string, int> SavingThrowBonuses,
    IReadOnlyDictionary<string, int> CombatBonusVs
)
{
    /// <summary>
    /// Applies this race's stat modifiers to a rolled StatBlock, then clamps to 3–18.
    /// If ChoiceModifier > 0, <paramref name="chosenStat"/> names the stat to boost
    /// ("Str", "Dex", "Con", or "Wis"). Ignored for races with no choice.
    /// </summary>
    public StatBlock Apply(StatBlock rolled, string? chosenStat = null)
    {
        int str = rolled.Str + Get("Str");
        int dex = rolled.Dex + Get("Dex");
        int con = rolled.Con + Get("Con");
        int wis = rolled.Wis + Get("Wis");

        if (ChoiceModifier != 0 && chosenStat != null)
        {
            switch (chosenStat.ToLowerInvariant())
            {
                case "str": str += ChoiceModifier; break;
                case "dex": dex += ChoiceModifier; break;
                case "con": con += ChoiceModifier; break;
                case "wis": wis += ChoiceModifier; break;
            }
        }

        return new StatBlock(str, dex, con, wis).Clamped();
    }

    private int Get(string key) =>
        StatModifiers.TryGetValue(key, out int v) ? v : 0;
}
