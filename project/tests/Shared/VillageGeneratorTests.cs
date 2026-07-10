using System;
using System.Collections.Generic;
using System.Linq;
using MankersKingdoms.Shared;
using Xunit;

namespace MankersKingdoms.Tests.Shared;

public class VillageGeneratorTests
{
    // Minimal name pool sufficient for all test runs.
    private static readonly IReadOnlyList<string> _names = new[]
    {
        "Alice",  "Bob",   "Carol", "Dave",  "Eve",
        "Frank",  "Grace", "Hal",   "Iris",  "Jack",
        "Kate",   "Len",   "Mia",   "Ned",   "Ora"
    };

    // ── Village layout ────────────────────────────────────────────────────────

    [Fact]
    public void Generate_Returns1Village_WithExpectedId()
    {
        var (village, _) = VillageGenerator.Generate(42u, _names);
        Assert.Equal("village.0", village.Id);
    }

    [Fact]
    public void Generate_VillagePosition_OutsideOriginBuffer()
    {
        // Village should always be placed >= 45 units from world origin.
        for (uint seed = 0; seed < 20; seed++)
        {
            var (village, _) = VillageGenerator.Generate(seed, _names);
            double dist = Math.Sqrt(village.WorldX * village.WorldX + village.WorldZ * village.WorldZ);
            Assert.True(dist >= 44.9, $"seed={seed}: village too close to origin ({dist:F1} < 45)");
        }
    }

    // ── Villager count ────────────────────────────────────────────────────────

    [Fact]
    public void Generate_VillagerCount_InRange6To10()
    {
        for (uint seed = 0; seed < 30; seed++)
        {
            var (_, villagers) = VillageGenerator.Generate(seed, _names);
            Assert.InRange(villagers.Count, 6, 10);
        }
    }

    [Fact]
    public void Generate_VillageData_VillagerIdsMatchVillagerList()
    {
        var (village, villagers) = VillageGenerator.Generate(42u, _names);
        Assert.Equal(village.VillagerIds.Count, villagers.Count);
        for (int i = 0; i < villagers.Count; i++)
            Assert.Equal(villagers[i].Id, village.VillagerIds[i]);
    }

    // ── Stat rolling ──────────────────────────────────────────────────────────

    [Fact]
    public void Generate_VillagerStats_AllInValidRange()
    {
        var (_, villagers) = VillageGenerator.Generate(42u, _names);
        foreach (var v in villagers)
        {
            Assert.InRange(v.Stats.Str, 3, 18);
            Assert.InRange(v.Stats.Dex, 3, 18);
            Assert.InRange(v.Stats.Con, 3, 18);
            Assert.InRange(v.Stats.Wis, 3, 18);
        }
    }

    [Fact]
    public void Generate_BestOf3Rolls_MeanStrAbove12()
    {
        // Best-of-three 3d6 skews well above the straight-3d6 average (~10.5).
        // Over 100 seeds, mean Str across all villagers should exceed 12.
        double total = 0;
        int    n     = 0;
        for (uint seed = 0; seed < 100; seed++)
        {
            var (_, villagers) = VillageGenerator.Generate(seed, _names);
            foreach (var v in villagers) { total += v.Stats.Str; n++; }
        }
        double mean = total / n;
        Assert.True(mean > 12.0, $"Expected mean Str > 12, got {mean:F2}");
    }

    // ── Archetype derivation ──────────────────────────────────────────────────

    [Fact]
    public void Generate_ArchetypeTag_MatchesHighestStat()
    {
        // Over many seeds, every villager's archetype must correspond to their highest stat
        // (tie-break: Str > Con > Dex > Wis).
        for (uint seed = 0; seed < 20; seed++)
        {
            var (_, villagers) = VillageGenerator.Generate(seed, _names);
            foreach (var v in villagers)
            {
                var  s   = v.Stats;
                int  max = Math.Max(Math.Max(s.Str, s.Dex), Math.Max(s.Con, s.Wis));
                bool ok  = v.ArchetypeTag switch
                {
                    "archetype.woodcutter" => s.Str == max,
                    "archetype.laborer"    => s.Con == max && s.Str < max,
                    "archetype.guard"      => s.Dex == max && s.Str < max && s.Con < max,
                    "archetype.forager"    => s.Wis == max && s.Str < max && s.Con < max && s.Dex < max,
                    _                      => false
                };
                Assert.True(ok,
                    $"seed={seed} {v.Name}: archetype '{v.ArchetypeTag}' invalid for stats Str={s.Str} Dex={s.Dex} Con={s.Con} Wis={s.Wis}");
            }
        }
    }

    [Theory]
    [InlineData("archetype.woodcutter", "archetype.woodcutter.name")]
    [InlineData("archetype.forager",    "archetype.forager.name")]
    [InlineData("archetype.guard",      "archetype.guard.name")]
    [InlineData("archetype.laborer",    "archetype.laborer.name")]
    public void VillagerData_ArchetypeNameKey_FollowsLocConvention(string tag, string expectedKey)
    {
        // ArchetypeNameKey must be the tag + ".name" so Loc.T can resolve it.
        // Build a minimal VillagerData with a forced archetype by matching stat layout.
        StatBlock stats = tag switch
        {
            "archetype.woodcutter" => new StatBlock(18,  5,  5,  5),
            "archetype.laborer"    => new StatBlock( 5,  5, 18,  5),
            "archetype.guard"      => new StatBlock( 5, 18,  5,  5),
            _                      => new StatBlock( 5,  5,  5, 18) // forager
        };
        var data = new VillagerData("v.test", "Test", stats, 0f, 0f);
        Assert.Equal(tag, data.ArchetypeTag);
        Assert.Equal(expectedKey, data.ArchetypeNameKey);
    }

    // ── Names ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Generate_Names_NoDuplicatesWithinVillage()
    {
        var (_, villagers) = VillageGenerator.Generate(42u, _names);
        var names = villagers.Select(v => v.Name).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Fact]
    public void Generate_Names_DrawnFromSuppliedPool()
    {
        var pool = new HashSet<string>(_names);
        var (_, villagers) = VillageGenerator.Generate(42u, _names);
        foreach (var v in villagers)
            Assert.Contains(v.Name, pool);
    }

    // ── Determinism ───────────────────────────────────────────────────────────

    [Fact]
    public void Generate_Deterministic_SameSeedSameOutput()
    {
        var (v1, vs1) = VillageGenerator.Generate(99u, _names);
        var (v2, vs2) = VillageGenerator.Generate(99u, _names);

        Assert.Equal(v1.WorldX, v2.WorldX);
        Assert.Equal(v1.WorldZ, v2.WorldZ);
        Assert.Equal(vs1.Count,  vs2.Count);
        for (int i = 0; i < vs1.Count; i++)
        {
            Assert.Equal(vs1[i].Name,    vs2[i].Name);
            Assert.Equal(vs1[i].Stats,   vs2[i].Stats);
            Assert.Equal(vs1[i].WorldX,  vs2[i].WorldX);
            Assert.Equal(vs1[i].WorldZ,  vs2[i].WorldZ);
        }
    }

    [Fact]
    public void Generate_DifferentSeeds_DifferentVillagePositions()
    {
        var (v0, _) = VillageGenerator.Generate(0u,  _names);
        var (v1, _) = VillageGenerator.Generate(1u,  _names);
        var (v2, _) = VillageGenerator.Generate(42u, _names);

        // Extremely unlikely all three share the exact same floating-point position.
        bool allSame = v0.WorldX == v1.WorldX && v1.WorldX == v2.WorldX
                    && v0.WorldZ == v1.WorldZ && v1.WorldZ == v2.WorldZ;
        Assert.False(allSame);
    }

    // ── Villager IDs ──────────────────────────────────────────────────────────

    [Fact]
    public void Generate_VillagerIds_UniqueWithinVillage()
    {
        var (_, villagers) = VillageGenerator.Generate(42u, _names);
        var ids = villagers.Select(v => v.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }
}
