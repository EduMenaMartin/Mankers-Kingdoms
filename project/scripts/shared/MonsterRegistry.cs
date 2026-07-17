using System.Collections.Generic;

namespace MankersKingdoms.Shared;

/// <summary>
/// Static registry of all monster type definitions.
/// Hardcoded for v1; move to data/base/monsters/*.json when the content loader exists.
/// See IDEAS_BACKLOG.md — "Content loading from repo-root data/ via filesystem".
///
/// Combat stats authored per docs/gdd/combat.md §6:
///   Wolf (beast): flat AttackBonus + TargetNumber per §6.2 — no gear slot formula.
///   Goblin/Bandit/BanditArcher (humanoids): also flat-authored in Phase 4.8 for
///   simplicity; live stat+armor formula (§6.3) applies once Phase 6 wires gear slots.
///
/// TargetNumber = 10 + ArmorValue + DexMod (approximated from authored monster fiction).
/// DamageDice matches the SRD weapon or natural attack appropriate for the monster type.
/// </summary>
public static class MonsterRegistry
{
    /// <summary>
    /// Average-roll HP formula for humanoid monsters.
    /// HP = HD × ((dieSize+1)/2) + StatModifier(con) × HD.
    /// Uses combat.md §2.3 gentler curve: StatModifier = floor((con-10)/4).
    /// Beasts use flat authored MaxHp instead.
    /// </summary>
    private static float ComputeHp(int hd, int dieSize, int con) =>
        hd * ((dieSize + 1) / 2f) + CombatResolver.StatModifier(con) * hd;

    public static readonly IReadOnlyList<MonsterData> All = new[]
    {
        // ── Beasts ────────────────────────────────────────────────────────────
        // Wolf: fast pursuit predator. Flat authored stats per combat.md §6.2 example.
        //   TargetNumber 12 = natural agility, no armour (10 + 2 natural dodge).
        //   MaxHp flat (beast) — no HD/Con formula.
        new MonsterData(
            Id:             "monster.wolf",
            DisplayNameKey: "monster.wolf.name",
            MaxHp:          40f,
            AttackBonus:    3,
            TargetNumber:   12,
            DamageDice:     "1d6",
            DamageType:     "piercing",   // bite
            MoveSpeed:      5f,           // fast — rushes players
            AggroRange:     15f,
            AttackRange:    1.8f,
            AttackCooldown: 1.2f,
            LootTable:      ["resource.wood"]   // Phase 5: replace with hide/pelt item
        ),

        // ── Hostiles ─────────────────────────────────────────────────────────
        // Goblin: weak melee humanoid. 7 HD d8, Con 10 → MaxHp 31.5.
        //   Leather armour (ArmorValue 1). TargetNumber 11 = 10 + 1 + 0.
        new MonsterData(
            Id:             "monster.goblin",
            DisplayNameKey: "monster.goblin.name",
            MaxHp:          ComputeHp(7, 8, 10),   // 31.5
            AttackBonus:    2,
            TargetNumber:   11,
            DamageDice:     "1d6",
            DamageType:     "slashing",   // scimitar-level weapon
            MoveSpeed:      3.5f,
            AggroRange:     12f,
            AttackRange:    1.8f,
            AttackCooldown: 1.0f,
            LootTable:      ["resource.wood"],
            HitDiceCount:      7,
            HitDieSize:        8,
            ConstitutionScore: 10
        ),
        // Bandit: experienced melee fighter. 11 HD d8, Con 14 → MaxHp 60.5.
        //   Studded leather + slight Dex bonus. TargetNumber 13 = 10 + 2 + 1.
        new MonsterData(
            Id:             "monster.bandit",
            DisplayNameKey: "monster.bandit.name",
            MaxHp:          ComputeHp(11, 8, 14),   // 60.5
            AttackBonus:    3,
            TargetNumber:   13,
            DamageDice:     "1d8",
            DamageType:     "slashing",   // longsword-equivalent
            MoveSpeed:      3.5f,
            AggroRange:     18f,
            AttackRange:    2.0f,
            AttackCooldown: 1.5f,
            LootTable:      ["resource.wood", "item.arrow"],
            HitDiceCount:      11,
            HitDieSize:        8,
            ConstitutionScore: 14
        ),
        // Bandit Archer: trained ranged humanoid. 9 HD d8, Con 10 → MaxHp 40.5.
        //   Light leather, keeps distance. TargetNumber 12 = 10 + 1 + 1.
        //   Melee fallback: AttackBonus/DamageDice apply if player closes to melee range.
        new MonsterData(
            Id:             "monster.bandit_archer",
            DisplayNameKey: "monster.bandit_archer.name",
            MaxHp:          ComputeHp(9, 8, 10),   // 40.5
            AttackBonus:    3,
            TargetNumber:   12,
            DamageDice:     "1d6",
            DamageType:     "piercing",   // shortbow arrow
            MoveSpeed:      3f,
            AggroRange:     20f,
            AttackRange:    18f,          // ranged engagement distance
            AttackCooldown: 2.0f,
            IsRanged:       true,
            RangedWeaponId: "item.weapon.shortbow",
            LootTable:      ["item.arrow"],
            HitDiceCount:      9,
            HitDieSize:        8,
            ConstitutionScore: 10
        ),
        // Orc: elite heavy melee. 18 HD d8, Con 16 → MaxHp 99.
        //   Guards the Major raider nest (VERTICAL_SLICE.md §3.6).
        //   Heavy armour equivalent. TargetNumber 14 = 10 + 3 (heavy) + 1 (natural).
        //   AttackBonus 5 — veteran warrior. Large weapon (1d10 slashing).
        new MonsterData(
            Id:             "monster.orc",
            DisplayNameKey: "monster.orc.name",
            MaxHp:          ComputeHp(18, 8, 16),   // 99
            AttackBonus:    5,
            TargetNumber:   14,
            DamageDice:     "1d10",
            DamageType:     "slashing",   // greataxe
            MoveSpeed:      3.0f,
            AggroRange:     18f,
            AttackRange:    2.2f,
            AttackCooldown: 2.0f,
            LootTable:      ["resource.wood", "item.arrow"],
            HitDiceCount:      18,
            HitDieSize:        8,
            ConstitutionScore: 16
        ),
    };

    private static readonly Dictionary<string, MonsterData> _byId = new();

    static MonsterRegistry()
    {
        foreach (var m in All)
            _byId[m.Id] = m;
    }

    /// <summary>Returns the MonsterData for the given ID, or null if not found.</summary>
    public static MonsterData? Find(string id) =>
        _byId.TryGetValue(id, out var m) ? m : null;
}
