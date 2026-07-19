using System;
using System.Collections.Generic;

namespace MankersKingdoms.Shared;

/// <summary>
/// Places trees deterministically on a heightmap using seeded RNG + noise-based density.
/// Pure C#, no Godot dependency — both TreeSystem (server) and any client init code
/// call this with the same seed to get the identical tree list without a sync RPC.
///
/// Density is sampled from a value-noise field so trees form organic clusters rather than
/// being scattered uniformly. The average density across the map stays close to TreeConfig.Density.
/// Noise uses seed XOR 0xF07E5700u — independent of terrain and bush generators.
/// </summary>
public sealed class TreeGenerator
{
    private readonly uint _seed;
    private readonly TerrainConfig _terrain;
    private readonly TreeConfig _trees;

    public TreeGenerator(uint seed, TerrainConfig terrain, TreeConfig trees)
    {
        _seed    = seed;
        _terrain = terrain;
        _trees   = trees;
    }

    /// <param name="riverMask">
    /// Optional channel mask from RiverGenerator. When non-null, cells marked true are skipped —
    /// prevents trees from appearing on river banks that pass the MinHeight check but are
    /// inside the carved channel. Pass <c>TerrainSystem.River?.ChannelMask</c> at call sites.
    /// </param>
    public IReadOnlyList<TreeData> Generate(float[,] heightmap, bool[,]? riverMask = null)
    {
        // RNG seed offset keeps tree sequence independent of terrain/bush RNG.
        var rng = new System.Random((int)(_seed ^ 0xDEADBEEF));
        var result = new List<TreeData>();
        int index = 0;

        for (int x = 0; x < _terrain.MapWidth; x++)
        for (int z = 0; z < _terrain.MapHeight; z++)
        {
            float h = heightmap[x, z];
            if (h < _trees.MinHeight) continue;

            // Skip cells inside the carved river channel.
            if (riverMask != null
                && x < riverMask.GetLength(0) && z < riverMask.GetLength(1)
                && riverMask[x, z]) continue;

            // Noise-sampled local density: averages ~Density across the map (noise mean ≈ 0.5,
            // so Density * 2 * 0.5 = Density), but varies tile-to-tile creating patches.
            float localDensity = Math.Clamp(DensityNoise(x, z) * _trees.Density * 2f, 0f, 1f);
            if (rng.NextDouble() > localDensity) continue;

            float worldX = (x - (_terrain.MapWidth  - 1) / 2f) * _terrain.TileSize;
            float worldZ = (z - (_terrain.MapHeight - 1) / 2f) * _terrain.TileSize;

            result.Add(new TreeData(
                Id:     $"tree_{index++}",
                GridX:  x,
                GridZ:  z,
                WorldX: worldX,
                WorldY: h,
                WorldZ: worldZ
            ));
        }

        return result;
    }

    // ── Density noise ─────────────────────────────────────────────────────────

    private float DensityNoise(int ix, int iz)
    {
        float fx = ix * _trees.DensityNoiseFreq;
        float fz = iz * _trees.DensityNoiseFreq;
        return ValueNoise(fx, fz);
    }

    private float ValueNoise(float x, float z)
    {
        int x0 = (int)MathF.Floor(x);
        int z0 = (int)MathF.Floor(z);
        float fx = x - x0;
        float fz = z - z0;

        // Smoothstep
        fx = fx * fx * (3f - 2f * fx);
        fz = fz * fz * (3f - 2f * fz);

        float v00 = Hash(x0,     z0);
        float v10 = Hash(x0 + 1, z0);
        float v01 = Hash(x0,     z0 + 1);
        float v11 = Hash(x0 + 1, z0 + 1);

        return Lerp(Lerp(v00, v10, fx), Lerp(v01, v11, fx), fz);
    }

    private float Hash(int ix, int iz)
    {
        unchecked
        {
            uint h = _seed ^ 0xF07E5700u; // "FOREST" mnemonic, independent of terrain hash
            h ^= (uint)(ix * 1619 + iz * 31337);
            h ^= h >> 16;
            h *= 0x45d9f3b;
            h ^= h >> 16;
            return (h & 0xFFFFu) / 65535f; // [0, 1]
        }
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
