namespace MankersKingdoms.Shared;

/// <summary>
/// Pure-C# dice resolver for the hybrid d20 attack system (docs/gdd/combat.md §2).
///
/// StatModifier: gentler curve floor((stat-10)/4) per combat.md §2.3.
///   Table: 3-5 → -2, 6-9 → -1, 10-13 → 0, 14-17 → +1, 18 → +2.
///
/// RollDice: rolls XdY or XdY+Z notation. Returns 0 for empty string (Net, no damage).
///
/// ResolveAttack: rolls 1d20 + attackBonus vs targetNumber.
///   Natural 20 → always hit + critical (roll damage dice twice, combat.md §5.4 Phase A).
///   Natural  1 → always miss (fumble effect table deferred to Phase A, not in Phase 4.8).
///   On hit: damage = RollDice(damageDice) + damageMod, minimum 1.
///   On miss: damage = 0.
///
/// Player stat helpers accept explicit (str, dex) parameters sourced from ClassKitData.
/// CombatSystem stores per-peer stats and passes them through; skill levels remain 0
/// until the skills system is wired in a later milestone.
///
/// All randomness goes through the caller's seeded System.Random (ADR-0022).
/// This class has no Godot dependency and is fully testable in xUnit.
/// See docs/gdd/combat.md and ADR-0022 for background.
/// </summary>
public static class CombatResolver
{

    // ── Stat modifier ─────────────────────────────────────────────────────────

    /// <summary>
    /// Gentler stat modifier: floor((stat − 10) / 4).
    /// Uses explicit floor division — C# integer division truncates toward zero,
    /// which gives the wrong answer for negative values (e.g. -7/4 = -1, not -2).
    /// </summary>
    public static int StatModifier(int stat)
    {
        int n = stat - 10;
        return n >= 0 ? n / 4 : (n - 3) / 4;
    }

    // ── Dice rolling ──────────────────────────────────────────────────────────

    /// <summary>
    /// Rolls dice from XdY or XdY+Z notation (e.g. "1d8", "2d6", "1d4+1").
    /// Returns 0 for null/empty input (used by Net weapon which deals no damage).
    /// </summary>
    public static int RollDice(string notation, System.Random rng)
    {
        if (string.IsNullOrEmpty(notation)) return 0;

        int dIdx = notation.IndexOf('d');
        if (dIdx < 0) return 0;

        if (!int.TryParse(notation[..dIdx], out int count) || count < 1) count = 1;

        var tail    = notation[(dIdx + 1)..];
        int plusIdx = tail.IndexOf('+');
        var sideStr = plusIdx >= 0 ? tail[..plusIdx] : tail;

        if (!int.TryParse(sideStr, out int sides) || sides < 1) return 0;
        int bonus = plusIdx >= 0 && int.TryParse(tail[(plusIdx + 1)..], out int b) ? b : 0;

        int total = bonus;
        for (int i = 0; i < count; i++)
            total += rng.Next(1, sides + 1);
        return total;
    }

    // ── Attack resolution ─────────────────────────────────────────────────────

    /// <summary>
    /// Resolves one attack attempt (combat.md §2.2 hybrid model).
    ///
    ///   roll = 1d20
    ///   natural 20 → always hit; critical: roll damageDice twice.
    ///   natural  1 → always miss; fumble if (1 + attackBonus) &lt; targetNumber (§5.2 asymmetry).
    ///   otherwise  → hit if (roll + attackBonus) ≥ targetNumber.
    ///
    /// On hit  returns (true,  damage ≥ 1, isCrit, false).
    /// On miss returns (false, 0,          false,   isFumble).
    /// isCrit  = natural 20.
    /// isFumble = natural 1 AND the bonus would not have saved the roll (§5.2 asymmetry rule:
    ///   a skilled attacker whose bonus carries nat-1 past TN does NOT fumble — just misses).
    /// </summary>
    public static (bool hit, int damage, bool isCrit, bool isFumble) ResolveAttack(
        int           attackBonus,
        int           targetNumber,
        string        damageDice,
        int           damageMod,
        System.Random rng)
    {
        int roll = rng.Next(1, 21); // 1d20
        bool isCrit = (roll == 20);
        // §5.2 asymmetry: fumble only when nat-1 AND the bonus wouldn't have saved it.
        bool isFumble = (roll == 1) && (1 + attackBonus < targetNumber);

        bool hit;
        if      (roll == 20) hit = true;
        else if (roll ==  1) hit = false;
        else                 hit = (roll + attackBonus) >= targetNumber;

        if (!hit) return (false, 0, false, isFumble);

        int damage = RollDice(damageDice, rng) + damageMod;
        if (isCrit && !string.IsNullOrEmpty(damageDice))
            damage += RollDice(damageDice, rng); // critical: roll damage dice a second time

        return (true, System.Math.Max(1, damage), isCrit, false);
    }

    // ── Crit / fumble effect tables ───────────────────────────────────────────

    /// <summary>
    /// Randomly selects one of the five critical-hit effects (combat.md §5.4 Phase A).
    /// Equal-weight for v1; weighting is a balancing pass.
    /// </summary>
    public static CritEffect RollCritEffect(System.Random rng) =>
        (CritEffect)rng.Next(0, 5);

    /// <summary>
    /// Randomly selects one of the four fumble complications (combat.md §5.4 Phase A).
    /// Equal-weight for v1; weighting is a balancing pass.
    /// </summary>
    public static FumbleEffect RollFumbleEffect(System.Random rng) =>
        (FumbleEffect)rng.Next(0, 4);

    // ── Player helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Player's attack bonus.
    /// Formula: floor(SkillLevel / 10) + StatModifier(GoverningStat).
    /// Melee → Strength; Ranged → Dexterity (combat.md §2.2).
    /// </summary>
    public static int PlayerAttackBonus(string weaponId, int str, int dex, int skillLevel)
    {
        var weapon = WeaponRegistry.Find(weaponId);
        int stat   = (weapon?.IsRanged == true) ? dex : str;
        return skillLevel / 10 + StatModifier(stat);
    }

    /// <summary>
    /// Player's Target Number (combat.md §2.2 + inventory.md §10.2).
    /// Formula: 10 + StatModifier(Dex, capped by armor category) + ArmorValue + ShieldBonus.
    /// ArmorValue and ShieldBonus read from the player's equipped slots by CombatSystem.
    /// ArmorCategory caps the Dex modifier per combat.md §11.1.
    /// </summary>
    public static int PlayerTargetNumber(
        int           dex,
        int           armorValue    = 0,
        int           shieldBonus   = 0,
        ArmorCategory armorCategory = ArmorCategory.Light)
    {
        int dexMod = StatModifier(dex);
        // §11.1: armor category caps how much Dex contributes to defense.
        dexMod = armorCategory switch
        {
            ArmorCategory.Medium => System.Math.Min(dexMod, 1),
            ArmorCategory.Heavy  => 0,
            _                    => dexMod  // Light: full modifier
        };
        return 10 + dexMod + armorValue + shieldBonus;
    }

    /// <summary>
    /// Player's damage modifier.
    /// Melee → StatModifier(Str); Ranged → StatModifier(Dex). (combat.md §4)
    /// </summary>
    public static int PlayerDamageMod(string weaponId, int str, int dex)
    {
        var weapon = WeaponRegistry.Find(weaponId);
        int stat   = (weapon?.IsRanged == true) ? dex : str;
        return StatModifier(stat);
    }
}
