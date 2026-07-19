using System.Collections.Generic;

namespace MankersKingdoms.Shared;

/// <summary>
/// Places healing herb patches deterministically on a heightmap using seeded RNG.
/// Pure C#, no Godot dependency — HerbSystem calls this with the same world seed as
/// BushSystem so both server and clients get identical patch lists without a sync RPC.
///
/// Uses XOR salt 0x48455242 ("HERB") so placement is independent of tree and bush
/// placement even with the same world seed.
///
/// Herb patches prefer slightly elevated, flat ground (meadows and hillsides).
/// </summary>
public sealed class HerbGenerator
{
    private const int   HERB_COUNT = 30;
    private const float MAX_SLOPE  = 1.2f;   // slightly steeper tolerance than bushes
    private const float MIN_HEIGHT = 0f;      // herbs need dry ground — no marshes

    private readonly uint         _seed;
    private readonly TerrainConfig _cfg;

    public HerbGenerator(uint seed, TerrainConfig cfg)
    {
        _seed = seed;
        _cfg  = cfg;
    }

    public IReadOnlyList<HerbPatchData> Generate(float[,] heightmap)
    {
        var rng    = new System.Random((int)(_seed ^ 0x48455242u));
        var result = new List<HerbPatchData>(HERB_COUNT);
        int index  = 0;
        int tries  = 0;

        while (result.Count < HERB_COUNT && tries < HERB_COUNT * 20)
        {
            tries++;

            int gx = rng.Next(1, _cfg.MapWidth  - 1);
            int gz = rng.Next(1, _cfg.MapHeight - 1);

            float h = heightmap[gx, gz];
            if (h < MIN_HEIGHT) continue;

            float slopeX = System.Math.Abs(heightmap[gx + 1, gz] - h);
            float slopeZ = System.Math.Abs(heightmap[gx, gz + 1] - h);
            if (slopeX > MAX_SLOPE || slopeZ > MAX_SLOPE) continue;

            float worldX = (gx - (_cfg.MapWidth  - 1) / 2f) * _cfg.TileSize;
            float worldZ = (gz - (_cfg.MapHeight - 1) / 2f) * _cfg.TileSize;

            result.Add(new HerbPatchData(index++, worldX, worldZ, h + 0.1f));
        }

        return result;
    }
}
