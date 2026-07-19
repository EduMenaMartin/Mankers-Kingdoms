namespace MankersKingdoms.Shared;

/// <summary>
/// Controls how a buff's Amount is condensed with other buffs of the same stat.
///
/// Additive:       effective = base + sum(all additive amounts for this stat)
/// Multiplicative: effective = base * (1 + sum(all multiplicative amounts for this stat))
///
/// The "sum-then-apply" rule for Multiplicative ensures two +50% buffs give +100%, not +125%.
/// </summary>
public enum BuffAmountType
{
    Additive,
    Multiplicative,
}
