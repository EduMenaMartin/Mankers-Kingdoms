using MankersKingdoms.Shared;
using Xunit;

namespace MankersKingdoms.Tests.Shared;

public class TreeGeneratorTests
{
    private static readonly TerrainConfig TerrainCfg = new()
    {
        MapWidth = 32, MapHeight = 32, TileSize = 4f,
        NoiseFreq = 0.06f, NoiseOctaves = 3, NoiseAmp = 6f
    };
    private static readonly TreeConfig TreeCfg = new()
    {
        Density = 0.12f, MinHeight = -1f, TreeHp = 5, WoodYield = 3, WoodcuttingXp = 25
    };

    private static float[,] MakeHeightmap(uint seed = 42u) =>
        new TerrainGenerator(seed, TerrainCfg).GenerateHeightmap();

    [Fact]
    public void SameSeed_ProducesIdenticalTreeList()
    {
        var h = MakeHeightmap();
        var g = new TreeGenerator(42u, TerrainCfg, TreeCfg);
        var t1 = g.Generate(h);
        var t2 = g.Generate(h);

        Assert.Equal(t1.Count, t2.Count);
        for (int i = 0; i < t1.Count; i++)
        {
            Assert.Equal(t1[i].Id,     t2[i].Id);
            Assert.Equal(t1[i].WorldX, t2[i].WorldX);
            Assert.Equal(t1[i].WorldZ, t2[i].WorldZ);
        }
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentTrees()
    {
        // Count alone can collide; compare cumulative WorldX as a cheap positional hash.
        var h = MakeHeightmap();
        var t1 = new TreeGenerator(1u,  TerrainCfg, TreeCfg).Generate(h);
        var t2 = new TreeGenerator(99u, TerrainCfg, TreeCfg).Generate(h);

        float sum1 = 0f, sum2 = 0f;
        foreach (var t in t1) sum1 += t.WorldX;
        foreach (var t in t2) sum2 += t.WorldX;

        Assert.NotEqual(sum1, sum2);
    }

    [Fact]
    public void TreeIds_AreUniqueAndStable()
    {
        var h    = MakeHeightmap();
        var trees = new TreeGenerator(42u, TerrainCfg, TreeCfg).Generate(h);
        var ids  = new System.Collections.Generic.HashSet<string>();

        foreach (var t in trees)
        {
            Assert.StartsWith("tree_", t.Id);
            Assert.True(ids.Add(t.Id), $"Duplicate tree ID: {t.Id}");
        }
    }

    [Fact]
    public void Trees_OnlySpawnAboveMinHeight()
    {
        var h    = MakeHeightmap();
        var trees = new TreeGenerator(42u, TerrainCfg, TreeCfg).Generate(h);
        foreach (var t in trees)
            Assert.True(t.WorldY >= TreeCfg.MinHeight,
                $"Tree {t.Id} spawned below MinHeight ({t.WorldY} < {TreeCfg.MinHeight})");
    }
}
