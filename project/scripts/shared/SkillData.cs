namespace MankersKingdoms.Shared;

/// <summary>
/// Immutable definition of one skill.
/// Pure C# — no Godot dependency, testable in xUnit.
///
/// XpPerAction:    XP awarded each time a relevant action succeeds (e.g., melee hit).
/// XpPerLevel:     XP needed to advance one level (flat formula — same cost every level).
/// GoverningStats: stat IDs whose cap applies ("str", "dex", "con", "wis").
///                 Level is capped at the minimum SkillCap across governing stats.
///                 Use max(Str,Con) for Athletics by listing both — caller takes min cap.
/// ToolTiers:      ordered array of (MinLevel, GrantedItemId) unlocks.
///
/// Level formula: effectiveLevel = min(statCap, rawXp/XpPerLevel + classBump)
///   rawXp starts at 0; classBump is applied once at class selection.
///
/// See ADR-0019, docs/gdd/skills.md.
/// </summary>
public sealed record SkillData(
    string         Id,
    string         DisplayNameKey,
    string[]       GoverningStats,
    int            XpPerAction,
    int            XpPerLevel,
    ToolTierData[] ToolTiers)
{
    /// <summary>
    /// Returns the effective stat cap for this skill given the player's stats.
    /// GoverningStats list one stat normally. Athletics uses ["str","con"] and the
    /// cap is the HIGHER of the two (representing the player using their best physical stat).
    /// </summary>
    public int GetCap(StatBlock stats)
    {
        int best = 0;
        foreach (var s in GoverningStats)
        {
            int stat = s switch
            {
                "str" => stats.Str,
                "dex" => stats.Dex,
                "con" => stats.Con,
                "wis" => stats.Wis,
                _     => 10
            };
            int cap = StatBlock.SkillCap(stat);
            if (cap > best) best = cap;
        }
        return best;
    }
}
