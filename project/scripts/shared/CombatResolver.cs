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
/// Phase 4.8 — placeholder player stats (Option A, decided 2026-07-05):
///   PLACEHOLDER_STR = 13, PLACEHOLDER_DEX = 12 — both give StatModifier = 0.
///   Skill levels are 0 — attack bonus is 0 for both placeholder stats.
///   Real stats and trained skill levels wired in Phase 6 when class kits land.
///
/// All randomness goes through the caller's seeded System.Random (ADR-0022).
/// This class has no Godot dependency and is fully testable in xUnit.
/// See docs/gdd/combat.md and ADR-0022 for background.
/// </summary>
public static class CombatResolver
{
    // ── Placeholder player stats (Phase 4.8 Option A) ─────────────────────────
    // Replace with real per-player CharacterData stats in Phase 6.
    private const int PLACEHOLDER_STR = 13;
    private const int PLACEHOLDER_DEX = 12;

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
    ///   natural  1 → always miss (fumble table deferred to Phase A).
    ///   otherwise  → hit if (roll + attackBonus) ≥ targetNumber.
    ///
    /// On hit returns (true, damage, isCrit) with minimum 1 damage.
    /// On miss returns (false, 0, false).
    /// isCrit is true only on a natural 20.
    /// </summary>
    public static (bool hit, int damage, bool isCrit) ResolveAttack(
        int           attackBonus,
        int           targetNumber,
        string        damageDice,
        int           damageMod,
        System.Random rng)
    {
        int roll = rng.Next(1, 21); // 1d20
        bool isCrit = (roll == 20);

        bool hit;
        if      (roll == 20) hit = true;
        else if (roll ==  1) hit = false;
        else                 hit = (roll + attackBonus) >= targetNumber;

        if (!hit) return (false, 0, false);

        int damage = RollDice(damageDice, rng) + damageMod;
        if (isCrit && !string.IsNullOrEmpty(damageDice))
            damage += RollDice(damageDice, rng); // critical: roll damage dice a second time

        return (true, System.Math.Max(1, damage), isCrit);
    }

    // ── Player helpers (Phase 4.8 placeholders) ───────────────────────────────

    /// <summary>
    /// Player's attack bonus for Phase 4.8.
    /// Formula: floor(SkillLevel / 10) + StatModifier(GoverningStat).
    /// Both skill (0) and stat modifier (0 for Str 13 / Dex 12) yield 0.
    /// Wire real per-player stats and skill levels in Phase 6.
    /// </summary>
    public static int PlayerAttackBonus(string weaponId)
    {
        // Melee → Strength; Ranged → Dexterity (combat.md §2.2).
        var weapon = WeaponRegistry.Find(weaponId);
        int stat   = (weapon?.IsRanged == true) ? PLACEHOLDER_DEX : PLACEHOLDER_STR;
        return 0 + StatModifier(stat); // skill=0; stat modifier = 0 for both placeholders
    }

    /// <summary>
    /// Player's Target Number for Phase 4.8.
    /// Formula: 10 + StatModifier(Dex) + ArmorValue + ShieldBonus.
    /// Dex=12 → mod 0; no equipment slots tracked yet → ArmorValue=0, ShieldBonus=0.
    /// Result: 10. Wire real equipped-armor lookup in Phase 6.
    /// </summary>
    public static int PlayerTargetNumber() => 10 + StatModifier(PLACEHOLDER_DEX);

    /// <summary>
    /// Player's damage modifier for Phase 4.8.
    /// Melee → StatModifier(Str); Ranged → StatModifier(Dex).
    /// Returns 0 for both placeholder values. Wire in Phase 6.
    /// </summary>
    public static int PlayerDamageMod(string weaponId)
    {
        var weapon = WeaponRegistry.Find(weaponId);
        int stat   = (weapon?.IsRanged == true) ? PLACEHOLDER_DEX : PLACEHOLDER_STR;
        return StatModifier(stat);
    }
}
