namespace MankersKingdoms.Shared;

/// <summary>
/// Resolves a saving throw roll against a difficulty value (combat.md §16).
///
/// Formula: 1d20 + floor(skillLevel / 10) + racial bonus ≥ authored difficulty → success.
///
/// Racial bonuses come from RaceData.SavingThrowBonuses, keyed by category tag (e.g. "poison",
/// "magic", "sleep", "charm"). The caller supplies the category string; this resolver looks up
/// the matching bonus and sums all matching entries (in case a future race has multiple tags
/// for the same category — currently each race tag is distinct).
///
/// See docs/gdd/combat.md §16 and docs/gdd/character-creation.md §12.
/// </summary>
public static class SavingThrowResolver
{
    /// <summary>
    /// Resolves a saving throw.
    /// </summary>
    /// <param name="d20Roll">A pre-rolled 1–20 value (caller provides; aids testability).</param>
    /// <param name="skillLevel">The skill level governing this save (0 if no skill applies).</param>
    /// <param name="race">The race data for the saving character, or null (no racial bonus).</param>
    /// <param name="category">The saving-throw category tag, e.g. "poison", "sleep", "charm".</param>
    /// <param name="difficulty">The authored difficulty number that the total must meet or exceed.</param>
    /// <returns>
    /// <c>success</c>: true if total ≥ difficulty.
    /// <c>roll</c>: the raw d20 value.
    /// <c>racialBonus</c>: the bonus from racial traits (0 if race is null or no matching tag).
    /// <c>total</c>: roll + floor(skillLevel/10) + racialBonus.
    /// </returns>
    public static (bool success, int roll, int racialBonus, int total) Resolve(
        int       d20Roll,
        int       skillLevel,
        RaceData? race,
        string    category,
        int       difficulty)
    {
        int skillBonus  = skillLevel / 10;
        int racialBonus = 0;

        if (race != null)
        {
            // Sum all bonuses whose tag matches the requested category (case-insensitive).
            foreach (var (tag, bonus) in race.SavingThrowBonuses)
            {
                if (string.Equals(tag, category, System.StringComparison.OrdinalIgnoreCase))
                    racialBonus += bonus;
            }
        }

        int total   = d20Roll + skillBonus + racialBonus;
        bool success = total >= difficulty;

        return (success, d20Roll, racialBonus, total);
    }
}
