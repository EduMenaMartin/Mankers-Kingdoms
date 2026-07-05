# Mankers Kingdoms — Equipment Catalog (GDD)

**Status:** v0.1 — locked. Fills the gap flagged during `docs/gdd/combat.md` discussion: previously only a schema + 5 illustrative examples existed, no actual planned catalog. This document is that catalog.

**Source:** D&D SRD 5.1, Creative Commons Attribution 4.0 International License — same licensing precedent already relied on for the bestiary (`PRD.md` §4.8). Verified directly against the SRD's own armor and weapons tables, not reconstructed from memory. Core SRD content only — third-party/supplemental weapons (monk weapons, "Beyond Damage Dice" maneuvers, etc.) excluded from this catalog.

**Related:** `docs/gdd/combat.md` §4, §7, §11 (damage/armor formulas and schema), `docs/gdd/inventory.md` (item schema, shape/weight), ADR-0009 (data-driven content)

---

## 1. Naming collision — read first

The weapon property **"heavy"** (small creatures have disadvantage using it) and the armor **category "heavy"** (`docs/gdd/combat.md` §11.1, no Dex modifier) are unrelated homonyms from the source material — not a design error. Keep this distinction in mind when reading the tables below; a weapon's `heavy` property tag has nothing to do with an armor's `armor_category: heavy`.

---

## 2. Armor and Shields

Base AC values from the SRD are mapped to our schema as `armor_value = SRD_base_AC − 10`, so our existing formula (`docs/gdd/combat.md` §2.2: `10 + StatModifier + ArmorValue`) reproduces the same real AC numbers once combined with our category-based Dex handling (§11.1).

**Note:** our Medium category caps Dex modifier at +1 (not the SRD's +2) — a deliberate choice already locked in `combat.md` §11.1, proportional to our gentler stat-modifier curve (§2.3, range only −2 to +2 vs. the SRD's wider range). This is intentional, not a transcription error.

### 2.1 Light Armor (full Dex modifier applies)

| Item | armor_value | stealth_disadvantage | str_requirement | Weight | Notes |
|---|---|---|---|---|---|
| Padded | 1 | true | 0 | 8 lb | Rare case: light armor WITH stealth penalty (padding rustles) |
| Leather | 1 | false | 0 | 10 lb | |
| Studded Leather | 2 | false | 0 | 13 lb | |

### 2.2 Medium Armor (Dex modifier capped at +1, per our house rule)

| Item | armor_value | stealth_disadvantage | str_requirement | Weight | Notes |
|---|---|---|---|---|---|
| Hide | 2 | false | 0 | 12 lb | |
| Chain Shirt | 3 | false | 0 | 20 lb | |
| Scale Mail | 4 | true | 0 | 45 lb | |
| Breastplate | 4 | false | 0 | 20 lb | Best medium option: no stealth penalty |
| Half Plate | 5 | true | 0 | 40 lb | |

### 2.3 Heavy Armor (no Dex modifier applies)

| Item | armor_value | stealth_disadvantage | str_requirement | Weight | Notes |
|---|---|---|---|---|---|
| Ring Mail | 4 | true | 0 | 40 lb | Heavy but no Str requirement |
| Chain Mail | 6 | true | 13 | 55 lb | |
| Splint Mail | 7 | true | 15 | 60 lb | |
| Plate Mail | 8 | true | 15 | 65 lb | Best AC in the game |

### 2.4 Shield

| Item | shield_bonus | Weight | Notes |
|---|---|---|---|
| Shield | 2 | 6 lb | No stealth penalty or Str requirement in core rules |

---

## 3. Weapons

### 3.1 Simple Melee

| Item | damage_dice | damage_type | Weight | Properties |
|---|---|---|---|---|
| Club | 1d4 | bludgeoning | 2 lb | light |
| Dagger | 1d4 | piercing | 1 lb | finesse, light, thrown (20/60) |
| Greatclub | 1d8 | bludgeoning | 10 lb | two-handed |
| Handaxe | 1d6 | slashing | 2 lb | light, thrown (20/60) |
| Javelin | 1d6 | piercing | 2 lb | thrown (30/120) |
| Light hammer | 1d4 | bludgeoning | 2 lb | light, thrown (20/60) |
| Mace | 1d6 | bludgeoning | 4 lb | — |
| Quarterstaff | 1d6 | bludgeoning | 4 lb | versatile (1d8) |
| Sickle | 1d4 | slashing | 2 lb | light |
| Spear | 1d6 | piercing | 3 lb | thrown (20/60), versatile (1d8) |

### 3.2 Simple Ranged

| Item | damage_dice | damage_type | Weight | Range | Properties |
|---|---|---|---|---|---|
| Crossbow, light | 1d8 | piercing | 5 lb | 80/320 | ammunition, loading, two-handed |
| Dart | 1d4 | piercing | 0.25 lb | 20/60 | finesse, thrown |
| Shortbow | 1d6 | piercing | 2 lb | 80/320 | ammunition, two-handed |
| Sling | 1d4 | bludgeoning | — | 30/120 | ammunition |

### 3.3 Martial Melee

| Item | damage_dice | damage_type | Weight | Properties |
|---|---|---|---|---|
| Battleaxe | 1d8 | slashing | 4 lb | versatile (1d10) |
| Flail | 1d8 | bludgeoning | 2 lb | — |
| Glaive | 1d10 | slashing | 6 lb | heavy, reach, two-handed |
| Greataxe | 1d12 | slashing | 7 lb | heavy, two-handed |
| Greatsword | 2d6 | slashing | 6 lb | heavy, two-handed |
| Halberd | 1d10 | slashing | 6 lb | heavy, reach, two-handed |
| Lance | 1d12 | piercing | 6 lb | reach, special |
| **Longsword** | **1d8** | **slashing** | 3 lb | versatile (1d10) — **Fighter's locked starting weapon** |
| Maul | 2d6 | bludgeoning | 10 lb | heavy, two-handed |
| Morningstar | 1d8 | piercing | 4 lb | — |
| Pike | 1d10 | piercing | 18 lb | heavy, reach, two-handed |
| Rapier | 1d8 | piercing | 2 lb | finesse |
| Scimitar | 1d6 | slashing | 3 lb | finesse, light |
| Shortsword | 1d6 | piercing | 2 lb | finesse, light |
| Trident | 1d6 | piercing | 4 lb | thrown (20/60), versatile (1d8) |
| War pick | 1d8 | piercing | 2 lb | — |
| Warhammer | 1d8 | bludgeoning | 2 lb | versatile (1d10) |
| Whip | 1d4 | slashing | 3 lb | finesse, reach |

### 3.4 Martial Ranged

| Item | damage_dice | damage_type | Weight | Range | Properties |
|---|---|---|---|---|---|
| Blowgun | 1 | piercing | 1 lb | 25/100 | ammunition, loading |
| Hand crossbow | 1d6 | piercing | 3 lb | 30/120 | ammunition, light, loading |
| Heavy crossbow | 1d10 | piercing | 18 lb | 100/400 | ammunition, heavy, loading, two-handed |
| Longbow | 1d8 | piercing | 2 lb | 150/600 | ammunition, heavy, two-handed |
| Net | — | — | 3 lb | 5/15 | thrown, special (restrains, no damage) |

---

## 4. Property reference (not all mechanically implemented yet)

| Property | Meaning | Implemented in our combat system? |
|---|---|---|
| Finesse | Attacker's choice of Str or Dex modifier for the attack/damage roll | Not yet — open question, see §7 |
| Light | Usable for two-weapon fighting | Not yet modeled — no dual-wield system exists |
| Heavy (weapon) | Small creatures have disadvantage using it | Not yet relevant — no Small-sized playable races in v1 |
| Two-handed | Requires both hands | Not yet enforced — no hand-slot system exists |
| Versatile | Different damage die one-handed vs two-handed | Not yet — treat as one-handed value only for now |
| Reach | +5 feet reach | Not yet modeled in our geometry gate (`combat.md` §2.5) |
| Thrown | Can be thrown as a ranged attack | Partially relevant — ties to existing ranged combat (ADR-0013) but not formally connected yet |
| Ammunition | Requires ammo to fire | Already relevant — arrows already exist per `VERTICAL_SLICE.md` §3.4's crafting note |
| Loading | Only one shot per action regardless of attacks | Not yet relevant — no multi-attack system exists |

None of these gaps block v1 — Fighter/Ranger's locked kits (below) don't require any of them. Flagged so nobody assumes they're silently working.

---

## 5. v1 class kit mapping (confirms existing locks, no changes)

Per `VERTICAL_SLICE.md` §3.2's already-locked starting kits:

| Class | Weapon | Maps to catalog entry |
|---|---|---|
| Fighter | Sword + Shield | **Longsword** (§3.3) + **Shield** (§2.4) |
| Ranger | Shortbow + Hunting knife | **Shortbow** (§3.2) + **Dagger** (§3.1) — "hunting knife" is flavor-text for a Dagger, no separate SRD entry exists; this is a naming choice, not a new item |

Both classes' locked kits map cleanly onto existing SRD entries — no new items needed to satisfy what's already committed.

---

## 6. Data model recap

Combines fields from `docs/gdd/inventory.md` (base item schema) and `docs/gdd/combat.md` §7/§11.5 (combat-specific fields):

```json
{
  "id": "item.weapon.longsword",
  "display_name_key": "item.weapon.longsword.name",
  "category": "weapon",
  "shape": { "width": 1, "height": 3 },
  "weight": 3,
  "stackable": false,
  "damage_dice": "1d8",
  "damage_type": "slashing",
  "icon": "res://assets/items/longsword.png"
}
```

```json
{
  "id": "item.armor.chainmail",
  "display_name_key": "item.armor.chainmail.name",
  "category": "armor",
  "shape": { "width": 2, "height": 2 },
  "weight": 55,
  "stackable": false,
  "armor_value": 6,
  "armor_category": "heavy",
  "str_requirement": 13,
  "stealth_disadvantage": true,
  "shield_bonus": 0,
  "icon": "res://assets/items/chainmail.png"
}
```

`shape` fields are included now for forward compatibility with `docs/gdd/inventory.md`'s Phase B (shape-based grid) even though Phase A (current, simple grid) doesn't use them yet — same "design the seam now" pattern used elsewhere. Exact shape values above are placeholders pending Phase B's actual implementation.

---

## 7. Modding surface

Every item in this catalog is a stable-ID data entry per ADR-0009 — modders can add new weapons/armor by authoring the same fields, no code changes required. The full SRD catalog beyond what v1 actually equips (most martial weapons, several armor tiers) exists in data now specifically so post-slice class expansion (PRD §10, more classes at 1.0) can draw on it without re-authoring content from scratch.

---

## 8. Open questions

1. **Finesse property** — do we want a Dex-based melee option (rewarding Ranger-style Dex-fighters using daggers/rapiers/scimitars) or keep Melee strictly Str-governed per `docs/gdd/skills.md`'s current lock? Not blocking v1 (Fighter/Ranger's kits don't need it), but relevant once more classes arrive.
2. **Versatile/two-handed/reach mechanics** — none currently modeled in the geometry gate or damage formula. Low priority until a weapon requiring them actually gets equipped in gameplay.
3. **Economy/cost values** — SRD gold-piece costs weren't carried into the schema above (no economy system locked yet — ties to ADR-0015's roadmapped economic layer). Add a `cost` field when that system is designed.
4. **Full vs. subset content loading** — should the entire catalog above load into `data/base/items/` from day one, or only the subset v1 actually equips (Longsword, Shield, Shortbow, Dagger)? Recommend loading the full catalog now — it's free (just data) and avoids a re-authoring pass later when more classes/weapons matter.
