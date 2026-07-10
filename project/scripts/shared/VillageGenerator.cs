using System;
using System.Collections.Generic;

namespace MankersKingdoms.Shared;

/// <summary>
/// Generates the world's single village and its villagers deterministically from the world seed.
/// Pure C#, no Godot dependency — safe in xUnit and callable from both server and client.
///
/// Seed salt: WorldSeed XOR 0xB11A6E00u (mnemonic: "VILLAGe00"), independent of other generators.
///
/// Villager stat generation: best-of-three 3d6 for each stat independently.
/// This skews the distribution above the straight-3d6 average (~10.5) without changing the range
/// (3–18). Archetype is derived post-roll by VillagerData.ArchetypeTag (highest stat wins).
///
/// Names are drawn without replacement from the supplied pool using a seeded Fisher-Yates shuffle.
/// The caller (VillageSystem) loads the pool from res://data/base/villages/names.json.
/// Passing the pool as a parameter keeps this class testable without file I/O.
///
/// Village placement: one village at a random angle, 45–70 world units from origin.
/// Villagers scatter within ±SCATTER_RADIUS of the village centre.
///
/// Same pattern as NestGenerator and BushGenerator: both peers run Generate() independently;
/// identical seed → identical output → no position sync RPC needed.
/// </summary>
public static class VillageGenerator
{
    private const float VILLAGE_MIN_DIST = 45f;
    private const float VILLAGE_MAX_DIST = 70f;
    private const int   VILLAGER_MIN     = 6;
    private const int   VILLAGER_MAX     = 10;
    private const float SCATTER_RADIUS   = 6f;

    /// <summary>
    /// Generates one VillageData and the full VillagerData list for the given world seed.
    /// Requires at least VILLAGER_MAX (10) entries in namePool to guarantee unique names.
    /// </summary>
    public static (VillageData Village, IReadOnlyList<VillagerData> Villagers) Generate(
        uint worldSeed,
        IReadOnlyList<string> namePool)
    {
        var rng = new Random((int)(worldSeed ^ 0xB11A6E00u));

        // ── Village placement: random angle, fixed distance band from origin ────
        double angle = rng.NextDouble() * Math.PI * 2.0;
        float  dist  = VILLAGE_MIN_DIST + (float)(rng.NextDouble() * (VILLAGE_MAX_DIST - VILLAGE_MIN_DIST));
        float  vx    = (float)(Math.Cos(angle) * dist);
        float  vz    = (float)(Math.Sin(angle) * dist);

        // ── Name pool: Fisher-Yates shuffle so each run draws different names ───
        var shuffled = new List<string>(namePool);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        // ── Villager generation ──────────────────────────────────────────────────
        int count     = rng.Next(VILLAGER_MIN, VILLAGER_MAX + 1);
        var villagers = new List<VillagerData>(count);
        var ids       = new List<string>(count);

        for (int i = 0; i < count; i++)
        {
            string id   = $"villager.0.{i}";
            string name = shuffled[i % shuffled.Count]; // guard against tiny test pools

            // Scatter position (world XZ; terrain Y is snapped by VillageSystem)
            float wx = vx + (float)((rng.NextDouble() * 2.0 - 1.0) * SCATTER_RADIUS);
            float wz = vz + (float)((rng.NextDouble() * 2.0 - 1.0) * SCATTER_RADIUS);

            villagers.Add(new VillagerData(id, name, RollStats(rng), wx, wz));
            ids.Add(id);
        }

        return (new VillageData("village.0", vx, vz, ids), villagers);
    }

    // ── Stat rolling ──────────────────────────────────────────────────────────

    private static StatBlock RollStats(Random rng) => new(
        Str: BestOf3Rolls(rng),
        Dex: BestOf3Rolls(rng),
        Con: BestOf3Rolls(rng),
        Wis: BestOf3Rolls(rng));

    /// <summary>Roll 3d6 three independent times; return the highest sum (range 3–18).</summary>
    private static int BestOf3Rolls(Random rng)
    {
        int best = 0;
        for (int roll = 0; roll < 3; roll++)
        {
            int sum = rng.Next(1, 7) + rng.Next(1, 7) + rng.Next(1, 7);
            if (sum > best) best = sum;
        }
        return best;
    }
}
