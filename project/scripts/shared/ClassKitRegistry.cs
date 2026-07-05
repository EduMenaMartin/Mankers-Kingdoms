using System.Collections.Generic;

namespace MankersKingdoms.Shared;

/// <summary>
/// Static registry of the two vertical-slice class kits.
/// Hardcoded for v1; full class list (5-7 classes) is a post-slice expansion.
///
/// Fighter: sword-and-board melee archetype.
///   Longsword (1d8 slashing, 1.8 m range) + Shield (+2 TN block bonus).
///   Attack bonus: Strength. Target Number raised by Shield when blocking.
///
/// Ranger: mobile ranged archetype.
///   Shortbow (1d6 piercing, 30 m range) + 20 arrows to open with.
///   Attack bonus: Dexterity. Keeps distance, fires from safety.
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
            StartingItems:
            [
                new ClassKitItem("item.weapon.longsword", 1),
                new ClassKitItem("item.armor.shield",     1),
            ]
        ),
        new ClassKitData(
            ClassId:        "class.ranger",
            DisplayNameKey: "class.ranger.name",
            StartingItems:
            [
                new ClassKitItem("item.weapon.shortbow", 1),
                new ClassKitItem("item.arrow",           20),
            ]
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
