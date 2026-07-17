namespace MankersKingdoms.Shared;

/// <summary>
/// Immutable definition of a monster type, loaded from MonsterRegistry.
/// Position and velocity are server-authoritative; clients receive position pushes.
///
/// AttackBonus / TargetNumber:
///   For beasts without gear (Wolf): flat designer-authored values per combat.md §6.1.
///   For gear-bearing humanoids (Goblin, Bandit, Bandit Archer): also flat-authored in
///   Phase 4.8 for simplicity; the live stat+armor formula from combat.md §6.3 applies
///   once Phase 6 wires per-entity equipped gear.
///
/// DamageDice / DamageType: standard notation (e.g. "1d6"). Rolled by CombatResolver
///   on a successful melee hit. Ranged monsters use their weapon's DamageDice.
///
/// LootTable: item IDs eligible to drop on death. NestSystem uses seeded random
///   selection per ARCHITECTURE.md §6.
/// </summary>
public sealed record MonsterData(
    string   Id,
    string   DisplayNameKey,
    float    MaxHp,
    int      AttackBonus,
    int      TargetNumber,
    string   DamageDice,
    string   DamageType,
    float    MoveSpeed,
    float    AggroRange,
    float    AttackRange,
    float    AttackCooldown,
    bool     IsRanged          = false,
    string?  RangedWeaponId    = null,
    string[] LootTable         = null!,   // initialised to empty array by MonsterRegistry

    /// <summary>
    /// Hit dice used to compute MaxHp for humanoids (MonsterRegistry.ComputeHp).
    /// Beasts use flat MaxHp and leave these at 0.
    /// </summary>
    int      HitDiceCount      = 0,
    int      HitDieSize        = 8,
    int?     ConstitutionScore = null
);
