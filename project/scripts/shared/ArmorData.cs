namespace MankersKingdoms.Shared;

/// <summary>
/// Immutable stats for one armor or shield item.
/// Pure C# — no Godot dependency, testable in xUnit.
///
/// ArmorValue: adds directly to the defender's Target Number (docs/gdd/combat.md §2.2):
///   TargetNumber = 10 + StatMod(Dex, capped by ArmorCategory) + ArmorValue + ShieldBonus.
/// ShieldBonus: non-zero for Shield items only; also added to Target Number.
/// ArmorCategory: drives Dex modifier handling in combat — see ArmorCategory enum.
/// StrRequirement: if wearer Str is below this, movement is penalised (combat.md §11.2).
/// StealthDisadvantage: interacts with Stealth skill while worn (combat.md §11.3).
///
/// See docs/gdd/equipment.md §2 for the full catalog; §6 and combat.md §11.5 for the schema.
/// </summary>
public sealed record ArmorData(
    string        Id,
    string        DisplayNameKey,
    int           ArmorValue,
    ArmorCategory ArmorCategory,
    int           StrRequirement,
    bool          StealthDisadvantage,
    int           ShieldBonus,
    float         Weight
);
