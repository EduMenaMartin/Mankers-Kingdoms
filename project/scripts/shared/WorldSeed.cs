namespace MankersKingdoms.Shared;

/// <summary>
/// Wraps the active world seed and provides a factory for seeded System.Random instances.
/// Every system that needs randomness during world generation calls CreateRandom() once
/// and caches the result — never Random.Shared (ARCHITECTURE.md §7).
/// </summary>
public static class WorldSeed
{
    /// <summary>
    /// Creates a new System.Random seeded from the current GameSession.WorldSeed.
    /// Each caller gets an independent sequence starting from the same seed.
    /// </summary>
    public static System.Random CreateRandom() => new System.Random((int)GameSession.WorldSeed);
}
