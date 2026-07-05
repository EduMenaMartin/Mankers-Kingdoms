namespace MankersKingdoms.Shared;

/// <summary>
/// Relationship between two factions, used to gate AI targeting and
/// projectile damage. See docs/gdd/factions.md §2.2 and ADR-0024.
///
/// Allied  — same side; never targeted, no friendly fire.
/// Neutral — not enemies; no automatic aggro. Player-attack gating TBD (§10.1).
/// Hostile — valid attack target; AI will aggro on sight.
/// </summary>
public enum FactionRelationship
{
    Allied,
    Neutral,
    Hostile,
}
