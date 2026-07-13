namespace MankersKingdoms.Shared;

/// <summary>
/// Parameters driving procedural terrain generation.
/// Default values target a 64×64 tile map at 4 world-units per tile (256×256 m total).
/// </summary>
public sealed class TerrainConfig
{
    public int MapWidth       { get; init; } = 64;
    public int MapHeight      { get; init; } = 64;
    public float TileSize     { get; init; } = WorldConstants.TILE_SIZE;
    public float NoiseFreq    { get; init; } = 0.06f;
    public int NoiseOctaves   { get; init; } = 4;
    public float NoiseAmp     { get; init; } = 6f;   // max height variation in world units

    public static TerrainConfig Default { get; } = new();
}
