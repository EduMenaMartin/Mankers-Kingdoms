using System.Collections.Generic;

namespace MankersKingdoms.Shared;

/// <summary>
/// Immutable definition for a buildable structure type.
/// Pure C# — no Godot dependency so it can be tested in xUnit.
/// </summary>
public sealed record BuildingData(
    string                          Id,
    string                          DisplayNameKey,
    string                          ScenePath,
    IReadOnlyDictionary<string,int> Cost,
    float                           Width,
    float                           Height,
    float                           Depth
);
