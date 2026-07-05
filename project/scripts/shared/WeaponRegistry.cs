using System.Collections.Generic;

namespace MankersKingdoms.Shared;

/// <summary>
/// Static registry of all weapon definitions — full SRD 5.1 catalog.
/// Hardcoded for v1; move to data/base/items/weapons/*.json when the content loader exists.
/// See IDEAS_BACKLOG.md — "Content loading from repo-root data/ via filesystem".
///
/// Values sourced from D&D SRD 5.1 (CC-BY 4.0) per docs/gdd/equipment.md §3.
/// DamageDice and DamageType match the SRD exactly.
/// Range (metres) and SwingCooldown (seconds) are authored gameplay values.
///
/// Shield is an armor item in ArmorRegistry ("item.armor.shield"), not a weapon.
/// Full catalog loaded now so post-slice class additions draw on it without re-authoring
/// (docs/gdd/equipment.md §7).
/// </summary>
public static class WeaponRegistry
{
    public static readonly IReadOnlyList<WeaponData> All = new[]
    {
        // ── Simple Melee ───────────────────────────────────────────────────────
        new WeaponData(Id: "item.weapon.club",          DisplayNameKey: "item.weapon.club.name",          DamageDice: "1d4",  DamageType: "bludgeoning", Range: 1.5f, SwingCooldown: 0.7f),
        new WeaponData(Id: "item.weapon.dagger",        DisplayNameKey: "item.weapon.dagger.name",        DamageDice: "1d4",  DamageType: "piercing",    Range: 1.5f, SwingCooldown: 0.5f),
        new WeaponData(Id: "item.weapon.greatclub",     DisplayNameKey: "item.weapon.greatclub.name",     DamageDice: "1d8",  DamageType: "bludgeoning", Range: 1.8f, SwingCooldown: 1.2f),
        new WeaponData(Id: "item.weapon.handaxe",       DisplayNameKey: "item.weapon.handaxe.name",       DamageDice: "1d6",  DamageType: "slashing",    Range: 1.5f, SwingCooldown: 0.6f),
        new WeaponData(Id: "item.weapon.javelin",       DisplayNameKey: "item.weapon.javelin.name",       DamageDice: "1d6",  DamageType: "piercing",    Range: 1.5f, SwingCooldown: 0.8f),
        new WeaponData(Id: "item.weapon.light_hammer",  DisplayNameKey: "item.weapon.light_hammer.name",  DamageDice: "1d4",  DamageType: "bludgeoning", Range: 1.5f, SwingCooldown: 0.6f),
        new WeaponData(Id: "item.weapon.mace",          DisplayNameKey: "item.weapon.mace.name",          DamageDice: "1d6",  DamageType: "bludgeoning", Range: 1.5f, SwingCooldown: 0.8f),
        new WeaponData(Id: "item.weapon.quarterstaff",  DisplayNameKey: "item.weapon.quarterstaff.name",  DamageDice: "1d6",  DamageType: "bludgeoning", Range: 1.7f, SwingCooldown: 0.8f),
        new WeaponData(Id: "item.weapon.sickle",        DisplayNameKey: "item.weapon.sickle.name",        DamageDice: "1d4",  DamageType: "slashing",    Range: 1.5f, SwingCooldown: 0.6f),
        new WeaponData(Id: "item.weapon.spear",         DisplayNameKey: "item.weapon.spear.name",         DamageDice: "1d6",  DamageType: "piercing",    Range: 1.8f, SwingCooldown: 0.8f),

        // ── Simple Ranged ──────────────────────────────────────────────────────
        new WeaponData(Id: "item.weapon.crossbow_light", DisplayNameKey: "item.weapon.crossbow_light.name", DamageDice: "1d8", DamageType: "piercing",    Range: 25f, SwingCooldown: 2.5f, IsRanged: true, ProjectileSpeed: 30f, AmmoItemId: "item.bolt"),
        new WeaponData(Id: "item.weapon.dart",           DisplayNameKey: "item.weapon.dart.name",           DamageDice: "1d4", DamageType: "piercing",    Range: 15f, SwingCooldown: 0.7f, IsRanged: true, ProjectileSpeed: 18f),
        new WeaponData(Id: "item.weapon.shortbow",       DisplayNameKey: "item.weapon.shortbow.name",       DamageDice: "1d6", DamageType: "piercing",    Range: 30f, SwingCooldown: 1.5f, IsRanged: true, ProjectileSpeed: 22f, AmmoItemId: "item.arrow"),
        new WeaponData(Id: "item.weapon.sling",          DisplayNameKey: "item.weapon.sling.name",          DamageDice: "1d4", DamageType: "bludgeoning", Range: 20f, SwingCooldown: 1.2f, IsRanged: true, ProjectileSpeed: 25f, AmmoItemId: "item.bullet"),

        // ── Martial Melee ──────────────────────────────────────────────────────
        new WeaponData(Id: "item.weapon.battleaxe",   DisplayNameKey: "item.weapon.battleaxe.name",   DamageDice: "1d8",  DamageType: "slashing",    Range: 1.7f, SwingCooldown: 0.9f),
        new WeaponData(Id: "item.weapon.flail",       DisplayNameKey: "item.weapon.flail.name",       DamageDice: "1d8",  DamageType: "bludgeoning", Range: 1.6f, SwingCooldown: 0.9f),
        new WeaponData(Id: "item.weapon.glaive",      DisplayNameKey: "item.weapon.glaive.name",      DamageDice: "1d10", DamageType: "slashing",    Range: 2.0f, SwingCooldown: 1.2f),
        new WeaponData(Id: "item.weapon.greataxe",    DisplayNameKey: "item.weapon.greataxe.name",    DamageDice: "1d12", DamageType: "slashing",    Range: 1.8f, SwingCooldown: 1.3f),
        new WeaponData(Id: "item.weapon.greatsword",  DisplayNameKey: "item.weapon.greatsword.name",  DamageDice: "2d6",  DamageType: "slashing",    Range: 1.8f, SwingCooldown: 1.2f),
        new WeaponData(Id: "item.weapon.halberd",     DisplayNameKey: "item.weapon.halberd.name",     DamageDice: "1d10", DamageType: "slashing",    Range: 2.0f, SwingCooldown: 1.2f),
        new WeaponData(Id: "item.weapon.lance",       DisplayNameKey: "item.weapon.lance.name",       DamageDice: "1d12", DamageType: "piercing",    Range: 2.5f, SwingCooldown: 1.5f),
        // Longsword: Fighter's locked starting weapon (docs/gdd/equipment.md §5).
        new WeaponData(Id: "item.weapon.longsword",   DisplayNameKey: "item.weapon.longsword.name",   DamageDice: "1d8",  DamageType: "slashing",    Range: 1.8f, SwingCooldown: 0.8f),
        new WeaponData(Id: "item.weapon.maul",        DisplayNameKey: "item.weapon.maul.name",        DamageDice: "2d6",  DamageType: "bludgeoning", Range: 1.8f, SwingCooldown: 1.4f),
        new WeaponData(Id: "item.weapon.morningstar", DisplayNameKey: "item.weapon.morningstar.name", DamageDice: "1d8",  DamageType: "piercing",    Range: 1.5f, SwingCooldown: 0.9f),
        new WeaponData(Id: "item.weapon.pike",        DisplayNameKey: "item.weapon.pike.name",        DamageDice: "1d10", DamageType: "piercing",    Range: 2.0f, SwingCooldown: 1.3f),
        new WeaponData(Id: "item.weapon.rapier",      DisplayNameKey: "item.weapon.rapier.name",      DamageDice: "1d8",  DamageType: "piercing",    Range: 1.7f, SwingCooldown: 0.7f),
        new WeaponData(Id: "item.weapon.scimitar",    DisplayNameKey: "item.weapon.scimitar.name",    DamageDice: "1d6",  DamageType: "slashing",    Range: 1.5f, SwingCooldown: 0.6f),
        new WeaponData(Id: "item.weapon.shortsword",  DisplayNameKey: "item.weapon.shortsword.name",  DamageDice: "1d6",  DamageType: "piercing",    Range: 1.5f, SwingCooldown: 0.6f),
        new WeaponData(Id: "item.weapon.trident",     DisplayNameKey: "item.weapon.trident.name",     DamageDice: "1d6",  DamageType: "piercing",    Range: 1.7f, SwingCooldown: 0.9f),
        new WeaponData(Id: "item.weapon.war_pick",    DisplayNameKey: "item.weapon.war_pick.name",    DamageDice: "1d8",  DamageType: "piercing",    Range: 1.5f, SwingCooldown: 0.9f),
        new WeaponData(Id: "item.weapon.warhammer",   DisplayNameKey: "item.weapon.warhammer.name",   DamageDice: "1d8",  DamageType: "bludgeoning", Range: 1.6f, SwingCooldown: 0.9f),
        new WeaponData(Id: "item.weapon.whip",        DisplayNameKey: "item.weapon.whip.name",        DamageDice: "1d4",  DamageType: "slashing",    Range: 2.0f, SwingCooldown: 0.7f),

        // ── Martial Ranged ─────────────────────────────────────────────────────
        // Blowgun: SRD damage is a fixed "1" (not a die) — represented as 1d1.
        new WeaponData(Id: "item.weapon.blowgun",        DisplayNameKey: "item.weapon.blowgun.name",        DamageDice: "1d1",  DamageType: "piercing", Range: 10f, SwingCooldown: 1.5f, IsRanged: true, ProjectileSpeed: 15f, AmmoItemId: "item.needle"),
        new WeaponData(Id: "item.weapon.crossbow_hand",  DisplayNameKey: "item.weapon.crossbow_hand.name",  DamageDice: "1d6",  DamageType: "piercing", Range: 15f, SwingCooldown: 2.0f, IsRanged: true, ProjectileSpeed: 20f, AmmoItemId: "item.bolt"),
        new WeaponData(Id: "item.weapon.crossbow_heavy", DisplayNameKey: "item.weapon.crossbow_heavy.name", DamageDice: "1d10", DamageType: "piercing", Range: 35f, SwingCooldown: 3.0f, IsRanged: true, ProjectileSpeed: 35f, AmmoItemId: "item.bolt"),
        // Longbow: shares "item.arrow" ammo with the Shortbow.
        new WeaponData(Id: "item.weapon.longbow",        DisplayNameKey: "item.weapon.longbow.name",        DamageDice: "1d8",  DamageType: "piercing", Range: 40f, SwingCooldown: 1.8f, IsRanged: true, ProjectileSpeed: 28f, AmmoItemId: "item.arrow"),
        // Net: no damage (DamageDice empty); restraint effect is roadmap.
        new WeaponData(Id: "item.weapon.net",            DisplayNameKey: "item.weapon.net.name",            DamageDice: "",     DamageType: "special",  Range:  5f, SwingCooldown: 2.0f, IsRanged: true, ProjectileSpeed:  8f),
    };

    private static readonly Dictionary<string, WeaponData> _byId = new();

    static WeaponRegistry()
    {
        foreach (var w in All)
            _byId[w.Id] = w;
    }

    /// <summary>Returns the WeaponData for the given ID, or null if not found.</summary>
    public static WeaponData? Find(string id) =>
        _byId.TryGetValue(id, out var w) ? w : null;
}
