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
    float                           Depth,
    /// <summary>
    /// Optional presence gate. If non-null, this building can only be placed when the
    /// specified class (e.g. "class.ranger") is present in the settlement — either as
    /// the founder's own class or as an assigned NPC of the matching archetype.
    /// Null means no gate (any founder can build it).
    /// Checked server-side in SettlementSystem.RequestPlaceBuilding; reflected in BuildMenu UI.
    /// </summary>
    string?                         RequiresPresence = null
);
