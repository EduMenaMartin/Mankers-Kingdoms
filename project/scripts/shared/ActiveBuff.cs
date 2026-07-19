namespace MankersKingdoms.Shared;

/// <summary>
/// An individual buff or debuff that is currently active on an entity.
/// Created by BuffSystem.AddBuff; stored per-entity until ExpiresAt passes.
///
/// ExpiresAt is the server elapsed-time value (seconds since scene load) at which this
/// buff expires.  BuffCalculator compares it against the current elapsed time to filter
/// expired entries without a removal pass.
/// </summary>
public sealed record ActiveBuff(
    BuffStat      Stat,
    float         Amount,
    BuffAmountType AmountType,
    double        ExpiresAt);
