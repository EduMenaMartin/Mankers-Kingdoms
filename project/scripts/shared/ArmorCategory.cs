namespace MankersKingdoms.Shared;

/// <summary>
/// Governs how much of the Dex modifier applies to a defender's Target Number
/// (docs/gdd/combat.md §11.1).
///
/// Light  → full Dex modifier.
/// Medium → Dex modifier capped at +1 (our house rule; SRD uses +2).
/// Heavy  → no Dex modifier (0 regardless of actual Dexterity).
/// Shield → off-hand item; contributes ShieldBonus only, not covered by this category.
/// </summary>
public enum ArmorCategory { Light, Medium, Heavy, Shield }
