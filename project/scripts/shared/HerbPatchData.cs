namespace MankersKingdoms.Shared;

/// <summary>
/// Immutable data for a single herb patch instance on the map.
/// Produced by HerbGenerator and consumed by HerbSystem.
/// </summary>
public sealed record HerbPatchData(int Index, float WorldX, float WorldZ, float WorldY);
