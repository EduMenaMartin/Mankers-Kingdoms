namespace MankersKingdoms.Shared;

/// <summary>
/// Immutable data for a single berry bush instance on the map.
/// Produced by BushGenerator and consumed by BushSystem.
/// </summary>
public sealed record BushData(int Index, float WorldX, float WorldZ, float WorldY);
