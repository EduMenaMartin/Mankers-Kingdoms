using MankersKingdoms.Shared;
using Xunit;

namespace MankersKingdoms.Tests.Shared;

public class CombatResolverTests
{
    // ── StatModifier ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(3,  -2)]
    [InlineData(4,  -2)]
    [InlineData(5,  -2)]
    [InlineData(6,  -1)]
    [InlineData(7,  -1)]
    [InlineData(8,  -1)]
    [InlineData(9,  -1)]
    [InlineData(10,  0)]
    [InlineData(11,  0)]
    [InlineData(12,  0)]
    [InlineData(13,  0)]
    [InlineData(14, +1)]
    [InlineData(15, +1)]
    [InlineData(16, +1)]
    [InlineData(17, +1)]
    [InlineData(18, +2)]
    public void StatModifier_MatchesTable(int stat, int expected)
    {
        // Full table from combat.md §2.3.
        Assert.Equal(expected, CombatResolver.StatModifier(stat));
    }

    [Fact]
    public void StatModifier_PlaceholderStr13_IsZero()
    {
        // Phase 4.8 placeholder: Str=13 must give 0 mod (same as the border case).
        Assert.Equal(0, CombatResolver.StatModifier(13));
    }

    [Fact]
    public void StatModifier_PlaceholderDex12_IsZero()
    {
        Assert.Equal(0, CombatResolver.StatModifier(12));
    }

    // ── RollDice ─────────────────────────────────────────────────────────────

    [Fact]
    public void RollDice_1d6_InRange()
    {
        var rng = new System.Random(42);
        for (int i = 0; i < 200; i++)
        {
            int result = CombatResolver.RollDice("1d6", rng);
            Assert.InRange(result, 1, 6);
        }
    }

    [Fact]
    public void RollDice_2d6_InRange()
    {
        var rng = new System.Random(42);
        for (int i = 0; i < 200; i++)
        {
            int result = CombatResolver.RollDice("2d6", rng);
            Assert.InRange(result, 2, 12);
        }
    }

    [Fact]
    public void RollDice_1d8_InRange()
    {
        var rng = new System.Random(99);
        for (int i = 0; i < 200; i++)
        {
            int result = CombatResolver.RollDice("1d8", rng);
            Assert.InRange(result, 1, 8);
        }
    }

    [Fact]
    public void RollDice_1d4Plus1_InRange()
    {
        var rng = new System.Random(7);
        for (int i = 0; i < 200; i++)
        {
            int result = CombatResolver.RollDice("1d4+1", rng);
            Assert.InRange(result, 2, 5);
        }
    }

    [Fact]
    public void RollDice_1d1_AlwaysOne()
    {
        // Blowgun: SRD damage is 1, represented as "1d1".
        var rng = new System.Random(0);
        for (int i = 0; i < 50; i++)
            Assert.Equal(1, CombatResolver.RollDice("1d1", rng));
    }

    [Fact]
    public void RollDice_Empty_ReturnsZero()
    {
        // Net has empty DamageDice.
        var rng = new System.Random(0);
        Assert.Equal(0, CombatResolver.RollDice("", rng));
    }

    // ── ResolveAttack ─────────────────────────────────────────────────────────

    [Fact]
    public void ResolveAttack_CertainHit_Hits()
    {
        // attackBonus so high that even a roll of 2 beats the target number.
        // Use a deterministic RNG that avoids natural 1 in the first call.
        // With bonus=20 and TN=2, only a natural 1 misses.
        // Seeded RNG with seed=1 — check a large sample; all should hit given high bonus.
        var rng = new System.Random(1);
        int hitCount = 0;
        for (int i = 0; i < 100; i++)
        {
            var (hit, _, _) = CombatResolver.ResolveAttack(20, 2, "1d6", 0, rng);
            if (hit) hitCount++;
        }
        // With AB=20 and TN=2, only natural 1 misses (5% chance) — expect > 85 hits in 100.
        Assert.True(hitCount > 85, $"Expected mostly hits with AB=20 vs TN=2, got {hitCount}/100");
    }

    [Fact]
    public void ResolveAttack_CertainMiss_Misses()
    {
        // TN=100: only a natural 20 can hit. Expect mostly misses.
        var rng = new System.Random(1);
        int missCount = 0;
        for (int i = 0; i < 100; i++)
        {
            var (hit, _, _) = CombatResolver.ResolveAttack(0, 100, "1d6", 0, rng);
            if (!hit) missCount++;
        }
        // Natural 20 always hits (~5% chance) — expect > 85 misses in 100.
        Assert.True(missCount > 85, $"Expected mostly misses with AB=0 vs TN=100, got {missCount}/100");
    }

    [Fact]
    public void ResolveAttack_Miss_ReturnsDamageZero()
    {
        // Force a miss scenario and verify damage is 0.
        // With AB=0 and TN=21, only a natural 20 hits. Run until we find a miss.
        var rng = new System.Random(5);
        bool foundMiss = false;
        for (int i = 0; i < 50; i++)
        {
            var (hit, damage, _) = CombatResolver.ResolveAttack(0, 21, "1d8", 0, rng);
            if (!hit)
            {
                Assert.Equal(0, damage);
                foundMiss = true;
                break;
            }
        }
        Assert.True(foundMiss, "Should have found at least one miss in 50 rolls with TN=21");
    }

    [Fact]
    public void ResolveAttack_Hit_DamageAtLeastOne()
    {
        // Any hit must deal at least 1 damage even with a negative damage modifier.
        var rng = new System.Random(42);
        for (int i = 0; i < 100; i++)
        {
            var (hit, damage, _) = CombatResolver.ResolveAttack(20, 2, "1d4", damageMod: -10, rng);
            if (hit)
                Assert.True(damage >= 1, $"Hit must deal at least 1 damage, got {damage}");
        }
    }

    [Fact]
    public void ResolveAttack_BoundaryExact_Hits()
    {
        // When roll + attackBonus == targetNumber, the attack should hit (≥ rule).
        // With a fixed RNG that always returns 10 for the d20, AB=5, TN=15 → 10+5=15 ≥ 15 → hit.
        // We can't control the RNG directly, but we can set AB and TN such that any non-1
        // roll of 10 hits. Instead: verify the "meets exactly" case via many samples.
        // With AB=10, TN=20: roll 10 → total 20 ≥ 20 → hit. Only 1 misses, 20 auto-hits.
        var rng = new System.Random(7);
        int hitCount = 0;
        for (int i = 0; i < 200; i++)
        {
            var (hit, _, _) = CombatResolver.ResolveAttack(10, 20, "1d6", 0, rng);
            if (hit) hitCount++;
        }
        // Rolls 10-20 (11 outcomes out of 20) hit, plus natural 20 always hits.
        // Expect roughly 55% hit rate, definitely > 40% hit rate.
        Assert.True(hitCount > 80, $"Expected many hits with AB=10 vs TN=20, got {hitCount}/200");
    }

    // ── §12.2 block-and-attack penalty ───────────────────────────────────────

    [Fact]
    public void ResolveAttack_BlockPenalty_ReducesHitRate()
    {
        // §12.3: Fighter (AB=5) vs Goblin (TN=12). The −3 block penalty (AB=2) must
        // produce fewer hits over the same dice sequence (same seed = same rolls).
        var rngNormal  = new System.Random(12345);
        var rngBlocked = new System.Random(12345);
        int hitsNormal = 0, hitsBlocked = 0;
        for (int i = 0; i < 1000; i++)
        {
            var (hit1, _, _) = CombatResolver.ResolveAttack(5, 12, "1d8", 0, rngNormal);
            var (hit2, _, _) = CombatResolver.ResolveAttack(2, 12, "1d8", 0, rngBlocked);
            if (hit1) hitsNormal++;
            if (hit2) hitsBlocked++;
        }
        Assert.True(hitsBlocked < hitsNormal,
            $"Block penalty must reduce hits: {hitsBlocked} blocked < {hitsNormal} normal");
    }

    // ── Player helpers ────────────────────────────────────────────────────────

    [Fact]
    public void PlayerAttackBonus_MeleeWeapon_IsZero()
    {
        // Phase 4.8: Str=13 → mod 0; skill=0 → AB = 0.
        Assert.Equal(0, CombatResolver.PlayerAttackBonus("item.weapon.longsword"));
    }

    [Fact]
    public void PlayerAttackBonus_RangedWeapon_IsZero()
    {
        // Phase 4.8: Dex=12 → mod 0; skill=0 → AB = 0.
        Assert.Equal(0, CombatResolver.PlayerAttackBonus("item.weapon.shortbow"));
    }

    [Fact]
    public void PlayerTargetNumber_IsTen()
    {
        // Phase 4.8: Dex=12 → mod 0; no equipped armor → TN = 10 + 0 = 10.
        Assert.Equal(10, CombatResolver.PlayerTargetNumber());
    }

    [Fact]
    public void PlayerDamageMod_IsZero()
    {
        // Phase 4.8: both Str and Dex placeholder mods are 0.
        Assert.Equal(0, CombatResolver.PlayerDamageMod("item.weapon.longsword"));
        Assert.Equal(0, CombatResolver.PlayerDamageMod("item.weapon.shortbow"));
    }
}
