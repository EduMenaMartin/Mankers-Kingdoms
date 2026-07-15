using System.Collections.Generic;
using System.Linq;

namespace MankersKingdoms.Shared;

/// <summary>
/// Static registry of all buildable structure types for the vertical slice.
/// Hardcoded for M3; a JSON-driven ContentLoader replaces this in M5.
/// Width/Depth are the ground footprint in world units. Height is the total height.
/// </summary>
public static class BuildingRegistry
{
    public static readonly BuildingData Shelter = new(
        Id:             "building.shelter",
        DisplayNameKey: "building.shelter.name",
        ScenePath:      "res://scenes/Shelter.tscn",
        Cost:           new Dictionary<string, int> { ["resource.wood"] = 10 },
        Width: 4f, Height: 3f, Depth: 4f
    );

    public static readonly BuildingData StorageChest = new(
        Id:             "building.storage_chest",
        DisplayNameKey: "building.storage_chest.name",
        ScenePath:      "res://scenes/StorageChest.tscn",
        Cost:           new Dictionary<string, int> { ["resource.wood"] = 6 },
        Width: 2f, Height: 1.5f, Depth: 2f
    );

    public static readonly BuildingData Workbench = new(
        Id:             "building.workbench",
        DisplayNameKey: "building.workbench.name",
        ScenePath:      "res://scenes/Workbench.tscn",
        Cost:           new Dictionary<string, int> { ["resource.wood"] = 8 },
        Width: 3f, Height: 1.5f, Depth: 2f
    );

    public static readonly BuildingData CookingFire = new(
        Id:             "building.cooking_fire",
        DisplayNameKey: "building.cooking_fire.name",
        ScenePath:      "res://scenes/CookingFire.tscn",
        Cost:           new Dictionary<string, int> { ["resource.wood"] = 4 },
        Width: 2f, Height: 1f, Depth: 2f
    );

    public static readonly BuildingData WoodcuttersPost = new(
        Id:             "building.woodcutters_post",
        DisplayNameKey: "building.woodcutters_post.name",
        ScenePath:      "res://scenes/WoodcuttersPost.tscn",
        Cost:           new Dictionary<string, int> { ["resource.wood"] = 15 },
        Width: 4f, Height: 3f, Depth: 4f
    );

    public static readonly BuildingData StockpileDrop = new(
        Id:             "building.stockpile",
        DisplayNameKey: "building.stockpile.name",
        ScenePath:      "res://scenes/Stockpile.tscn",
        Cost:           new Dictionary<string, int> { ["resource.wood"] = 8 },
        Width: 3f, Height: 1.5f, Depth: 3f
    );

    /// <summary>
    /// Presence-gated: requires a Ranger (player or NPC forager archetype) in the settlement.
    /// When dormant (Ranger left) — building stands but crafting yields nothing until Ranger returns.
    /// See VERTICAL_SLICE.md §3.5 and docs/gdd/settlements.md for the one-gated-example rationale.
    /// </summary>
    public static readonly BuildingData HerbalistsHut = new(
        Id:             "building.herbalists_hut",
        DisplayNameKey: "building.herbalists_hut.name",
        ScenePath:      "res://scenes/HerbalistsHut.tscn",
        Cost:           new Dictionary<string, int> { ["resource.wood"] = 20 },
        Width: 4f, Height: 3f, Depth: 4f
    );

    /// <summary>
    /// Placed in a line to form a perimeter wall. One segment, 2 m wide.
    /// Scene is an editor task — two vertical planks and a horizontal rail.
    /// </summary>
    public static readonly BuildingData WoodenWall = new(
        Id:             "building.wooden_wall",
        DisplayNameKey: "building.wooden_wall.name",
        ScenePath:      "res://scenes/WoodenWall.tscn",
        Cost:           new Dictionary<string, int> { ["resource.wood"] = 5 },
        Width: 2f, Height: 3f, Depth: 0.4f
    );

    /// <summary>
    /// Hinged gate that fits a 2 m wall gap. Allows NPCs and founders to pass.
    /// Scene is an editor task — same height as WoodenWall.
    /// </summary>
    public static readonly BuildingData WoodenGate = new(
        Id:             "building.wooden_gate",
        DisplayNameKey: "building.wooden_gate.name",
        ScenePath:      "res://scenes/WoodenGate.tscn",
        Cost:           new Dictionary<string, int> { ["resource.wood"] = 10 },
        Width: 2f, Height: 3f, Depth: 0.4f
    );

    public static readonly IReadOnlyList<BuildingData> All =
        new[] { Shelter, StorageChest, Workbench, CookingFire, WoodcuttersPost, StockpileDrop, HerbalistsHut, WoodenWall, WoodenGate };

    public static BuildingData? Find(string id) =>
        All.FirstOrDefault(b => b.Id == id);
}
