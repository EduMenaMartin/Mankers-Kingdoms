using System.Collections.Generic;

namespace MankersKingdoms.Shared;

/// <summary>
/// Pure-C# faction allegiance service. No Godot dependency — fully testable in xUnit.
///
/// Two-layer relationship model (docs/gdd/factions.md §2.2):
///   Layer 1 — type-level defaults, derived from FactionType pair.
///   Layer 2 — instance-level overrides for specific faction-id pairs.
///
/// Faction IDs are stable strings (e.g. "faction.nest.3", "faction.player.settlement").
/// Relationship lookup order: same ID → Allied; Layer 2 override → that value;
/// Layer 1 type-default → that value.
///
/// §4 hard rule: player_settlement factions CANNOT be set Hostile to each other.
/// TrySetOverride enforces this in code, not just as a data default.
///
/// All players share "faction.player.settlement" in v1 (single-settlement slice).
/// </summary>
public static class FactionService
{
    /// <summary>All players belong to this faction in the vertical slice.</summary>
    public const string PLAYER_FACTION_ID = "faction.player.settlement";

    // ── Layer 1 — type-level default table ────────────────────────────────────
    // Implements docs/gdd/factions.md §2.2 table exactly.
    // Key: (typeA, typeB) where typeA <= typeB to avoid duplicate entries.

    private static FactionRelationship TypeDefault(FactionType a, FactionType b)
    {
        // Normalise order so we only need one entry per pair.
        if (a > b) (a, b) = (b, a);

        return (a, b) switch
        {
            (FactionType.MonsterNest,       FactionType.MonsterNest)       => FactionRelationship.Hostile,
            (FactionType.MonsterNest,       FactionType.Village)           => FactionRelationship.Hostile,
            (FactionType.MonsterNest,       FactionType.PlayerSettlement)  => FactionRelationship.Hostile,
            (FactionType.Village,           FactionType.Village)           => FactionRelationship.Neutral,
            (FactionType.Village,           FactionType.PlayerSettlement)  => FactionRelationship.Neutral,
            (FactionType.PlayerSettlement,  FactionType.PlayerSettlement)  => FactionRelationship.Allied,
            _ => FactionRelationship.Neutral,
        };
    }

    // ── State ─────────────────────────────────────────────────────────────────

    private static readonly Dictionary<string, FactionType> _factions  = new();
    // Override key: canonical string of two sorted faction IDs joined by '|'
    private static readonly Dictionary<string, FactionRelationship> _overrides = new();

    // ── Registration ──────────────────────────────────────────────────────────

    /// <summary>
    /// Registers a faction instance. Idempotent — re-registering the same id
    /// with the same type is a no-op; re-registering with a different type
    /// overwrites (supports re-use after world reset).
    /// </summary>
    public static void RegisterFaction(string factionId, FactionType type)
    {
        _factions[factionId] = type;
    }

    // ── Relationship query ────────────────────────────────────────────────────

    /// <summary>
    /// Returns the relationship between two faction instances.
    /// Same faction ID → Allied (a faction never fights itself).
    /// Unknown faction IDs fall back to Neutral — fail-safe, not fail-hostile.
    /// </summary>
    public static FactionRelationship GetRelationship(string factionIdA, string factionIdB)
    {
        if (factionIdA == factionIdB) return FactionRelationship.Allied;

        // Layer 2 — instance override.
        var key = OverrideKey(factionIdA, factionIdB);
        if (_overrides.TryGetValue(key, out var overrideRel)) return overrideRel;

        // Layer 1 — type default.
        if (_factions.TryGetValue(factionIdA, out var typeA) &&
            _factions.TryGetValue(factionIdB, out var typeB))
            return TypeDefault(typeA, typeB);

        // Unknown faction — neutral fallback.
        return FactionRelationship.Neutral;
    }

    /// <summary>Convenience: true if the two factions are in a Hostile relationship.</summary>
    public static bool IsHostile(string factionIdA, string factionIdB) =>
        GetRelationship(factionIdA, factionIdB) == FactionRelationship.Hostile;

    // ── Instance overrides (Layer 2) ──────────────────────────────────────────

    /// <summary>
    /// Sets an instance-level relationship override between two specific factions.
    ///
    /// §4 hard rule: returns false and does NOT apply the override if both factions
    /// are PlayerSettlement type and the requested relationship is Hostile.
    /// This enforces no-PvP in code, not just as a data default (ADR-0024, PRD.md §4.2).
    /// </summary>
    public static bool TrySetOverride(string factionIdA, string factionIdB,
                                      FactionRelationship relationship)
    {
        // §4 hard rule: player settlements are never Hostile to each other.
        if (relationship == FactionRelationship.Hostile
            && _factions.TryGetValue(factionIdA, out var typeA)
            && _factions.TryGetValue(factionIdB, out var typeB)
            && typeA == FactionType.PlayerSettlement
            && typeB == FactionType.PlayerSettlement)
        {
            return false;
        }

        _overrides[OverrideKey(factionIdA, factionIdB)] = relationship;
        return true;
    }

    // ── Test helpers ──────────────────────────────────────────────────────────

    /// <summary>Clears all registered factions and overrides. For test isolation only.</summary>
    public static void Reset()
    {
        _factions.Clear();
        _overrides.Clear();
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private static string OverrideKey(string a, string b) =>
        string.Compare(a, b, System.StringComparison.Ordinal) <= 0
            ? $"{a}|{b}"
            : $"{b}|{a}";
}
