using System;
using System.Collections.Generic;
using MankersKingdoms.Shared;
using Xunit;

namespace MankersKingdoms.Tests.Shared;

public class RiverGeneratorTests
{
    private static readonly TerrainConfig Cfg = new()
    {
        MapWidth  = 32, MapHeight = 32, TileSize = 4f,
        NoiseFreq = 0.06f, NoiseOctaves = 3, NoiseAmp = 6f
    };

    private static float[,] MakeHeightmap(uint seed = 42u) =>
        new TerrainGenerator(seed, Cfg).GenerateHeightmap();

    private static float[,] Clone(float[,] src)
    {
        var dst = new float[src.GetLength(0), src.GetLength(1)];
        Array.Copy(src, dst, src.Length);
        return dst;
    }

    // ── Determinism ───────────────────────────────────────────────────────────

    [Fact]
    public void SameSeed_ProducesSameRiverPath()
    {
        var h1 = MakeHeightmap();
        var h2 = Clone(h1);

        var r1 = new RiverGenerator(42u, Cfg).Generate(h1);
        var r2 = new RiverGenerator(42u, Cfg).Generate(h2);

        Assert.Equal(r1.Segments.Count, r2.Segments.Count);
        for (int i = 0; i < r1.Segments.Count; i++)
        {
            Assert.Equal(r1.Segments[i].GridX,  r2.Segments[i].GridX);
            Assert.Equal(r1.Segments[i].GridZ,  r2.Segments[i].GridZ);
            Assert.Equal(r1.Segments[i].WaterY, r2.Segments[i].WaterY);
        }
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentPaths()
    {
        var h1 = MakeHeightmap(1u);
        var h2 = MakeHeightmap(2u);

        var r1 = new RiverGenerator(1u, Cfg).Generate(h1);
        var r2 = new RiverGenerator(2u, Cfg).Generate(h2);

        // Different seeds → at least one segment differs.
        bool anyDiff = r1.Segments.Count != r2.Segments.Count;
        for (int i = 0; i < Math.Min(r1.Segments.Count, r2.Segments.Count) && !anyDiff; i++)
            anyDiff |= r1.Segments[i].GridX != r2.Segments[i].GridX
                    || r1.Segments[i].GridZ != r2.Segments[i].GridZ;

        Assert.True(anyDiff, "Different seeds must produce different river paths");
    }

    // ── Path validity ─────────────────────────────────────────────────────────

    [Fact]
    public void RiverPath_AllSegmentsWithinMapBounds()
    {
        var h = MakeHeightmap();
        var river = new RiverGenerator(42u, Cfg).Generate(h);

        foreach (var seg in river.Segments)
        {
            Assert.InRange(seg.GridX, 0, Cfg.MapWidth  - 1);
            Assert.InRange(seg.GridZ, 0, Cfg.MapHeight - 1);
        }
    }

    [Fact]
    public void RiverPath_MinimumLength()
    {
        // Expect a meaningful path (at least MapWidth / 2 steps).
        // Tests across several seeds to catch edge cases.
        int minExpected = Cfg.MapWidth / 2;
        for (uint seed = 1u; seed <= 10u; seed++)
        {
            var h = MakeHeightmap(seed);
            var river = new RiverGenerator(seed, Cfg).Generate(h);
            Assert.True(river.Segments.Count >= minExpected,
                $"Seed {seed}: river too short ({river.Segments.Count} segments, min {minExpected})");
        }
    }

    [Fact]
    public void RiverPath_TangentsAreNormalised()
    {
        var h = MakeHeightmap();
        var river = new RiverGenerator(42u, Cfg).Generate(h);

        foreach (var seg in river.Segments)
        {
            float len = MathF.Sqrt(seg.TangentX * seg.TangentX + seg.TangentZ * seg.TangentZ);
            Assert.InRange(len, 0.99f, 1.01f);
        }
    }

    // ── Terrain carving ───────────────────────────────────────────────────────

    [Fact]
    public void Carving_CentreCellsAreLowerThanOriginal()
    {
        var h    = MakeHeightmap();
        var orig = Clone(h);
        var river = new RiverGenerator(42u, Cfg).Generate(h);

        int carved = 0;
        foreach (var seg in river.Segments)
        {
            float after  = h[seg.GridX, seg.GridZ];
            float before = orig[seg.GridX, seg.GridZ];
            if (after < before) carved++;
        }

        Assert.True(carved > 0,
            "At least some path-centre cells must be carved below their original height");
    }

    [Fact]
    public void ChannelMask_TrueForPathCells()
    {
        var h = MakeHeightmap();
        var river = new RiverGenerator(42u, Cfg).Generate(h);

        foreach (var seg in river.Segments)
            Assert.True(river.ChannelMask[seg.GridX, seg.GridZ],
                $"ChannelMask must be true at path centre ({seg.GridX}, {seg.GridZ})");
    }

    [Fact]
    public void IsInChannel_ReturnsTrueForPathCells()
    {
        var h = MakeHeightmap();
        var river = new RiverGenerator(42u, Cfg).Generate(h);

        foreach (var seg in river.Segments)
            Assert.True(river.IsInChannel(seg.GridX, seg.GridZ));
    }

    [Fact]
    public void IsInChannel_ReturnsFalseForOutOfBounds()
    {
        var h = MakeHeightmap();
        var river = new RiverGenerator(42u, Cfg).Generate(h);

        Assert.False(river.IsInChannel(-1, 0));
        Assert.False(river.IsInChannel(0, -1));
        Assert.False(river.IsInChannel(Cfg.MapWidth, 0));
        Assert.False(river.IsInChannel(0, Cfg.MapHeight));
    }

    // ── Water surface is downstream-sloped ───────────────────────────────────

    [Fact]
    public void WaterY_SourceHigherThanOrEqualToMouth()
    {
        // After the monotonic height-smoothing pass, the path heights are non-increasing,
        // so WaterY at the source must be >= WaterY at the mouth.
        var h = MakeHeightmap();
        var river = new RiverGenerator(42u, Cfg).Generate(h);

        if (river.Segments.Count < 2) return; // degenerate — skip

        float sourceY = river.Segments[0].WaterY;
        float mouthY  = river.Segments[river.Segments.Count - 1].WaterY;

        Assert.True(sourceY >= mouthY - 0.001f, // small epsilon for float precision
            $"Source WaterY ({sourceY:F3}) should be >= mouth WaterY ({mouthY:F3})");
    }

    // ── Channel mask excludes trees (integration with TreeGenerator) ──────────

    [Fact]
    public void TreeGenerator_NoTreesInsideRiverChannel()
    {
        var h     = MakeHeightmap();
        var river = new RiverGenerator(42u, Cfg).Generate(h);

        var treeCfg = new TreeConfig
        {
            Density = 0.12f, MinHeight = -1f, TreeHp = 5, WoodYield = 3, WoodcuttingXp = 25
        };
        var trees = new TreeGenerator(42u, Cfg, treeCfg)
            .Generate(h, river.ChannelMask);

        foreach (var t in trees)
            Assert.False(river.IsInChannel(t.GridX, t.GridZ),
                $"Tree {t.Id} at ({t.GridX},{t.GridZ}) is inside the carved river channel");
    }
}
