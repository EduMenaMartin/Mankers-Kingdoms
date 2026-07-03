namespace MankersKingdoms.Shared;

/// <summary>
/// Parameters controlling tree placement and chopping behaviour.
/// id "tree.oak" is the content string ID (ARCHITECTURE.md: stable string IDs).
/// </summary>
public sealed class TreeConfig
{
    /// <summary>Probability [0,1] that any given tile gets a tree.</summary>
    public float Density       { get; init; } = 0.12f;

    /// <summary>Minimum terrain height a tree can spawn on (avoids water-level depressions).</summary>
    public float MinHeight     { get; init; } = -1f;

    /// <summary>Number of chops needed to fell one tree.</summary>
    public int TreeHp          { get; init; } = 5;

    /// <summary>Units of wood yielded when a tree is felled.</summary>
    public int WoodYield       { get; init; } = 3;

    /// <summary>Woodcutting XP awarded per fell.</summary>
    public int WoodcuttingXp   { get; init; } = 25;

    public static TreeConfig Default { get; } = new();
}
