using System.Collections.Generic;
using MankersKingdoms.Shared;
using Xunit;

namespace MankersKingdoms.Tests.Shared;

public class BushGeneratorTests
{
    private static readonly TerrainConfig Cfg = new()
    {
        MapWidth = 32, MapHeight = 32, TileSize = 4f,
        NoiseFreq = 0.06f, NoiseOctaves = 3, NoiseAmp = 6f
    };

    private static readonly TreeConfig TreeCfg = new()
    {
        Density = 0.12f, MinHeight = -1f, TreeHp = 5, WoodYield = 3, WoodcuttingXp = 25
    };

    private static float[,] MakeHeightmap(uint seed = 42u) =>
        new TerrainGenerator(seed, Cfg).GenerateHeightmap();

    // ── Determinism ───────────────────────────────────────────────────────────

    [Fact]
    public void SameSeed_NoTrees_ProducesIdenticalList()
    {
        var h  = MakeHeightmap();
        var b1 = new BushGenerator(42u, Cfg).Generate(h);
        var b2 = new BushGenerator(42u, Cfg).Generate(h);

        Assert.Equal(b1.Count, b2.Count);
        for (int i = 0; i < b1.Count; i++)
        {
            Assert.Equal(b1[i].WorldX, b2[i].WorldX);
            Assert.Equal(b1[i].WorldZ, b2[i].WorldZ);
        }
    }

    [Fact]
    public void SameSeed_WithTrees_ProducesIdenticalList()
    {
        var h     = MakeHeightmap();
        var trees = new TreeGenerator(42u, Cfg, TreeCfg).Generate(h);

        var b1 = new BushGenerator(42u, Cfg).Generate(h, trees);
        var b2 = new BushGenerator(42u, Cfg).Generate(h, trees);

        Assert.Equal(b1.Count, b2.Count);
        for (int i = 0; i < b1.Count; i++)
        {
            Assert.Equal(b1[i].WorldX, b2[i].WorldX);
            Assert.Equal(b1[i].WorldZ, b2[i].WorldZ);
        }
    }

    // ── Placement correctness ─────────────────────────────────────────────────

    [Fact]
    public void Bushes_OnlySpawnAboveMinHeight()
    {
        var h    = MakeHeightmap();
        var bush = new BushGenerator(42u, Cfg).Generate(h);

        // BushGenerator spawns at h + 0.1f; verify original terrain was above MIN_HEIGHT.
        // (MIN_HEIGHT = -2f; BushData.WorldY = terrainH + 0.1f → terrainH = WorldY - 0.1f)
        foreach (var b in bush)
            Assert.True(b.WorldY - 0.1f >= -2f,
                $"Bush at ({b.WorldX},{b.WorldZ}) spawned below MIN_HEIGHT");
    }

    [Fact]
    public void Bushes_WithRiverMask_NoneInChannel()
    {
        var h     = MakeHeightmap();
        var river = new RiverGenerator(42u, Cfg).Generate(h);
        var bush  = new BushGenerator(42u, Cfg).Generate(h, null, river.ChannelMask);

        foreach (var b in bush)
        {
            // Reconstruct grid coords from world position.
            int gx = (int)System.MathF.Round(b.WorldX / Cfg.TileSize + (Cfg.MapWidth  - 1) / 2f);
            int gz = (int)System.MathF.Round(b.WorldZ / Cfg.TileSize + (Cfg.MapHeight - 1) / 2f);
            Assert.False(river.IsInChannel(gx, gz),
                $"Bush at ({b.WorldX:F1},{b.WorldZ:F1}) grid ({gx},{gz}) is inside river channel");
        }
    }

    // ── Forest clustering ─────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that forest clustering concentrates bushes near trees.
    /// Uses a flat heightmap (all terrain checks pass) with trees planted in the
    /// top-left quadrant, then compares bush concentration before and after clustering.
    /// </summary>
    [Fact]
    public void Clustering_IncreasesBushConcentrationNearTrees()
    {
        // Flat heightmap: all zeros, above MIN_HEIGHT=-2 and zero slope → all cells pass checks.
        var flat = new float[Cfg.MapWidth, Cfg.MapHeight]; // all 0f

        // Place trees densely in the top-left quadrant (gx 2..13, gz 2..13, every 3 tiles).
        // This is the "near tree" zone for the clustering bias.
        var trees = new List<TreeData>();
        for (int x = 2; x < 14; x += 3)
        for (int z = 2; z < 14; z += 3)
        {
            float wx = (x - (Cfg.MapWidth  - 1) / 2f) * Cfg.TileSize;
            float wz = (z - (Cfg.MapHeight - 1) / 2f) * Cfg.TileSize;
            trees.Add(new TreeData($"t_{x}_{z}", x, z, wx, 0f, wz));
        }

        // World-space X threshold for "top-left quadrant" = gx < 16 ≈ WorldX < 2.
        // (gx=16 → WorldX = (16 - 15.5) * 4 = 2m)
        const float HALF = 2f;

        // Without clustering — flat chance everywhere.
        var flatA = new float[Cfg.MapWidth, Cfg.MapHeight];
        var bushesNoCluster = new BushGenerator(42u, Cfg).Generate(flatA, null);

        // With clustering — top-left is "near tree" → higher acceptance.
        var flatB = new float[Cfg.MapWidth, Cfg.MapHeight];
        var bushesClustered = new BushGenerator(42u, Cfg).Generate(flatB, trees);

        int noClusterCount  = 0;
        int clusteredCount  = 0;
        foreach (var b in bushesNoCluster)
            if (b.WorldX < HALF && b.WorldZ < HALF) noClusterCount++;
        foreach (var b in bushesClustered)
            if (b.WorldX < HALF && b.WorldZ < HALF) clusteredCount++;

        Assert.True(clusteredCount > noClusterCount,
            $"Clustering should place more bushes near trees: " +
            $"clustered={clusteredCount} vs uniform={noClusterCount} in top-left quadrant");
    }

    [Fact]
    public void Clustering_DoesNotBreakTargetCount()
    {
        // Clustering must still fill the target BUSH_COUNT even with biased acceptance rates.
        var h     = MakeHeightmap();
        var trees = new TreeGenerator(42u, Cfg, TreeCfg).Generate(h);
        var bush  = new BushGenerator(42u, Cfg).Generate(h, trees);

        // BUSH_COUNT = 50; allow slightly fewer if terrain exhausts valid candidates.
        Assert.InRange(bush.Count, 40, 50);
    }
}
