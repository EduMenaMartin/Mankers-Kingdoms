namespace MankersKingdoms.Shared;

/// <summary>
/// Randomly-selected bonus effect applied on a critical hit (natural 20, combat.md §5.4 Phase A).
/// Rolled by CombatResolver.RollCritEffect after a confirmed crit.
/// BuffSystem maps each entry to a buff applied to the defender.
/// </summary>
public enum CritEffect
{
    DevastatingBlow = 0,  // double damage only — no extra buff (most common)
    PreciseStrike   = 1,  // + brief stun on target
    BleedingWound   = 2,  // + short bleed DoT on target
    SunderingHit    = 3,  // + brief ArmorValue reduction on target
    StaggeringBlow  = 4,  // + brief movement-speed reduction on target (client sync deferred)
}
