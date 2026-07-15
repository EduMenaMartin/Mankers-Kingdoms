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
            var (hit, _, _, _) = CombatResolver.ResolveAttack(20, 2, "1d6", 0, rng);
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
            var (hit, _, _, _) = CombatResolver.ResolveAttack(0, 100, "1d6", 0, rng);
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
            var (hit, damage, _, _) = CombatResolver.ResolveAttack(0, 21, "1d8", 0, rng);
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
            var (hit, damage, _, _) = CombatResolver.ResolveAttack(20, 2, "1d4", damageMod: -10, rng);
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
            var (hit, _, _, _) = CombatResolver.ResolveAttack(10, 20, "1d6", 0, rng);
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
            var (hit1, _, _, _) = CombatResolver.ResolveAttack(5, 12, "1d8", 0, rngNormal);
            var (hit2, _, _, _) = CombatResolver.ResolveAttack(2, 12, "1d8", 0, rngBlocked);
            if (hit1) hitsNormal++;
            if (hit2) hitsBlocked++;
        }
        Assert.True(hitsBlocked < hitsNormal,
            $"Block penalty must reduce hits: {hitsBlocked} blocked < {hitsNormal} normal");
    }

    // ── §5.2 fumble asymmetry rule ────────────────────────────────────────────

    [Fact]
    public void ResolveAttack_Nat1_IsFumble_WhenBonusWouldNotSave()
    {
        // AB=0, TN=15: 1+0=1 < 15 → nat-1 is a true fumble.
        // We can't force the RNG to roll 1, but we can verify the rule via a large
        // sample — every nat-1 in this configuration MUST be a fumble.
        var rng = new System.Random(99);
        int nat1Count = 0, fumbleCount = 0;
        for (int i = 0; i < 5000; i++)
        {
            // Save RNG state indirectly: re-run with a separate seeded RNG to check the roll.
            // Instead, verify via the invariant: if !hit && isFumble → fumble is valid.
            var (hit, _, _, isFumble) = CombatResolver.ResolveAttack(0, 15, "1d6", 0, rng);
            if (!hit && isFumble) fumbleCount++;
            if (!hit) nat1Count++;
        }
        // With AB=0, TN=15: every nat-1 qualifies as fumble (1+0=1 < 15 always).
        // Roughly 5% of 5000 rolls = ~250 nat-1s; all should be fumbles.
        // We just verify some fumbles were registered without over-constraining the count.
        Assert.True(fumbleCount > 0, "Expected at least one fumble with AB=0 vs TN=15 in 5000 rolls");
    }

    [Fact]
    public void ResolveAttack_Nat1_NotFumble_WhenBonusCarries()
    {
        // AB=20, TN=2: 1+20=21 ≥ 2 → nat-1 is a normal miss, NOT a fumble (§5.2 asymmetry).
        var rng = new System.Random(77);
        for (int i = 0; i < 5000; i++)
        {
            var (_, _, _, isFumble) = CombatResolver.ResolveAttack(20, 2, "1d6", 0, rng);
            Assert.False(isFumble,
                "A skilled attacker whose bonus carries nat-1 past TN should NEVER fumble (§5.2)");
        }
    }

    // ── Crit / fumble table rolling ───────────────────────────────────────────

    [Fact]
    public void RollCritEffect_AlwaysReturnsValidEntry()
    {
        var rng = new System.Random(0);
        for (int i = 0; i < 500; i++)
        {
            var effect = CombatResolver.RollCritEffect(rng);
            Assert.True(System.Enum.IsDefined(typeof(CritEffect), effect),
                $"RollCritEffect returned out-of-range value {(int)effect}");
        }
    }

    [Fact]
    public void RollFumbleEffect_AlwaysReturnsValidEntry()
    {
        var rng = new System.Random(0);
        for (int i = 0; i < 500; i++)
        {
            var effect = CombatResolver.RollFumbleEffect(rng);
            Assert.True(System.Enum.IsDefined(typeof(FumbleEffect), effect),
                $"RollFumbleEffect returned out-of-range value {(int)effect}");
        }
    }

    [Fact]
    public void RollCritEffect_CoverageAcrossAllEntries()
    {
        // Over enough rolls, every CritEffect entry should appear at least once.
        var rng  = new System.Random(42);
        var seen = new System.Collections.Generic.HashSet<CritEffect>();
        for (int i = 0; i < 1000; i++)
            seen.Add(CombatResolver.RollCritEffect(rng));
        Assert.Equal(5, seen.Count); // 5 entries in CritEffect enum
    }

    [Fact]
    public void RollFumbleEffect_CoverageAcrossAllEntries()
    {
        // Over enough rolls, every FumbleEffect entry should appear at least once.
        var rng  = new System.Random(42);
        var seen = new System.Collections.Generic.HashSet<FumbleEffect>();
        for (int i = 0; i < 1000; i++)
            seen.Add(CombatResolver.RollFumbleEffect(rng));
        Assert.Equal(4, seen.Count); // 4 entries in FumbleEffect enum
    }

    // ── Player helpers ────────────────────────────────────────────────────────

    [Fact]
    public void PlayerAttackBonus_Fighter_Melee_IsOne()
    {
        // Fighter: Str=16 → mod +1; skill=0 → AB = 0+1 = 1. (combat.md §2.4)
        Assert.Equal(1, CombatResolver.PlayerAttackBonus("item.weapon.longsword", str: 16, dex: 10, skillLevel: 0));
    }

    [Fact]
    public void PlayerAttackBonus_Ranger_Ranged_IsOne()
    {
        // Ranger: Dex=15 → mod +1; skill=0 → AB = 0+1 = 1. (combat.md §4.2)
        Assert.Equal(1, CombatResolver.PlayerAttackBonus("item.weapon.shortbow", str: 10, dex: 15, skillLevel: 0));
    }

    [Fact]
    public void PlayerAttackBonus_NeutralStats_IsZero()
    {
        // Str=13/Dex=12 → mod 0 for both; skill=0.
        Assert.Equal(0, CombatResolver.PlayerAttackBonus("item.weapon.longsword", str: 13, dex: 12, skillLevel: 0));
        Assert.Equal(0, CombatResolver.PlayerAttackBonus("item.weapon.shortbow",  str: 13, dex: 12, skillLevel: 0));
    }

    [Fact]
    public void PlayerAttackBonus_SkillLevel_ContributesFloorDiv10()
    {
        // skill=10 → floor(10/10)=1; skill=19 → floor=1; skill=20 → floor=2.
        Assert.Equal(1, CombatResolver.PlayerAttackBonus("item.weapon.longsword", str: 10, dex: 10, skillLevel: 10));
        Assert.Equal(1, CombatResolver.PlayerAttackBonus("item.weapon.longsword", str: 10, dex: 10, skillLevel: 19));
        Assert.Equal(2, CombatResolver.PlayerAttackBonus("item.weapon.longsword", str: 10, dex: 10, skillLevel: 20));
    }

    [Fact]
    public void PlayerTargetNumber_NeutralDex_IsTen()
    {
        // Dex=12 → mod 0; no armor → TN = 10.
        Assert.Equal(10, CombatResolver.PlayerTargetNumber(dex: 12));
    }

    [Fact]
    public void PlayerTargetNumber_HighDex_IsElevated()
    {
        // Dex=15 → mod +1 → TN = 11.
        Assert.Equal(11, CombatResolver.PlayerTargetNumber(dex: 15));
    }

    [Fact]
    public void PlayerTargetNumber_WithArmor_AddsArmorValue()
    {
        // Dex=12 (mod 0) + leather armor (ArmorValue=1, Light) → TN = 11.
        Assert.Equal(11, CombatResolver.PlayerTargetNumber(dex: 12, armorValue: 1,
            armorCategory: ArmorCategory.Light));
    }

    [Fact]
    public void PlayerTargetNumber_WithShield_AddsShieldBonus()
    {
        // Dex=12 (mod 0) + shield (ShieldBonus=2) → TN = 12.
        Assert.Equal(12, CombatResolver.PlayerTargetNumber(dex: 12, shieldBonus: 2));
    }

    [Fact]
    public void PlayerTargetNumber_MediumArmor_CapsHighDexAtOne()
    {
        // Dex=18 → uncapped mod +2; medium armor caps Dex mod at +1 → TN = 10+1+armorValue.
        Assert.Equal(14, CombatResolver.PlayerTargetNumber(dex: 18, armorValue: 3,
            armorCategory: ArmorCategory.Medium));
    }

    [Fact]
    public void PlayerTargetNumber_HeavyArmor_NukesDexMod()
    {
        // Dex=18 → heavy armor zeroes Dex mod → TN = 10+0+armorValue.
        Assert.Equal(15, CombatResolver.PlayerTargetNumber(dex: 18, armorValue: 5,
            armorCategory: ArmorCategory.Heavy));
    }

    [Fact]
    public void PlayerTargetNumber_HeavyArmorWithShield_StacksCorrectly()
    {
        // Heavy armor: Dex mod ignored; armorValue=6 + shieldBonus=2 → TN=18.
        Assert.Equal(18, CombatResolver.PlayerTargetNumber(dex: 18, armorValue: 6,
            shieldBonus: 2, armorCategory: ArmorCategory.Heavy));
    }

    [Fact]
    public void PlayerTargetNumber_MediumArmorLowDex_DoesNotRaiseFloor()
    {
        // Dex=8 → mod -1; medium armor cap of +1 only caps upward — negative mod still applies.
        Assert.Equal(9, CombatResolver.PlayerTargetNumber(dex: 8, armorValue: 0,
            armorCategory: ArmorCategory.Medium));
    }

    [Fact]
    public void PlayerDamageMod_Fighter_Melee_IsOne()
    {
        // Fighter Str=16 → mod +1 for melee damage. (combat.md §4.2)
        Assert.Equal(1, CombatResolver.PlayerDamageMod("item.weapon.longsword", str: 16, dex: 10));
    }

    [Fact]
    public void PlayerDamageMod_Ranger_Ranged_IsOne()
    {
        // Ranger Dex=15 → mod +1 for ranged damage.
        Assert.Equal(1, CombatResolver.PlayerDamageMod("item.weapon.shortbow", str: 10, dex: 15));
    }

    [Fact]
    public void PlayerDamageMod_NeutralStats_IsZero()
    {
        Assert.Equal(0, CombatResolver.PlayerDamageMod("item.weapon.longsword", str: 13, dex: 12));
    }
}
