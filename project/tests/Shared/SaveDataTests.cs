using System.Collections.Generic;
using System.Text.Json;
using MankersKingdoms.Shared;
using Xunit;

namespace MankersKingdoms.Tests.Shared;

/// <summary>
/// Round-trip and schema tests for SaveData (M8).
/// These tests cover the pure-C# serialization layer only.
/// SaveSystem itself (Godot Node) is exercised by the in-engine smoke test.
/// </summary>
public class SaveDataTests
{
    // ── Version field ─────────────────────────────────────────────────────────

    [Fact]
    public void SaveData_DefaultVersion_Is2()
    {
        // v2 added SessionSave.Alignment (character-creation.md §11).
        var data = new SaveData();
        Assert.Equal(2, data.Version);
    }

    // ── JSON round-trip ───────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_WorldSeed_Preserved()
    {
        var data = new SaveData { WorldSeed = 0xDEADBEEF };
        var json = JsonSerializer.Serialize(data);
        var back = JsonSerializer.Deserialize<SaveData>(json)!;
        Assert.Equal(0xDEADBEEFu, back.WorldSeed);
    }

    [Fact]
    public void RoundTrip_Markers_Preserved()
    {
        var data = new SaveData
        {
            Markers =
            [
                new MarkerSave { PeerId = 1L, X = 40.5f, Y = 0.1f, Z = 80.25f }
            ]
        };
        var back = Roundtrip(data);
        Assert.Single(back.Markers);
        Assert.Equal(1L,    back.Markers[0].PeerId);
        Assert.Equal(40.5f, back.Markers[0].X, precision: 3);
        Assert.Equal(0.1f,  back.Markers[0].Y, precision: 3);
        Assert.Equal(80.25f,back.Markers[0].Z, precision: 3);
    }

    [Fact]
    public void RoundTrip_Buildings_Preserved()
    {
        var data = new SaveData
        {
            Buildings =
            [
                new BuildingSave { BuildingId = "building.woodcutters_post", X = 45f, Y = 0f, Z = 85f },
                new BuildingSave { BuildingId = "building.shelter",          X = 50f, Y = 0f, Z = 90f }
            ]
        };
        var back = Roundtrip(data);
        Assert.Equal(2, back.Buildings.Count);
        Assert.Equal("building.woodcutters_post", back.Buildings[0].BuildingId);
        Assert.Equal("building.shelter",          back.Buildings[1].BuildingId);
    }

    [Fact]
    public void RoundTrip_Stockpile_Preserved()
    {
        var data = new SaveData
        {
            Stockpile = new Dictionary<string, int>
            {
                ["resource.wood"] = 12,
                ["item.herb"]     = 3
            }
        };
        var back = Roundtrip(data);
        Assert.Equal(12, back.Stockpile["resource.wood"]);
        Assert.Equal(3,  back.Stockpile["item.herb"]);
    }

    [Fact]
    public void RoundTrip_NpcAssignments_Preserved()
    {
        var data = new SaveData
        {
            NpcAssignments =
            [
                new NpcAssignSave
                {
                    NpcId           = "villager_0",
                    StationNodeName = "building.woodcutters_post_45_85",
                    FounderPeerId   = 1L
                }
            ]
        };
        var back = Roundtrip(data);
        Assert.Single(back.NpcAssignments);
        Assert.Equal("villager_0",                     back.NpcAssignments[0].NpcId);
        Assert.Equal("building.woodcutters_post_45_85",back.NpcAssignments[0].StationNodeName);
        Assert.Equal(1L,                               back.NpcAssignments[0].FounderPeerId);
    }

    [Fact]
    public void RoundTrip_PlayerSave_Items_Preserved()
    {
        var data = new SaveData
        {
            Players =
            [
                new PlayerSave
                {
                    PeerId = 1L,
                    Items  = new Dictionary<string, int> { ["item.bandage"] = 3, ["resource.wood"] = 7 },
                    Hp     = 85.5f,
                    Hunger = 72.3f,
                    Rest   = 45.0f,
                    PosX   = 42f,
                    PosY   = 1.5f,
                    PosZ   = 80f
                }
            ]
        };
        var back = Roundtrip(data);
        Assert.Single(back.Players);
        var ps = back.Players[0];
        Assert.Equal(1L,           ps.PeerId);
        Assert.Equal(3,            ps.Items["item.bandage"]);
        Assert.Equal(7,            ps.Items["resource.wood"]);
        Assert.Equal(85.5f,        ps.Hp,     precision: 3);
        Assert.Equal(72.3f,        ps.Hunger, precision: 3);
        Assert.Equal(45.0f,        ps.Rest,   precision: 3);
        Assert.Equal(42f,          ps.PosX,   precision: 3);
        Assert.Equal(1.5f,         ps.PosY,   precision: 3);
        Assert.Equal(80f,          ps.PosZ,   precision: 3);
    }

    [Fact]
    public void RoundTrip_PlayerSave_HotbarSlots_Preserved()
    {
        var ps = new PlayerSave { PeerId = 1L };
        ps.HotbarSlots[2] = "item.bandage"; // slot 3 (0-indexed)
        ps.HotbarSlots[0] = null;           // slot 1 empty
        var data = new SaveData { Players = [ps] };

        var back = Roundtrip(data);
        Assert.Null(back.Players[0].HotbarSlots[0]);
        Assert.Null(back.Players[0].HotbarSlots[1]);
        Assert.Equal("item.bandage", back.Players[0].HotbarSlots[2]);
    }

    [Fact]
    public void RoundTrip_PlayerSave_SkillXpAndBumps_Preserved()
    {
        var ps = new PlayerSave
        {
            PeerId     = 1L,
            SkillXp    = new Dictionary<string, int> { ["skill.woodcutting"] = 75, ["skill.melee"] = 5 },
            SkillBumps = new Dictionary<string, int> { ["skill.melee"] = 5, ["skill.athletics"] = 3 }
        };
        var data = new SaveData { Players = [ps] };

        var back = Roundtrip(data);
        Assert.Equal(75, back.Players[0].SkillXp["skill.woodcutting"]);
        Assert.Equal(5,  back.Players[0].SkillXp["skill.melee"]);
        Assert.Equal(5,  back.Players[0].SkillBumps["skill.melee"]);
        Assert.Equal(3,  back.Players[0].SkillBumps["skill.athletics"]);
    }

    [Fact]
    public void RoundTrip_EmptySave_DoesNotThrow()
    {
        var data = new SaveData { WorldSeed = 42u };
        var back = Roundtrip(data);
        Assert.Equal(42u, back.WorldSeed);
        Assert.Empty(back.Markers);
        Assert.Empty(back.Buildings);
        Assert.Empty(back.Stockpile);
        Assert.Empty(back.NpcAssignments);
        Assert.Empty(back.Players);
    }

    // ── Schema defaults ───────────────────────────────────────────────────────

    [Fact]
    public void PlayerSave_Defaults_AreFullBars()
    {
        // A PlayerSave with no explicit HP/Hunger/Rest should default to full bars
        // so missing fields in old saves don't kill the player on load.
        var ps = new PlayerSave();
        Assert.Equal(100f, ps.Hp);
        Assert.Equal(100f, ps.Hunger);
        Assert.Equal(100f, ps.Rest);
    }

    [Fact]
    public void PlayerSave_HotbarSlots_DefaultLength_Is9()
    {
        var ps = new PlayerSave();
        Assert.Equal(9, ps.HotbarSlots.Length);
        Assert.All(ps.HotbarSlots, slot => Assert.Null(slot));
    }

    [Fact]
    public void BuildingSave_DefaultBuildingId_IsEmpty()
    {
        var b = new BuildingSave();
        Assert.Equal("", b.BuildingId);
    }

    // ── Felled trees ──────────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_FelledTreeIds_Preserved()
    {
        var data = new SaveData
        {
            FelledTreeIds = ["tree.42", "tree.7", "tree.103"]
        };
        var back = Roundtrip(data);
        Assert.Equal(3, back.FelledTreeIds.Count);
        Assert.Contains("tree.42",  back.FelledTreeIds);
        Assert.Contains("tree.7",   back.FelledTreeIds);
        Assert.Contains("tree.103", back.FelledTreeIds);
    }

    [Fact]
    public void SaveData_FelledTreeIds_DefaultsToEmpty()
    {
        var data = new SaveData();
        Assert.Empty(data.FelledTreeIds);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static SaveData Roundtrip(SaveData data)
    {
        var json = JsonSerializer.Serialize(data);
        return JsonSerializer.Deserialize<SaveData>(json)!;
    }
}
