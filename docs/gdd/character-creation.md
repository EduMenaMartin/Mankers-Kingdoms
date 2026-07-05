# Mankers Kingdoms — Character Creation (GDD)

**Status:** v0.1 — locked for v1. Race system added to vertical slice scope per Edu's direction, verified against the original D&D Stronghold (1993)'s actual character creation flow.

**Related:** `PRD.md` §4.4, §10; `VERTICAL_SLICE.md` §3.2; `docs/gdd/skills.md` (stat caps, ADR-0019); `docs/gdd/combat.md` (stat modifiers feed combat formulas); ADR-0019, ADR-0023 (this feature)

---

## 1. Historical grounding

Verified directly against the original D&D Stronghold's game manual and period sales material, not assumed. The original used **race and class as separate, orthogonal choices** — not race-as-class:

> "Players first choose between four races (dwarves, elves, halflings or humans) and then name five leaders... Players may also roll for their leaders' abilities in areas such as strength, intelligence, charisma and dexterity, which will help determine each character's class: cleric, mage, fighter or thief."

The manual's table of contents confirms the flow order: **Attributes → Races & Classes → Alignment → Name Your Character → Place Your Castle → Create Party Members** — roll stats first, then race, then class informed by rolled stats. Race also shaped recruitment ("leaders attract others of the same race to your kingdom") and building access, alongside class — consistent with what `docs/gdd/settlements.md` already does with class-based presence-gating.

We adopt the **first four races**: Human, Dwarf, Elf, Halfling. More races (Gnome, Half-Elf, etc.) are roadmap, not v1 — confirmed easy to add later since the system is just a modifier table (§3).

We keep our **already-locked class list** (Fighter, Ranger) rather than reverting to the original's Cleric/Mage/Fighter/Thief — Thief's melee niche is already covered by Fighter, magic is deferred (ADR-0006), and more classes arrive later per `PRD.md` §10's open question 2. Race layers onto our existing class trajectory, not the original's.

---

## 2. Character creation flow

1. **Roll Attributes** — 3d6 straight, once per stat (Strength, Dexterity, Constitution, Wisdom — the 4 stats locked in `PRD.md` §4.4). **This locks player stat generation to the same method already used for NPCs** (`docs/gdd/skills.md`), resolving `PRD.md` §10's previously-open question on player stat rolling.
2. **Choose Race** — Human, Dwarf, Elf, or Halfling. Applies the racial modifier (§3) to the rolled stats.
3. **Choose Class** — Fighter or Ranger, unchanged from `VERTICAL_SLICE.md` §3.2. Provides starting kit + skill bumps, unaffected by race.
4. **Name Character.**
5. **Unlimited rerolls** before commit (already locked); character is permanent once entering the world.

---

## 3. Racial modifiers

Applied to the 3d6-rolled stat **before** it feeds skill caps (ADR-0019) or combat formulas (`docs/gdd/combat.md`).

| Race | Bonus | Penalty |
|---|---|---|
| **Human** | +1 to one stat of the player's choice at creation (recommended — versatility theme; open to revisiting, see §9) | none |
| **Dwarf** | +1 Constitution | −1 Charisma |
| **Elf** | +1 Dexterity | −1 Constitution |
| **Halfling** | +1 Dexterity | −1 Strength |

These are the classic AD&D 1st/2nd Edition conventions — consistent with Stronghold's own confirmed use of "First Edition D&D Rules" (established during `docs/gdd/combat.md`'s research).

### 3.1 Worked example

A player rolls Str 14, Dex 13, Con 12, Wis 10, picks **Dwarf**:
- Adjusted: Str 14, Dex 13, **Con 13** (+1), **Cha n/a (we don't use Cha in v1's 4-stat set — see Open Questions §9)**
- This adjusted Con 13 now feeds their skill cap (ADR-0019: `floor(99×13/18)` = 71 for any Con-governed skill) and their combat Target Number (`docs/gdd/combat.md` §2.2, if they're a defender).

**Note:** since v1 only uses Str/Dex/Con/Wis (not Charisma), the Dwarf's −1 Cha and any other Cha-related penalty currently has **no mechanical effect** until Charisma is added at 1.0 (per `PRD.md` §10's open question on expanding to the full six stats). Document this rather than silently drop it — see §9.

---

## 4. Race × Class combinations (v1)

4 races × 2 classes = **8 playable combinations** for the vertical slice: Human/Dwarf/Elf/Halfling Fighter, and Human/Dwarf/Elf/Halfling Ranger.

---

## 5. NPC extension

Procedural village NPCs (`docs/gdd/skills.md` §4 — hidden archetype + rolled stats) also receive a race at generation, using the same modifier table. This is a natural, low-cost extension of the already-locked NPC generation system — one more generation parameter, not a new system. Enables recruitment texture consistent with the original's spirit (a village might skew Dwarf-heavy, appealing for Con-driven Trades recruitment) without adding new mechanics.

**Not included in v1:** race-matched recruitment bonuses (e.g. same-race NPCs join more easily) or race-gated buildings (alongside the existing class-gated ones). Both are natural extensions but explicitly deferred to roadmap — see §8.

---

## 6. Data model

```json
{
  "id": "race.dwarf",
  "display_name_key": "race.dwarf.name",
  "stat_modifiers": { "con": 1, "cha": -1 },
  "player_choice_modifier": false
}
```

```json
{
  "id": "race.human",
  "display_name_key": "race.human.name",
  "stat_modifiers": {},
  "player_choice_modifier": true,
  "player_choice_amount": 1
}
```

Stable string IDs per ADR-0009, stored in `data/base/races/*.json`.

---

## 7. Visual differentiation

Deferred to art pass — not blocking the mechanic. For the prototype, even a scale or color-tint variation on the existing placeholder capsule is sufficient (Halfling shorter, Dwarf stockier, Elf slimmer). Distinct models are a later content task, consistent with how the rest of v1 treats placeholder art.

---

## 8. Roadmap (post-slice, not scheduled)

- Additional races (Gnome, Half-Elf, Half-Orc, etc.) — confirmed easy to add given the system is just a modifier-table lookup.
- Race-gated buildings, alongside the existing class-gated system in `docs/gdd/settlements.md`.
- Race-matched recruitment bonuses (loyalty/happiness bonus for recruiting same-race NPCs into a same-race-founded settlement) — ties to the original's "leaders attract others of the same race" flavor.
- Charisma-dependent racial penalties (Dwarf's −1 Cha, etc.) become mechanically live once Charisma is added at 1.0 (per `PRD.md` §10's open question on expanding to the full six stats).

---

## 9. Open questions

1. **Human's bonus** — is "+1 to one stat of player's choice" the right call, or should Human simply have no modifier at all (purely flavor-neutral, simplest possible baseline)? Recommended the former for parity of interest with the other three races, not locked.
2. **Cha-penalty dormancy** — Dwarf's −1 Charisma currently does nothing mechanically since v1 doesn't use Cha. Acceptable to ship as-is (documented, dormant) or should Dwarf get penalized on a stat we DO use instead, for v1 specifically? Leaning toward "ship as documented, activates naturally at 1.0" — simplest, avoids reworking the table twice.
3. **Racial trait beyond stats** — should any race get a small non-stat trait (e.g. Elf: slightly larger vision radius / minimap range, Dwarf: bonus to a specific Trade skill like Mining) similar to how classes get skill bumps, or keep races purely stat-modifier-based for v1 simplicity? Leaning toward "stats only for v1," non-stat traits as a roadmap richness pass.
