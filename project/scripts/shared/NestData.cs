namespace MankersKingdoms.Shared;

/// <summary>
/// Describes a monster nest: the types of monsters it spawns per wave, its world position,
/// and the delay before it respawns after all monsters are killed.
///
/// MonsterTypeIds lists one entry per monster spawned per wave; duplicates are allowed
/// (e.g. a bandit camp: ["monster.bandit", "monster.bandit", "monster.bandit_archer"]).
/// </summary>
public sealed record NestData(
    int      Id,
    string[] MonsterTypeIds,
    float    WorldX,
    float    WorldZ,
    float    RespawnDelaySec
);
