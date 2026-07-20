using System.Collections.Generic;

namespace MankersKingdoms.Shared;

/// <summary>
/// Static registry of the two vertical-slice class kits.
/// Hardcoded for v1; full class list (5-7 classes) is a post-slice expansion.
///
/// Fighter: sword-and-board melee archetype.
///   Longsword (1d8 slashing, 1.8 m range) + Shield (+2 TN block bonus).
///   Skill bumps: Melee +5, Athletics +3 (docs/gdd/character-creation.md §4).
///
/// Ranger: mobile ranged archetype.
///   Shortbow (1d6 piercing, 30 m range) + 20 arrows to open with.
///   Skill bumps: Ranged +5, Foraging +3 (docs/gdd/character-creation.md §4).
///
/// See docs/gdd/character-creation.md §4 and VERTICAL_SLICE.md §3.5.
/// </summary>
public static class ClassKitRegistry
{
    public static readonly IReadOnlyList<ClassKitData> All = new[]
    {
        new ClassKitData(
            ClassId:        "class.fighter",
            DisplayNameKey: "class.fighter.name",
            SkillBumps:
            [
                new ClassSkillBump("skill.melee",     5),
                new ClassSkillBump("skill.athletics", 3),
            ],
            StartingItems:
            [
                new ClassKitItem("item.weapon.longsword", 1),
                new ClassKitItem("item.armor.shield",     1),
            ],
            HitDiceCount:       4,   // 4d8 + ConMod×4 at creation; avg Con 10 → ~18 HP
            HitDieSize:         8,
            ActiveBlockBonus:   6,   // §17.1: Fighter block bonus (+2 over standard +4)
            RangedCritThreshold: 24  // standard; Fighter has no ranged crit trait
        ),
        new ClassKitData(
            ClassId:        "class.ranger",
            DisplayNameKey: "class.ranger.name",
            SkillBumps:
            [
                new ClassSkillBump("skill.ranged",   5),
                new ClassSkillBump("skill.foraging", 3),
            ],
            StartingItems:
            [
                new ClassKitItem("item.weapon.shortbow", 1),
                new ClassKitItem("item.arrow",           20),
            ],
            HitDiceCount:       3,   // 3d8 + ConMod×3 at creation; avg Con 10 → ~13.5 HP
            HitDieSize:         8,
            ActiveBlockBonus:   4,   // standard; Ranger has no block trait
            RangedCritThreshold: 22  // §17.2: Ranger crit threshold (vs standard 24)
        ),
    };

    private static readonly Dictionary<string, ClassKitData> _byId = new();

    static ClassKitRegistry()
    {
        foreach (var kit in All)
            _byId[kit.ClassId] = kit;
    }

    /// <summary>Returns the ClassKitData for the given classId, or null if not found.</summary>
    public static ClassKitData? Find(string classId) =>
        _byId.TryGetValue(classId, out var kit) ? kit : null;
}
