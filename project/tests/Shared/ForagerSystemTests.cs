using System.Collections.Generic;
using MankersKingdoms.Shared;
using Xunit;

namespace MankersKingdoms.Tests.Shared;

/// <summary>
/// Tests for the shared-side preconditions of the forager NPC job loop.
/// The VillageSystem loop itself runs inside a Godot node and cannot be unit-tested
/// without the Godot runtime; these tests verify the data invariants it depends on.
/// </summary>
public class ForagerSystemTests
{
    // ── Building registry ─────────────────────────────────────────────────────

    [Fact]
    public void HerbalistsHut_IsInBuildingRegistry()
    {
        var hut = BuildingRegistry.Find("building.herbalists_hut");
        Assert.NotNull(hut);
    }

    [Fact]
    public void HerbalistsHut_HasExpectedFootprint()
    {
        var hut = BuildingRegistry.Find("building.herbalists_hut");
        Assert.Equal(4f, hut!.Width);
        Assert.Equal(3f, hut.Height);
        Assert.Equal(4f, hut.Depth);
    }

    [Fact]
    public void HerbalistsHut_Cost_IsWood20()
    {
        var hut = BuildingRegistry.Find("building.herbalists_hut");
        Assert.True(hut!.Cost.TryGetValue("resource.wood", out int cost));
        Assert.Equal(20, cost);
    }

    // ── Villager archetype routing ────────────────────────────────────────────

    [Fact]
    public void VillagerData_WisHighest_GivesForagerArchetype()
    {
        // Wis-highest NPC → archetype.forager (maps to Herbalist's Hut job).
        var stats = new StatBlock(Str: 8, Dex: 8, Con: 8, Wis: 18);
        var data  = new VillagerData("v1", "Test", stats, 0f, 0f);
        Assert.Equal("archetype.forager", data.ArchetypeTag);
    }

    [Fact]
    public void VillagerData_StrHighest_GivesWoodcutterArchetype()
    {
        // Str-highest NPC → archetype.woodcutter (maps to Woodcutter's Post job).
        var stats = new StatBlock(Str: 18, Dex: 8, Con: 8, Wis: 8);
        var data  = new VillagerData("v1", "Test", stats, 0f, 0f);
        Assert.Equal("archetype.woodcutter", data.ArchetypeTag);
    }

    [Fact]
    public void VillagerData_WisStrTie_StrWins_GivesWoodcutterArchetype()
    {
        // Tie-break priority: Str > Con > Dex > Wis — so Str beats Wis on a tie.
        var stats = new StatBlock(Str: 15, Dex: 10, Con: 10, Wis: 15);
        var data  = new VillagerData("v1", "Test", stats, 0f, 0f);
        Assert.Equal("archetype.woodcutter", data.ArchetypeTag);
    }

    [Fact]
    public void VillagerData_WisOnly_GivesForagerArchetype()
    {
        // Pure Wis-dominant (no ties) → forager.
        var stats = new StatBlock(Str: 8, Dex: 9, Con: 10, Wis: 16);
        var data  = new VillagerData("v1", "Test", stats, 0f, 0f);
        Assert.Equal("archetype.forager", data.ArchetypeTag);
    }

    // ── Station name routing ──────────────────────────────────────────────────

    [Fact]
    public void HerbalistsHut_ScenePath_MatchesRegistry()
    {
        // TickJobs routes by checking stationNodeName.StartsWith("building.herbalists_hut").
        // Station node names are "$buildingId_$x_$z" (see SettlementSystem.SpawnBuilding).
        // Verify the building ID prefix matches what TickJobs checks.
        var hut = BuildingRegistry.Find("building.herbalists_hut");
        const string exampleNodeName = "building.herbalists_hut_10_20";
        Assert.StartsWith(hut!.Id, exampleNodeName, System.StringComparison.Ordinal);
    }
}
