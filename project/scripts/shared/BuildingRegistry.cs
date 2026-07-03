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

    public static readonly IReadOnlyList<BuildingData> All =
        new[] { Shelter, StorageChest, Workbench, CookingFire };

    public static BuildingData? Find(string id) =>
        All.FirstOrDefault(b => b.Id == id);
}
