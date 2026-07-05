using MankersKingdoms.Shared;
using Xunit;

namespace MankersKingdoms.Tests.Shared;

/// <summary>
/// Tests for FactionService — two-layer relationship model (docs/gdd/factions.md §2.2).
/// Each test calls FactionService.Reset() so registrations don't bleed between tests.
/// </summary>
public class FactionServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void Register(string id, FactionType type)
        => FactionService.RegisterFaction(id, type);

    // ── Same-faction rule ─────────────────────────────────────────────────────

    [Fact]
    public void SameFactionId_IsAlways_Allied()
    {
        FactionService.Reset();
        Register("faction.nest.1", FactionType.MonsterNest);
        Assert.Equal(FactionRelationship.Allied,
            FactionService.GetRelationship("faction.nest.1", "faction.nest.1"));
    }

    // ── Layer 1 — type-level defaults ─────────────────────────────────────────

    [Fact]
    public void MonsterNest_vs_MonsterNest_DifferentInstance_IsHostile()
    {
        FactionService.Reset();
        Register("faction.nest.1", FactionType.MonsterNest);
        Register("faction.nest.2", FactionType.MonsterNest);
        Assert.Equal(FactionRelationship.Hostile,
            FactionService.GetRelationship("faction.nest.1", "faction.nest.2"));
    }

    [Fact]
    public void MonsterNest_vs_PlayerSettlement_IsHostile()
    {
        FactionService.Reset();
        Register("faction.nest.1", FactionType.MonsterNest);
        Register(FactionService.PLAYER_FACTION_ID, FactionType.PlayerSettlement);
        Assert.True(FactionService.IsHostile("faction.nest.1", FactionService.PLAYER_FACTION_ID));
    }

    [Fact]
    public void MonsterNest_vs_Village_IsHostile()
    {
        FactionService.Reset();
        Register("faction.nest.1", FactionType.MonsterNest);
        Register("faction.village.1", FactionType.Village);
        Assert.Equal(FactionRelationship.Hostile,
            FactionService.GetRelationship("faction.nest.1", "faction.village.1"));
    }

    [Fact]
    public void Village_vs_Village_IsNeutral()
    {
        FactionService.Reset();
        Register("faction.village.1", FactionType.Village);
        Register("faction.village.2", FactionType.Village);
        Assert.Equal(FactionRelationship.Neutral,
            FactionService.GetRelationship("faction.village.1", "faction.village.2"));
    }

    [Fact]
    public void Village_vs_PlayerSettlement_IsNeutral()
    {
        FactionService.Reset();
        Register("faction.village.1", FactionType.Village);
        Register(FactionService.PLAYER_FACTION_ID, FactionType.PlayerSettlement);
        Assert.Equal(FactionRelationship.Neutral,
            FactionService.GetRelationship("faction.village.1", FactionService.PLAYER_FACTION_ID));
    }

    [Fact]
    public void PlayerSettlement_vs_PlayerSettlement_IsAllied()
    {
        FactionService.Reset();
        Register("faction.player.a", FactionType.PlayerSettlement);
        Register("faction.player.b", FactionType.PlayerSettlement);
        Assert.Equal(FactionRelationship.Allied,
            FactionService.GetRelationship("faction.player.a", "faction.player.b"));
    }

    [Fact]
    public void Relationship_IsSymmetric()
    {
        // GetRelationship(A, B) must equal GetRelationship(B, A).
        FactionService.Reset();
        Register("faction.nest.1", FactionType.MonsterNest);
        Register(FactionService.PLAYER_FACTION_ID, FactionType.PlayerSettlement);
        Assert.Equal(
            FactionService.GetRelationship("faction.nest.1", FactionService.PLAYER_FACTION_ID),
            FactionService.GetRelationship(FactionService.PLAYER_FACTION_ID, "faction.nest.1"));
    }

    // ── Unknown factions — fail-safe fallback ─────────────────────────────────

    [Fact]
    public void UnknownFaction_FallsBackTo_Neutral()
    {
        FactionService.Reset();
        // Neither faction registered — should not throw, should return Neutral.
        Assert.Equal(FactionRelationship.Neutral,
            FactionService.GetRelationship("faction.unknown.a", "faction.unknown.b"));
    }

    // ── Layer 2 — instance overrides ──────────────────────────────────────────

    [Fact]
    public void Override_TakesPrecedence_OverTypeDefault()
    {
        FactionService.Reset();
        Register("faction.nest.1", FactionType.MonsterNest);
        Register("faction.nest.2", FactionType.MonsterNest);

        // Default is Hostile; override to Allied (narrative alliance).
        bool applied = FactionService.TrySetOverride(
            "faction.nest.1", "faction.nest.2", FactionRelationship.Allied);

        Assert.True(applied);
        Assert.Equal(FactionRelationship.Allied,
            FactionService.GetRelationship("faction.nest.1", "faction.nest.2"));
    }

    [Fact]
    public void Override_IsSymmetric()
    {
        FactionService.Reset();
        Register("faction.nest.1", FactionType.MonsterNest);
        Register("faction.nest.2", FactionType.MonsterNest);
        FactionService.TrySetOverride("faction.nest.1", "faction.nest.2", FactionRelationship.Neutral);

        Assert.Equal(FactionRelationship.Neutral,
            FactionService.GetRelationship("faction.nest.2", "faction.nest.1"));
    }

    // ── §4 hard rule — no PvP ────────────────────────────────────────────────

    [Fact]
    public void PlayerSettlement_vs_PlayerSettlement_Hostile_Override_IsRejected()
    {
        FactionService.Reset();
        Register("faction.player.a", FactionType.PlayerSettlement);
        Register("faction.player.b", FactionType.PlayerSettlement);

        bool applied = FactionService.TrySetOverride(
            "faction.player.a", "faction.player.b", FactionRelationship.Hostile);

        Assert.False(applied);
        // Relationship must remain Allied (type default), not Hostile.
        Assert.Equal(FactionRelationship.Allied,
            FactionService.GetRelationship("faction.player.a", "faction.player.b"));
    }

    [Fact]
    public void PlayerSettlement_vs_PlayerSettlement_Neutral_Override_IsAllowed()
    {
        // Neutral is not PvP — should be accepted even though it downgrades from Allied.
        FactionService.Reset();
        Register("faction.player.a", FactionType.PlayerSettlement);
        Register("faction.player.b", FactionType.PlayerSettlement);

        bool applied = FactionService.TrySetOverride(
            "faction.player.a", "faction.player.b", FactionRelationship.Neutral);

        Assert.True(applied);
        Assert.Equal(FactionRelationship.Neutral,
            FactionService.GetRelationship("faction.player.a", "faction.player.b"));
    }

    // ── IsHostile convenience ─────────────────────────────────────────────────

    [Fact]
    public void IsHostile_ReturnsFalse_ForAllied()
    {
        FactionService.Reset();
        Register("faction.player.a", FactionType.PlayerSettlement);
        Register("faction.player.b", FactionType.PlayerSettlement);
        Assert.False(FactionService.IsHostile("faction.player.a", "faction.player.b"));
    }

    [Fact]
    public void IsHostile_ReturnsFalse_ForNeutral()
    {
        FactionService.Reset();
        Register("faction.village.1", FactionType.Village);
        Register(FactionService.PLAYER_FACTION_ID, FactionType.PlayerSettlement);
        Assert.False(FactionService.IsHostile("faction.village.1", FactionService.PLAYER_FACTION_ID));
    }

    [Fact]
    public void IsHostile_ReturnsTrue_ForHostile()
    {
        FactionService.Reset();
        Register("faction.nest.1", FactionType.MonsterNest);
        Register(FactionService.PLAYER_FACTION_ID, FactionType.PlayerSettlement);
        Assert.True(FactionService.IsHostile("faction.nest.1", FactionService.PLAYER_FACTION_ID));
    }
}
