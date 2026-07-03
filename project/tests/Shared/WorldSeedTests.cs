using MankersKingdoms.Shared;
using Xunit;

namespace MankersKingdoms.Tests.Shared;

public class WorldSeedTests
{
    [Fact]
    public void SameSeed_ProducesSameSequence()
    {
        GameSession.WorldSeed = 12345u;
        var r1 = WorldSeed.CreateRandom();
        var r2 = WorldSeed.CreateRandom();

        for (int i = 0; i < 20; i++)
            Assert.Equal(r1.Next(), r2.Next());
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentSequences()
    {
        GameSession.WorldSeed = 1u;
        var v1 = WorldSeed.CreateRandom().Next();

        GameSession.WorldSeed = 2u;
        var v2 = WorldSeed.CreateRandom().Next();

        Assert.NotEqual(v1, v2);
    }
}
