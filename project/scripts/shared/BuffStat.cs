namespace MankersKingdoms.Shared;

/// <summary>
/// The property that an active buff or debuff modifies.
/// Used as a key when querying effective values from BuffCalculator / BuffSystem.
///
/// Additive buffs: effective = base + sum(amounts).
/// Multiplicative buffs: effective = base * (1 + sum(amounts)).
///   Amounts are always the delta from 1.0 — e.g. 0.5 means "+50%", -0.5 means "-50%".
///   This ensures two +50% buffs combine to +100%, not (1.5×1.5)=+125%.
/// </summary>
public enum BuffStat
{
    // ── Defensive ──────────────────────────────────────────────────────────
    /// <summary>Additive modifier on the target's ArmorValue (e.g. −2 from SunderingHit).</summary>
    ArmorValue,

    /// <summary>
    /// Multiplicative modifier on damage received (e.g. +0.5 from Overextended = 1.5× damage).
    /// Applied in HealthSystem.ApplyDamage.
    /// </summary>
    IncomingDamage,

    // ── Offensive ──────────────────────────────────────────────────────────
    /// <summary>Additive modifier on the entity's own AttackBonus (e.g. −2 from OffBalance).</summary>
    AttackBonus,

    // ── Mobility ───────────────────────────────────────────────────────────
    /// <summary>
    /// Multiplicative modifier on movement speed (e.g. −0.5 from Stumble = 0.5× speed).
    /// Server-side buff is tracked; client sync via RPC is deferred (MoveSpeed TODO).
    /// </summary>
    MoveSpeed,

    // ── Action gates ───────────────────────────────────────────────────────
    /// <summary>
    /// Additive count — any value > 0 means the entity is stunned and cannot attack.
    /// Applied by BuffSystem.IsBuffActive(entity, BuffStat.Stun).
    /// </summary>
    Stun,

    /// <summary>
    /// Additive count — any value > 0 means the entity's weapon hand is empty.
    /// Prevents melee swings; ranged projectiles are not affected (no weapon-hold animation).
    /// </summary>
    Disarm,

    // ── Damage-over-time ───────────────────────────────────────────────────
    /// <summary>
    /// Additive bleed damage per tick (1-second intervals).
    /// Ticked by BuffSystem._Process; multiple BleedingWound stacks sum correctly.
    /// </summary>
    BleedDamage,
}
