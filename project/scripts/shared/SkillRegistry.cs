namespace MankersKingdoms.Shared;

/// <summary>
/// Hardcoded registry of all skills for the vertical slice (M5).
/// Authoring note: move to data/base/skills.json when the content loader exists.
///
/// Six skills for M5:
///   skill.melee       — melee combat (governs: Str)
///   skill.ranged      — ranged combat (governs: Dex)
///   skill.athletics   — physical movement/stamina (governs: max(Str,Con))
///   skill.woodcutting — chopping trees (governs: Str)
///   skill.foraging    — harvesting bushes (governs: Wis)
///   skill.cooking     — cooking food at fire (governs: Wis)
///
/// XpPerLevel = 5, XpPerAction = 1: 75 actions = level 15 (M5 demo gate).
/// Tool tiers: Woodcutting 15 → bronze_axe; Foraging 15 → sickle; Cooking 10 → stew_pot.
/// </summary>
public static class SkillRegistry
{
    private const int XP_PER_ACTION = 1;
    private const int XP_PER_LEVEL  = 5;

    private static readonly SkillData[] _all =
    {
        new(
            Id:             "skill.melee",
            DisplayNameKey: "skill.melee.name",
            GoverningStats: new[] { "str" },
            XpPerAction:    XP_PER_ACTION,
            XpPerLevel:     XP_PER_LEVEL,
            ToolTiers:      System.Array.Empty<ToolTierData>()
        ),
        new(
            Id:             "skill.ranged",
            DisplayNameKey: "skill.ranged.name",
            GoverningStats: new[] { "dex" },
            XpPerAction:    XP_PER_ACTION,
            XpPerLevel:     XP_PER_LEVEL,
            ToolTiers:      System.Array.Empty<ToolTierData>()
        ),
        new(
            Id:             "skill.athletics",
            DisplayNameKey: "skill.athletics.name",
            GoverningStats: new[] { "str", "con" },
            XpPerAction:    XP_PER_ACTION,
            XpPerLevel:     XP_PER_LEVEL,
            ToolTiers:      System.Array.Empty<ToolTierData>()
        ),
        new(
            Id:             "skill.woodcutting",
            DisplayNameKey: "skill.woodcutting.name",
            GoverningStats: new[] { "str" },
            XpPerAction:    XP_PER_ACTION,
            XpPerLevel:     XP_PER_LEVEL,
            ToolTiers:      new[]
            {
                new ToolTierData(MinLevel: 15, GrantedItemId: "item.tool.bronze_axe")
            }
        ),
        new(
            Id:             "skill.foraging",
            DisplayNameKey: "skill.foraging.name",
            GoverningStats: new[] { "wis" },
            XpPerAction:    XP_PER_ACTION,
            XpPerLevel:     XP_PER_LEVEL,
            ToolTiers:      new[]
            {
                new ToolTierData(MinLevel: 15, GrantedItemId: "item.tool.sickle")
            }
        ),
        new(
            Id:             "skill.cooking",
            DisplayNameKey: "skill.cooking.name",
            GoverningStats: new[] { "wis" },
            XpPerAction:    XP_PER_ACTION,
            XpPerLevel:     XP_PER_LEVEL,
            ToolTiers:      new[]
            {
                new ToolTierData(MinLevel: 10, GrantedItemId: "item.tool.stew_pot")
            }
        ),
    };

    public static System.Collections.Generic.IReadOnlyList<SkillData> All => _all;

    /// <summary>Returns the skill with the given ID, or null if not found.</summary>
    public static SkillData? Find(string skillId)
    {
        foreach (var s in _all)
            if (s.Id == skillId) return s;
        return null;
    }
}
