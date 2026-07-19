using System.Collections.Generic;

namespace MankersKingdoms.Shared;

/// <summary>
/// Character and session data embedded in every save file so the game can be
/// resumed directly from the Load Game screen without re-running character creation.
/// </summary>
public sealed class SessionSave
{
    public string   ChosenClassId    { get; set; } = "class.fighter";
    public string   ChosenRaceId     { get; set; } = "race.human";
    /// <summary>Null if the player skipped character creation (debug entry).</summary>
    public StatBlock? RolledStats    { get; set; }
    public string?  HumanChosenStat { get; set; }
}

/// <summary>
/// Root save file schema. Version 1.
///
/// Serialized to JSON by SaveSystem (System.Text.Json).
/// Every breaking schema change must increment Version and add a migration function.
/// See ARCHITECTURE.md §8 (save format version field rule).
/// </summary>
public sealed class SaveData
{
    /// <summary>Increment on every breaking change; add a migration in SaveSystem.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Character and session data — restored to GameSession on load so the player
    /// can go directly from Load Game to GameWorld without re-running character creation.</summary>
    public SessionSave Session { get; set; } = new();

    /// <summary>
    /// World seed used to generate terrain, trees, and village layout.
    /// On load, if this differs from GameSession.WorldSeed the save is rejected —
    /// it belongs to a different world.
    /// </summary>
    public uint WorldSeed { get; set; }

    /// <summary>Kingdom Marker positions, one per founder peer.</summary>
    public List<MarkerSave> Markers { get; set; } = new();

    /// <summary>All buildings placed in the settlement.</summary>
    public List<BuildingSave> Buildings { get; set; } = new();

    /// <summary>Settlement stockpile contents: itemId → count.</summary>
    public Dictionary<string, int> Stockpile { get; set; } = new();

    /// <summary>IDs of trees that have been fully felled (not yet re-grown).</summary>
    public List<string> FelledTreeIds { get; set; } = new();

    /// <summary>Active NPC work assignments (villagerId → station).</summary>
    public List<NpcAssignSave> NpcAssignments { get; set; } = new();

    /// <summary>One entry per player peer that was connected at save time.</summary>
    public List<PlayerSave> Players { get; set; } = new();

    /// <summary>
    /// Fog-of-war grid serialized as Base64 (worldgen.md §11.4).
    /// Null in saves predating the fog system — treated as all-UNSEEN on load; no migration needed.
    /// </summary>
    public string? FogBase64 { get; set; }
}

/// <summary>Kingdom Marker position for one founder peer.</summary>
public sealed class MarkerSave
{
    public long  PeerId { get; set; }
    public float X      { get; set; }
    public float Y      { get; set; }
    public float Z      { get; set; }
}

/// <summary>One placed building in the settlement.</summary>
public sealed class BuildingSave
{
    /// <summary>Stable string ID, e.g. "building.woodcutters_post".</summary>
    public string BuildingId { get; set; } = "";

    /// <summary>Terrain-level position (before the height-offset applied in SpawnBuilding).</summary>
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
}

/// <summary>NPC-to-station work assignment.</summary>
public sealed class NpcAssignSave
{
    public string NpcId           { get; set; } = "";
    public string StationNodeName { get; set; } = "";
    public long   FounderPeerId   { get; set; }
}

/// <summary>All save-relevant state for one player peer.</summary>
public sealed class PlayerSave
{
    public long PeerId { get; set; }

    /// <summary>Inventory contents: itemId → count.</summary>
    public Dictionary<string, int> Items { get; set; } = new();

    /// <summary>9 hotbar slots; null = empty slot.</summary>
    public string?[] HotbarSlots { get; set; } = new string?[9];

    /// <summary>Raw XP accumulated per skill (before class bump and stat cap).</summary>
    public Dictionary<string, int> SkillXp { get; set; } = new();

    /// <summary>Class starting skill bumps (flat bonus on top of raw XP).</summary>
    public Dictionary<string, int> SkillBumps { get; set; } = new();

    public float Hp     { get; set; } = 100f;

    /// <summary>
    /// Rolled base MaxHp at character creation (before Athletics growth bonus).
    /// Default 100f for backward compat with saves written before the HP system.
    /// Additive field — no version bump required.
    /// </summary>
    public float BaseHp { get; set; } = 100f;

    public float Hunger { get; set; } = 100f;
    public float Rest   { get; set; } = 100f;

    public float PosX { get; set; }
    public float PosY { get; set; }
    public float PosZ { get; set; }

    /// <summary>
    /// Equipment slot contents (inventory.md §10.6). Null = slot empty.
    /// Absent in saves predating this feature — System.Text.Json leaves them null,
    /// which is valid (no item equipped) and requires no migration.
    /// </summary>
    public string? EquippedMainHand  { get; set; }
    public string? EquippedOffHand   { get; set; }
    public string? EquippedBodyArmor { get; set; }
}
