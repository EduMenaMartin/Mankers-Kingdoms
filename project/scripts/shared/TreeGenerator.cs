using System.Collections.Generic;

namespace MankersKingdoms.Shared;

/// <summary>
/// Places trees deterministically on a heightmap using seeded RNG.
/// Pure C#, no Godot dependency — both TreeSystem (server) and any client init code
/// call this with the same seed to get the identical tree list without a sync RPC.
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

    public IReadOnlyList<TreeData> Generate(float[,] heightmap)
    {
        // Use a seed offset so tree RNG sequence is independent of terrain RNG.
        var rng = new System.Random((int)(_seed ^ 0xDEADBEEF));
        var result = new List<TreeData>();
        int index = 0;

        for (int x = 0; x < _terrain.MapWidth; x++)
        for (int z = 0; z < _terrain.MapHeight; z++)
        {
            float h = heightmap[x, z];
            if (h < _trees.MinHeight) continue;
            if (rng.NextDouble() > _trees.Density) continue;

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
}
