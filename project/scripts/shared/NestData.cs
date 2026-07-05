namespace MankersKingdoms.Shared;

/// <summary>
/// Describes a monster nest: the types of monsters it spawns per wave, its world position,
/// the delay before it respawns after all monsters are killed, and its unique faction ID.
///
/// MonsterTypeIds lists one entry per monster spawned per wave; duplicates are allowed
/// (e.g. a bandit camp: ["monster.bandit", "monster.bandit", "monster.bandit_archer"]).
///
/// FactionId is assigned by NestSystem at world generation time and registered with
/// FactionService. Format: "faction.nest.{Id}". See docs/gdd/factions.md §2.1.
/// </summary>
public sealed record NestData(
    int      Id,
    string[] MonsterTypeIds,
    float    WorldX,
    float    WorldZ,
    float    RespawnDelaySec,
    string   FactionId = ""
);
