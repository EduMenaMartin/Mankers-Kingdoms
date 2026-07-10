using System.Collections.Generic;

namespace MankersKingdoms.Shared;

/// <summary>
/// Immutable layout record for the world's single procedural village.
/// Produced by VillageGenerator; held by VillageSystem for the lifetime of the world.
/// VillagerIds is ordered (spawn index = position in list).
/// </summary>
public sealed record VillageData(
    string Id,
    float WorldX,
    float WorldZ,
    IReadOnlyList<string> VillagerIds);
