using System.Collections.Generic;

namespace MankersKingdoms.Shared;

/// <summary>
/// Generates the world's nest layout deterministically from the world seed.
/// Pure C#, no Godot dependency — safe in xUnit and callable from both server and client.
///
/// Uses WorldSeed XOR 0xDEAD1234 salt, independent of tree/bush placement.
/// Both NestSystem (server) and MinimapHUD/WorldMapScreen (client) call this
/// with the same seed to get identical positions without any network sync.
/// Same pattern as TreeGenerator and BushGenerator.
/// </summary>
public static class NestGenerator
{
    private const float MIN_FROM_ORIGIN   = 15f;
    private const float MIN_NEST_DIST     = 25f;
    private const float MIN_FROM_VILLAGE  = 45f; // nests spawn away from the starting village
    public  const float MAP_HALF          = 96f; // ±96 world-units — safe terrain area

    /// <summary>
    /// Returns 3 nests deterministically placed for the given world seed (VERTICAL_SLICE.md §3.8):
    ///   1× wolf pack  (2 wolves,                            respawn 45 s, Minor)
    ///   1× goblin den (3 goblins,                           respawn 60 s, Minor)
    ///   1× raider camp (2 bandits + 1 archer + 1 orc elite, respawn 120 s, Major)
    ///
    /// Orc is included in the Major raider camp per §3.6 ("rare, guards higher-value nests").
    /// Minor nests show a small dot on maps; Major shows a large dot.
    /// </summary>
    public static IReadOnlyList<NestData> Generate(uint worldSeed)
    {
        var rng   = new System.Random((int)(worldSeed ^ 0xDEAD1234u));
        var nests = new List<NestData>();

        // Village centre — used to keep nests away from the player's starting area.
        var (vx, vz) = VillageGenerator.GetVillageCenter(worldSeed);

        (string[] types, float respawnSec, NestTier tier)[] templates =
        [
            (["monster.wolf",   "monster.wolf"],                                                          45f,  NestTier.Minor),
            (["monster.goblin", "monster.goblin", "monster.goblin"],                                     60f,  NestTier.Minor),
            (["monster.bandit", "monster.bandit", "monster.bandit_archer", "monster.orc"],              120f,  NestTier.Major),
        ];

        int id = 0;
        foreach (var (types, respawnSec, tier) in templates)
        {
            bool placed = false;
            for (int tries = 0; tries < 200 && !placed; tries++)
            {
                float x = (float)(rng.NextDouble() * MAP_HALF * 2 - MAP_HALF);
                float z = (float)(rng.NextDouble() * MAP_HALF * 2 - MAP_HALF);

                if (x * x + z * z < MIN_FROM_ORIGIN * MIN_FROM_ORIGIN) continue;

                float dvx = x - vx, dvz = z - vz;
                if (dvx * dvx + dvz * dvz < MIN_FROM_VILLAGE * MIN_FROM_VILLAGE) continue;

                bool tooClose = false;
                foreach (var n in nests)
                {
                    float dx = x - n.WorldX, dz = z - n.WorldZ;
                    if (dx * dx + dz * dz < MIN_NEST_DIST * MIN_NEST_DIST)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;

                nests.Add(new NestData(id++, types, x, z, respawnSec, Tier: tier));
                placed = true;
            }
        }

        return nests;
    }
}
