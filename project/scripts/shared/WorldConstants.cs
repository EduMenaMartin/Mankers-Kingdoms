namespace MankersKingdoms.Shared;

/// <summary>
/// Authoritative unit-scale declaration for Mankers Kingdoms.
///
/// 1 Godot world unit = 1 metre. All distances, speeds, heights, and radii
/// throughout server/, client/, and shared/ are in metres unless explicitly noted.
///
/// Human-scale reference (KayKit Adventurers 2.0, Rig_Medium):
///   Player capsule height : 1.8 m  (set in Player.tscn — .tscn is runtime source of truth)
///   Player capsule radius : 0.3 m  (set in Player.tscn — .tscn is runtime source of truth)
///   Player move speed     : 5 m/s  (PlayerController.SPEED)
///   Gravity               : 9.8 m/s² (PlayerController.GRAVITY)
///
/// Terrain reference (TerrainConfig.Default):
///   Grid cell size        : TILE_SIZE (4 m)
///   World footprint       : 64 × 64 cells → ~252 × 252 m
///   Max height amplitude  : 6 m  (TerrainConfig.NoiseAmp)
///
/// Tree reference (tree.tscn, KayKit Forest Nature Pack):
///   Collision cylinder h  : 3.25 m  (~1.8× player height — young oak scale)
///   Collision cylinder r  : 0.6 m
/// </summary>
public static class WorldConstants
{
    /// <summary>
    /// World units per terrain grid cell. X/Z scale applied to the HeightMapShape3D
    /// StaticBody3D in TerrainSystem so each heightmap sample covers this many metres.
    /// </summary>
    public const float TILE_SIZE = 4f;

    /// <summary>
    /// Documented height of the player collision capsule (metres).
    /// The runtime value lives in Player.tscn (CapsuleShape3D.height).
    /// This constant exists for code that needs to reason about human scale
    /// (e.g. camera placement, NPC line-of-sight offsets) without importing the scene.
    /// </summary>
    public const float PLAYER_CAPSULE_HEIGHT = 1.8f;

    /// <summary>
    /// Documented radius of the player collision capsule (metres).
    /// The runtime value lives in Player.tscn (CapsuleShape3D.radius).
    /// </summary>
    public const float PLAYER_CAPSULE_RADIUS = 0.3f;
}
