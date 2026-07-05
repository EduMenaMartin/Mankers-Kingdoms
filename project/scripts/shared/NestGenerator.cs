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
    private const float MIN_FROM_ORIGIN = 15f;
    private const float MIN_NEST_DIST   = 25f;
    public  const float MAP_HALF        = 96f; // ±96 world-units — safe terrain area

    /// <summary>
    /// Returns 5 nests deterministically placed for the given world seed:
    ///   2× wolf pack   (2 wolves,             respawn 45 s)
    ///   2× goblin group (3 goblins,            respawn 60 s)
    ///   1× bandit camp  (2 bandits + 1 archer, respawn 90 s)
    /// </summary>
    public static IReadOnlyList<NestData> Generate(uint worldSeed)
    {
        var rng   = new System.Random((int)(worldSeed ^ 0xDEAD1234u));
        var nests = new List<NestData>();

        (string[] types, float respawnSec)[] templates =
        [
            (["monster.wolf",   "monster.wolf"],                                     45f),
            (["monster.wolf",   "monster.wolf"],                                     45f),
            (["monster.goblin", "monster.goblin", "monster.goblin"],                60f),
            (["monster.goblin", "monster.goblin", "monster.goblin"],                60f),
            (["monster.bandit", "monster.bandit", "monster.bandit_archer"],         90f),
        ];

        int id = 0;
        foreach (var (types, respawnSec) in templates)
        {
            bool placed = false;
            for (int tries = 0; tries < 200 && !placed; tries++)
            {
                float x = (float)(rng.NextDouble() * MAP_HALF * 2 - MAP_HALF);
                float z = (float)(rng.NextDouble() * MAP_HALF * 2 - MAP_HALF);

                if (x * x + z * z < MIN_FROM_ORIGIN * MIN_FROM_ORIGIN) continue;

                bool tooClose = false;
                foreach (var n in nests)
                {
                    float dx = x - n.WorldX, dz = z - n.WorldZ;
                    if (dx * dx + dz * dz < MIN_NEST_DIST * MIN_NEST_DIST) { tooClose = true; break; }
                }
                if (tooClose) continue;

                nests.Add(new NestData(id++, types, x, z, respawnSec));
                placed = true;
            }
        }

        return nests;
    }
}
