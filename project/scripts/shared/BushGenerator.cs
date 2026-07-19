using System.Collections.Generic;

namespace MankersKingdoms.Shared;

/// <summary>
/// Places berry bushes deterministically on a heightmap using seeded RNG.
/// Pure C#, no Godot dependency — BushSystem calls this with the same seed as
/// TreeGenerator so both server and clients get identical bush lists without a sync RPC.
///
/// Uses seed XOR mask (0xBEEFCAFE) so bush placement is independent of tree placement
/// even with the same world seed.
///
/// Forest clustering (M10):
///   When a tree list is supplied, placement rolls are biased by proximity to existing
///   trees. Cells within CLUSTER_RADIUS tiles of any tree have a 70% acceptance chance;
///   isolated cells have 30%. This creates natural undergrowth clustering without changing
///   the target bush count or the overall rejection-sampling structure.
/// </summary>
public sealed class BushGenerator
{
    private const int   BUSH_COUNT   = 50;
    private const float MAX_SLOPE    = 1.5f;  // skip cells with height delta > this
    private const float MIN_HEIGHT   = -2f;   // skip very low / channel-bottom cells

    // Clustering constants (applied only when trees != null).
    private const int   CLUSTER_RADIUS    = 3;    // tiles — Chebyshev circle radius
    private const float NEAR_TREE_CHANCE  = 0.70f; // acceptance rate near a tree
    private const float ISOLATED_CHANCE   = 0.30f; // acceptance rate far from trees

    private readonly uint         _seed;
    private readonly TerrainConfig _cfg;

    public BushGenerator(uint seed, TerrainConfig cfg)
    {
        _seed = seed;
        _cfg  = cfg;
    }

    /// <param name="heightmap">Carved heightmap from TerrainSystem (post-river carving).</param>
    /// <param name="trees">
    /// Optional tree list from TreeGenerator. When non-null, activates forest clustering:
    /// bushes are biased to spawn within CLUSTER_RADIUS tiles of existing trees.
    /// </param>
    /// <param name="riverMask">
    /// Optional channel mask from RiverGenerator. Cells marked true are skipped — prevents
    /// bushes inside the carved river channel even if terrain height passes MIN_HEIGHT.
    /// Pass <c>TerrainSystem.River?.ChannelMask</c> at call sites.
    /// </param>
    public IReadOnlyList<BushData> Generate(
        float[,]                  heightmap,
        IReadOnlyList<TreeData>?  trees     = null,
        bool[,]?                  riverMask = null)
    {
        var rng    = new System.Random((int)(_seed ^ 0xBEEFCAFEu));
        var result = new List<BushData>(BUSH_COUNT);
        int index  = 0;
        int tries  = 0;
        int maxTries = BUSH_COUNT * 40; // extra margin when clustering reduces acceptance rate

        while (result.Count < BUSH_COUNT && tries < maxTries)
        {
            tries++;

            int gx = rng.Next(1, _cfg.MapWidth  - 1);
            int gz = rng.Next(1, _cfg.MapHeight - 1);

            // Skip cells inside the carved river channel.
            if (riverMask != null
                && gx < riverMask.GetLength(0) && gz < riverMask.GetLength(1)
                && riverMask[gx, gz]) continue;

            float h = heightmap[gx, gz];
            if (h < MIN_HEIGHT) continue;

            float slopeX = System.Math.Abs(heightmap[gx + 1, gz] - h);
            float slopeZ = System.Math.Abs(heightmap[gx, gz + 1] - h);
            if (slopeX > MAX_SLOPE || slopeZ > MAX_SLOPE) continue;

            // Forest clustering: accept with higher probability near trees.
            if (trees != null)
            {
                float chance = IsNearAnyTree(gx, gz, trees)
                    ? NEAR_TREE_CHANCE
                    : ISOLATED_CHANCE;
                if (rng.NextDouble() > chance) continue;
            }

            float worldX = (gx - (_cfg.MapWidth  - 1) / 2f) * _cfg.TileSize;
            float worldZ = (gz - (_cfg.MapHeight - 1) / 2f) * _cfg.TileSize;

            result.Add(new BushData(index++, worldX, worldZ, h + 0.1f));
        }

        return result;
    }

    /// <summary>
    /// Returns true if (gx, gz) is within CLUSTER_RADIUS tiles of any tree in the list.
    /// Uses squared distances to avoid MathF.Sqrt per tree.
    /// </summary>
    private static bool IsNearAnyTree(int gx, int gz, IReadOnlyList<TreeData> trees)
    {
        const int radiusSq = CLUSTER_RADIUS * CLUSTER_RADIUS;
        foreach (var t in trees)
        {
            int dx = gx - t.GridX;
            int dz = gz - t.GridZ;
            if (dx * dx + dz * dz <= radiusSq)
                return true;
        }
        return false;
    }
}
