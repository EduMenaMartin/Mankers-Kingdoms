using System.Collections.Generic;

namespace MankersKingdoms.Shared;

/// <summary>
/// Places berry bushes deterministically on a heightmap using seeded RNG.
/// Pure C#, no Godot dependency — BushSystem calls this with the same seed as
/// TreeGenerator so both server and clients get identical bush lists without a sync RPC.
///
/// Uses a different seed XOR mask (0xBEEFCAFE) so bush placement is independent
/// of tree placement even with the same world seed.
/// </summary>
public sealed class BushGenerator
{
    private const int   BUSH_COUNT   = 50;
    private const float MAX_SLOPE    = 1.5f;  // skip cells with height delta > this
    private const float MIN_HEIGHT   = -2f;   // skip underwater / very low cells

    private readonly uint _seed;
    private readonly TerrainConfig _cfg;

    public BushGenerator(uint seed, TerrainConfig cfg)
    {
        _seed = seed;
        _cfg  = cfg;
    }

    public IReadOnlyList<BushData> Generate(float[,] heightmap)
    {
        var rng    = new System.Random((int)(_seed ^ 0xBEEFCAFEu));
        var result = new List<BushData>(BUSH_COUNT);
        int index  = 0;
        int tries  = 0;

        while (result.Count < BUSH_COUNT && tries < BUSH_COUNT * 20)
        {
            tries++;

            // Pick a random grid cell.
            int gx = rng.Next(1, _cfg.MapWidth  - 1);
            int gz = rng.Next(1, _cfg.MapHeight - 1);

            float h = heightmap[gx, gz];
            if (h < MIN_HEIGHT) continue;

            // Skip steep terrain — bushes don't grow on cliff faces.
            float slopeX = System.Math.Abs(heightmap[gx + 1, gz] - h);
            float slopeZ = System.Math.Abs(heightmap[gx, gz + 1] - h);
            if (slopeX > MAX_SLOPE || slopeZ > MAX_SLOPE) continue;

            float worldX = (gx - (_cfg.MapWidth  - 1) / 2f) * _cfg.TileSize;
            float worldZ = (gz - (_cfg.MapHeight - 1) / 2f) * _cfg.TileSize;

            result.Add(new BushData(index++, worldX, worldZ, h + 0.1f));
        }

        return result;
    }
}
