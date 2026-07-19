using MankersKingdoms.Shared;
using Xunit;

namespace MankersKingdoms.Tests.Shared;

public class NestGeneratorTests
{
    private const uint TEST_SEED = 12345u;

    [Fact]
    public void Generate_ReturnsThreeNests()
    {
        var nests = NestGenerator.Generate(TEST_SEED);
        Assert.Equal(3, nests.Count);
    }

    [Fact]
    public void Generate_ExactlyOneMajorNest()
    {
        var nests = NestGenerator.Generate(TEST_SEED);
        Assert.Single(nests, n => n.Tier == NestTier.Major);
    }

    [Fact]
    public void Generate_ExactlyTwoMinorNests()
    {
        var nests = NestGenerator.Generate(TEST_SEED);
        Assert.Equal(2, nests.Count(n => n.Tier == NestTier.Minor));
    }

    [Fact]
    public void MajorNest_ContainsOrc()
    {
        var nests = NestGenerator.Generate(TEST_SEED);
        var major = nests.Single(n => n.Tier == NestTier.Major);
        Assert.Contains("monster.orc", major.MonsterTypeIds);
    }

    [Fact]
    public void MinorNests_DoNotContainOrc()
    {
        var nests = NestGenerator.Generate(TEST_SEED);
        foreach (var n in nests.Where(n => n.Tier == NestTier.Minor))
            Assert.DoesNotContain("monster.orc", n.MonsterTypeIds);
    }

    [Fact]
    public void Generate_IsDeterministicForSameSeed()
    {
        var a = NestGenerator.Generate(TEST_SEED);
        var b = NestGenerator.Generate(TEST_SEED);
        for (int i = 0; i < a.Count; i++)
        {
            Assert.Equal(a[i].WorldX, b[i].WorldX);
            Assert.Equal(a[i].WorldZ, b[i].WorldZ);
            Assert.Equal(a[i].Tier,   b[i].Tier);
        }
    }

    [Fact]
    public void Generate_DifferentSeedsProduceDifferentPositions()
    {
        var a = NestGenerator.Generate(TEST_SEED);
        var b = NestGenerator.Generate(TEST_SEED + 1);
        bool anyDifferent = false;
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i].WorldX != b[i].WorldX || a[i].WorldZ != b[i].WorldZ)
                anyDifferent = true;
        }
        Assert.True(anyDifferent, "Different seeds should produce at least one different nest position");
    }

    [Fact]
    public void AllNests_WithinMapBounds()
    {
        var nests = NestGenerator.Generate(TEST_SEED);
        foreach (var n in nests)
        {
            Assert.InRange(n.WorldX, -NestGenerator.MAP_HALF, NestGenerator.MAP_HALF);
            Assert.InRange(n.WorldZ, -NestGenerator.MAP_HALF, NestGenerator.MAP_HALF);
        }
    }

    [Fact]
    public void AllNests_HavePositiveRespawnDelay()
    {
        var nests = NestGenerator.Generate(TEST_SEED);
        foreach (var n in nests)
            Assert.True(n.RespawnDelaySec > 0f, $"Nest {n.Id} has non-positive respawn delay");
    }

    [Fact]
    public void AllNests_HaveUniqueIds()
    {
        var nests = NestGenerator.Generate(TEST_SEED);
        var ids   = nests.Select(n => n.Id).ToList();
        Assert.Equal(ids.Distinct().Count(), ids.Count);
    }

    [Fact]
    public void AllNests_AtLeast45UnitsFromVillage()
    {
        var nests     = NestGenerator.Generate(TEST_SEED);
        var (vx, vz)  = VillageGenerator.GetVillageCenter(TEST_SEED);
        const float MIN_DIST = 45f;
        foreach (var n in nests)
        {
            float dx   = n.WorldX - vx;
            float dz   = n.WorldZ - vz;
            float dist = MathF.Sqrt(dx * dx + dz * dz);
            Assert.True(dist >= MIN_DIST,
                $"Nest {n.Id} is only {dist:F1}m from village centre (min {MIN_DIST}m)");
        }
    }
}
