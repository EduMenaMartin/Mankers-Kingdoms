using System.Collections.Generic;

namespace MankersKingdoms.Shared;

/// <summary>
/// Pure-C# helper that applies the buff condensation rules over a list of ActiveBuff entries.
/// No Godot dependency — fully unit-testable via xUnit.
///
/// Condensation rules (user-specified correctness requirement):
///   Additive:       effective = base + Σ(amount  for non-expired additive buffs on this stat)
///   Multiplicative: effective = base × (1 + Σ(amount for non-expired multiplicative buffs))
///
/// The Multiplicative rule sums all delta-from-1 values BEFORE applying, so two "+50%"
/// buffs (amount=0.5 each) combine to ×2.0, not ×1.5×1.5=×2.25.
///
/// Called by BuffSystem, which supplies the live buff list and current elapsed time.
/// </summary>
public static class BuffCalculator
{
    /// <summary>
    /// Returns the sum of all non-expired additive buff amounts for the given stat.
    /// Add this directly to the base value: effectiveBase = base + GetAdditiveModifier(...)
    /// </summary>
    public static float GetAdditiveModifier(
        IReadOnlyList<ActiveBuff> buffs,
        BuffStat                  stat,
        double                    currentTime)
    {
        float sum = 0f;
        for (int i = 0; i < buffs.Count; i++)
        {
            var b = buffs[i];
            if (b.Stat == stat && b.AmountType == BuffAmountType.Additive && b.ExpiresAt > currentTime)
                sum += b.Amount;
        }
        return sum;
    }

    /// <summary>
    /// Returns the combined multiplier for all non-expired multiplicative buffs on this stat.
    /// Multiply the base value by the result: effectiveBase = base * GetMultiplicativeModifier(...)
    ///
    /// Formula: 1 + Σ(amount) — amounts are deltas from 1.0.
    ///   Example: two vulnerability buffs each with amount=0.5 → 1+(0.5+0.5) = 2.0 (×2.0 damage).
    ///   Without condensation: 1.5×1.5 = 2.25 (incorrect).
    ///
    /// Returns 1.0 when no multiplicative buffs are active (neutral multiplier).
    /// </summary>
    public static float GetMultiplicativeModifier(
        IReadOnlyList<ActiveBuff> buffs,
        BuffStat                  stat,
        double                    currentTime)
    {
        float sum = 0f;
        for (int i = 0; i < buffs.Count; i++)
        {
            var b = buffs[i];
            if (b.Stat == stat && b.AmountType == BuffAmountType.Multiplicative && b.ExpiresAt > currentTime)
                sum += b.Amount;
        }
        return 1f + sum;
    }

    /// <summary>
    /// Returns true if at least one non-expired buff for the given stat exists.
    /// Used for boolean gates (Stun, Disarm) where any presence blocks an action.
    /// </summary>
    public static bool IsActive(
        IReadOnlyList<ActiveBuff> buffs,
        BuffStat                  stat,
        double                    currentTime)
    {
        for (int i = 0; i < buffs.Count; i++)
        {
            var b = buffs[i];
            if (b.Stat == stat && b.ExpiresAt > currentTime)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Returns the sum of all non-expired additive amounts for BleedDamage.
    /// BuffSystem ticks this every second to know how much damage to apply.
    /// </summary>
    public static float GetBleedDamagePerTick(IReadOnlyList<ActiveBuff> buffs, double currentTime)
        => GetAdditiveModifier(buffs, BuffStat.BleedDamage, currentTime);
}
