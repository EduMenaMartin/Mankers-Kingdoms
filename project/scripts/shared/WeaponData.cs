namespace MankersKingdoms.Shared;

/// <summary>
/// Immutable stats for one weapon type.
/// Pure C# — no Godot dependency, testable in xUnit.
///
/// DamageDice: standard notation, e.g. "1d8", "2d6", "1d4+1".
///   Empty string for weapons with no damage (e.g. Net).
///   Phase 4.8 (CombatResolver) resolves this to an actual roll.
/// DamageType: "slashing", "piercing", "bludgeoning", or "special".
///   Unused by Phase A's flat crit/fumble tables; authored now so Phase B's
///   weapon-type-specific tables require no schema migration (docs/gdd/combat.md §5.5).
/// IsRanged: true for bows and crossbows; false for melee weapons.
/// ProjectileSpeed: metres/second for ranged weapons; 0 for melee.
/// AmmoItemId: item consumed per shot (e.g. "item.arrow"); null for melee/no ammo.
///
/// See docs/gdd/equipment.md §3 for the full catalog and §6 for the data model.
/// Shield is an armor item (ArmorRegistry, "item.armor.shield"), not a weapon.
/// </summary>
public sealed record WeaponData(
    string  Id,
    string  DisplayNameKey,
    string  DamageDice,
    string  DamageType,
    float   Range,
    float   SwingCooldown,
    bool    IsRanged        = false,
    float   ProjectileSpeed = 0f,
    string? AmmoItemId      = null
);
