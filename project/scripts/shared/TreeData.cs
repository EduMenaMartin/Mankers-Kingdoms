namespace MankersKingdoms.Shared;

/// <summary>
/// Immutable description of a single tree's placement, generated from seed + config.
/// Uses plain floats for position to stay free of Godot dependencies (testable in xUnit).
/// </summary>
public sealed record TreeData(
    string Id,       // stable string ID e.g. "tree.0", "tree.1" …
    int    GridX,
    int    GridZ,
    float  WorldX,
    float  WorldY,
    float  WorldZ
);
