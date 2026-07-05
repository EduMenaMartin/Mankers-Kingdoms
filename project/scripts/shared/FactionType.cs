namespace MankersKingdoms.Shared;

/// <summary>
/// Category of a faction instance. Determines which row/column of the
/// Layer 1 default relationship table (FactionService) applies.
///
/// monster_nest — wolves, goblins, bandits spawned from a nest/camp.
/// village      — procedural NPC settlement; approachable, recruitable.
/// player_settlement — any settlement founded via Kingdom Marker.
///
/// See docs/gdd/factions.md §2.2 and ADR-0024.
/// </summary>
public enum FactionType
{
    MonsterNest,
    Village,
    PlayerSettlement,
}
