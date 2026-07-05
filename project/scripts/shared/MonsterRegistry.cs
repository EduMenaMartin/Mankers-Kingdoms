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
    public static readonly IReadOnlyList<MonsterData> All = new[]
    {
        // ── Beasts ────────────────────────────────────────────────────────────
        // Wolf: fast pursuit predator. Flat authored stats per combat.md §6.2 example.
        //   TargetNumber 12 = natural agility, no armour (10 + 2 natural dodge).
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
        // Goblin: weak melee humanoid. Leather armour equivalent (ArmorValue 1).
        //   TargetNumber 11 = 10 + 1 (leather) + 0 (low Dex mod).
        new MonsterData(
            Id:             "monster.goblin",
            DisplayNameKey: "monster.goblin.name",
            MaxHp:          30f,
            AttackBonus:    2,
            TargetNumber:   11,
            DamageDice:     "1d6",
            DamageType:     "slashing",   // scimitar-level weapon
            MoveSpeed:      3.5f,
            AggroRange:     12f,
            AttackRange:    1.8f,
            AttackCooldown: 1.0f,
            LootTable:      ["resource.wood"]
        ),
        // Bandit: experienced melee fighter. Studded leather + slight Dex bonus.
        //   TargetNumber 13 = 10 + 2 (studded leather) + 1 (Dex mod).
        new MonsterData(
            Id:             "monster.bandit",
            DisplayNameKey: "monster.bandit.name",
            MaxHp:          60f,
            AttackBonus:    3,
            TargetNumber:   13,
            DamageDice:     "1d8",
            DamageType:     "slashing",   // longsword-equivalent
            MoveSpeed:      3.5f,
            AggroRange:     18f,
            AttackRange:    2.0f,
            AttackCooldown: 1.5f,
            LootTable:      ["resource.wood", "item.arrow"]
        ),
        // Bandit Archer: trained ranged humanoid. Light leather, keeps distance.
        //   TargetNumber 12 = 10 + 1 (leather) + 1 (Dex mod).
        //   DamageDice matches item.weapon.shortbow (1d6 piercing).
        //   Melee fallback: AttackBonus/DamageDice apply if player closes to melee range.
        new MonsterData(
            Id:             "monster.bandit_archer",
            DisplayNameKey: "monster.bandit_archer.name",
            MaxHp:          40f,
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
            LootTable:      ["item.arrow"]
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
