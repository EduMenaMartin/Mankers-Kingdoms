using System.Collections.Generic;

namespace MankersKingdoms.Shared;

/// <summary>
/// Static registry of all armor and shield definitions.
/// Hardcoded for v1; move to data/base/items/armor/*.json when the content loader exists.
/// See IDEAS_BACKLOG.md — "Content loading from repo-root data/ via filesystem".
///
/// Values sourced from D&D SRD 5.1 (CC-BY 4.0) per docs/gdd/equipment.md §2.
/// armor_value = SRD_base_AC − 10, fitting docs/gdd/combat.md §2.2 formula.
/// Medium Dex cap is +1 (our house rule, not the SRD's +2) — see equipment.md §2.
/// </summary>
public static class ArmorRegistry
{
    public static readonly IReadOnlyList<ArmorData> All = new[]
    {
        // ── Light Armor (full Dex modifier applies) ───────────────────────────
        // Padded: rare case of light armor with stealth penalty (padding rustles).
        new ArmorData("item.armor.padded",          "item.armor.padded.name",          ArmorValue: 1, ArmorCategory: ArmorCategory.Light,  StrRequirement:  0, StealthDisadvantage: true,  ShieldBonus: 0, Weight:  8f),
        new ArmorData("item.armor.leather",         "item.armor.leather.name",         ArmorValue: 1, ArmorCategory: ArmorCategory.Light,  StrRequirement:  0, StealthDisadvantage: false, ShieldBonus: 0, Weight: 10f),
        new ArmorData("item.armor.studded_leather", "item.armor.studded_leather.name", ArmorValue: 2, ArmorCategory: ArmorCategory.Light,  StrRequirement:  0, StealthDisadvantage: false, ShieldBonus: 0, Weight: 13f),

        // ── Medium Armor (Dex modifier capped at +1) ──────────────────────────
        new ArmorData("item.armor.hide",            "item.armor.hide.name",            ArmorValue: 2, ArmorCategory: ArmorCategory.Medium, StrRequirement:  0, StealthDisadvantage: false, ShieldBonus: 0, Weight: 12f),
        new ArmorData("item.armor.chain_shirt",     "item.armor.chain_shirt.name",     ArmorValue: 3, ArmorCategory: ArmorCategory.Medium, StrRequirement:  0, StealthDisadvantage: false, ShieldBonus: 0, Weight: 20f),
        new ArmorData("item.armor.scale_mail",      "item.armor.scale_mail.name",      ArmorValue: 4, ArmorCategory: ArmorCategory.Medium, StrRequirement:  0, StealthDisadvantage: true,  ShieldBonus: 0, Weight: 45f),
        // Breastplate: best medium option — full ArmorValue with no stealth penalty.
        new ArmorData("item.armor.breastplate",     "item.armor.breastplate.name",     ArmorValue: 4, ArmorCategory: ArmorCategory.Medium, StrRequirement:  0, StealthDisadvantage: false, ShieldBonus: 0, Weight: 20f),
        new ArmorData("item.armor.half_plate",      "item.armor.half_plate.name",      ArmorValue: 5, ArmorCategory: ArmorCategory.Medium, StrRequirement:  0, StealthDisadvantage: true,  ShieldBonus: 0, Weight: 40f),

        // ── Heavy Armor (no Dex modifier applies) ─────────────────────────────
        new ArmorData("item.armor.ring_mail",       "item.armor.ring_mail.name",       ArmorValue: 4, ArmorCategory: ArmorCategory.Heavy,  StrRequirement:  0, StealthDisadvantage: true,  ShieldBonus: 0, Weight: 40f),
        new ArmorData("item.armor.chain_mail",      "item.armor.chain_mail.name",      ArmorValue: 6, ArmorCategory: ArmorCategory.Heavy,  StrRequirement: 13, StealthDisadvantage: true,  ShieldBonus: 0, Weight: 55f),
        new ArmorData("item.armor.splint_mail",     "item.armor.splint_mail.name",     ArmorValue: 7, ArmorCategory: ArmorCategory.Heavy,  StrRequirement: 15, StealthDisadvantage: true,  ShieldBonus: 0, Weight: 60f),
        // Plate Mail: highest ArmorValue in the game.
        new ArmorData("item.armor.plate_mail",      "item.armor.plate_mail.name",      ArmorValue: 8, ArmorCategory: ArmorCategory.Heavy,  StrRequirement: 15, StealthDisadvantage: true,  ShieldBonus: 0, Weight: 65f),

        // ── Shield ────────────────────────────────────────────────────────────
        // Contributes ShieldBonus to the Target Number; does not provide ArmorValue.
        // ArmorCategory.Shield: off-hand item — Dex modifier handling does not apply.
        new ArmorData("item.armor.shield",          "item.armor.shield.name",          ArmorValue: 0, ArmorCategory: ArmorCategory.Shield,  StrRequirement:  0, StealthDisadvantage: false, ShieldBonus: 2, Weight:  6f),
    };

    private static readonly Dictionary<string, ArmorData> _byId = new();

    static ArmorRegistry()
    {
        foreach (var a in All)
            _byId[a.Id] = a;
    }

    /// <summary>Returns the ArmorData for the given ID, or null if not found.</summary>
    public static ArmorData? Find(string id) =>
        _byId.TryGetValue(id, out var a) ? a : null;
}
