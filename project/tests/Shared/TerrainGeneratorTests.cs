using MankersKingdoms.Shared;
using Xunit;

namespace MankersKingdoms.Tests.Shared;

public class TerrainGeneratorTests
{
    private static readonly TerrainConfig Cfg = new()
    {
        MapWidth = 16, MapHeight = 16, TileSize = 4f,
        NoiseFreq = 0.06f, NoiseOctaves = 3, NoiseAmp = 6f
    };

    [Fact]
    public void SameSeed_ProducesIdenticalHeightmap()
    {
        var h1 = new TerrainGenerator(99u, Cfg).GenerateHeightmap();
        var h2 = new TerrainGenerator(99u, Cfg).GenerateHeightmap();

        for (int x = 0; x < Cfg.MapWidth; x++)
        for (int z = 0; z < Cfg.MapHeight; z++)
            Assert.Equal(h1[x, z], h2[x, z]);
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentHeightmaps()
    {
        var h1 = new TerrainGenerator(1u, Cfg).GenerateHeightmap();
        var h2 = new TerrainGenerator(2u, Cfg).GenerateHeightmap();

        // At least one cell must differ (extremely unlikely to be identical)
        bool anyDiff = false;
        for (int x = 0; x < Cfg.MapWidth && !anyDiff; x++)
        for (int z = 0; z < Cfg.MapHeight && !anyDiff; z++)
            anyDiff |= h1[x, z] != h2[x, z];

        Assert.True(anyDiff);
    }

    [Fact]
    public void Heightmap_DimensionsMatchConfig()
    {
        var h = new TerrainGenerator(42u, Cfg).GenerateHeightmap();
        Assert.Equal(Cfg.MapWidth,  h.GetLength(0));
        Assert.Equal(Cfg.MapHeight, h.GetLength(1));
    }

    [Fact]
    public void Heights_AreWithinAmplitudeBounds()
    {
        var h = new TerrainGenerator(7u, Cfg).GenerateHeightmap();
        float half = Cfg.NoiseAmp * 0.6f; // allow slight overshoot at edges of norm range

        for (int x = 0; x < Cfg.MapWidth; x++)
        for (int z = 0; z < Cfg.MapHeight; z++)
            Assert.InRange(h[x, z], -half, half);
    }
}
