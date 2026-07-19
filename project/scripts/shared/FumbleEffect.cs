namespace MankersKingdoms.Shared;

/// <summary>
/// Randomly-selected complication applied on a critical fumble (natural 1 that would have
/// missed anyway — §5.2 asymmetry rule, combat.md §5.4 Phase A).
/// Rolled by CombatResolver.RollFumbleEffect after a confirmed fumble.
/// BuffSystem maps each entry to a buff applied to the attacker.
/// </summary>
public enum FumbleEffect
{
    OffBalance   = 0,  // brief AccuracyPenalty on self (most common)
    WeaponSlip   = 1,  // brief Disarm on self (quick recovery)
    Overextended = 2,  // brief IncomingDamage vulnerability on self
    Stumble      = 3,  // brief movement-speed reduction on self (client sync deferred)
}
