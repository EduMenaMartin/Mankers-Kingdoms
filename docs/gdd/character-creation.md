# Mankers Kingdoms — Character Creation (GDD)

**Status:** v0.1 — locked for v1. Race system, stat-rolling revision, alignment, and racial/class traits all confirmed. Visual customization deferred to its own stub (`docs/gdd/character_customization.md`).

**Related:** `PRD.md` §4.4, §10; `VERTICAL_SLICE.md` §3.2; `docs/gdd/skills.md` (stat caps, ADR-0019); `docs/gdd/combat.md` (stat modifiers, Saving Throws §16, class traits §17); ADR-0019, ADR-0023

---

## 1. Historical grounding

Verified directly against the original D&D Stronghold's game manual and period sales material, not assumed. The original used **race and class as separate, orthogonal choices** — not race-as-class:

> "Players first choose between four races (dwarves, elves, halflings or humans) and then name five leaders... Players may also roll for their leaders' abilities in areas such as strength, intelligence, charisma and dexterity, which will help determine each character's class: cleric, mage, fighter or thief."

The manual's table of contents confirms the flow order: **Attributes → Races & Classes → Alignment → Name Your Character → Place Your Castle → Create Party Members** — roll stats first, then race, then class informed by rolled stats. Race also shaped recruitment ("leaders attract others of the same race to your kingdom") and building access, alongside class — consistent with what `docs/gdd/settlements.md` already does with class-based presence-gating.

We adopt the **first four races**: Human, Dwarf, Elf, Halfling. More races (Gnome, Half-Elf, etc.) are roadmap, not v1 — confirmed easy to add later since the system is just a modifier table (§3).

We keep our **already-locked class list** (Fighter, Ranger) rather than reverting to the original's Cleric/Mage/Fighter/Thief — Thief's melee niche is already covered by Fighter, magic is deferred (ADR-0006), and more classes arrive later per `PRD.md` §10's open question 2. Race layers onto our existing class trajectory, not the original's.

---

## 2. Character creation flow

1. **Roll Attributes** — see §10 for the current (revised) rolling method.
2. **Choose Race** — Human, Dwarf, Elf, or Halfling. Applies the racial modifier (§3) to the rolled stats, plus the racial traits in §12.
3. **Choose Class** — Fighter or Ranger, unchanged from `VERTICAL_SLICE.md` §3.2. Provides starting kit + skill bumps, plus the class traits in §12.
4. **Choose Alignment** — Lawful / Neutral / Chaotic, see §11.
5. **Name Character.**
6. **Unlimited rerolls** before commit; character is permanent once entering the world.

---

## 3. Racial modifiers (stat bonuses/penalties)

Applied to the rolled stat **before** it feeds skill caps (ADR-0019) or combat formulas (`docs/gdd/combat.md`).

| Race | Bonus | Penalty |
|---|---|---|
| **Human** | +1 to one stat of the player's choice at creation | none |
| **Dwarf** | +1 Constitution | −1 Charisma |
| **Elf** | +1 Dexterity | −1 Constitution |
| **Halfling** | +1 Dexterity | −1 Strength |

These are the classic AD&D 1st/2nd Edition conventions — consistent with Stronghold's own confirmed use of "First Edition D&D Rules."

### 3.1 Worked example

A player rolls Str 14, Dex 13, Con 12, Wis 10, picks **Dwarf**:
- Adjusted: Str 14, Dex 13, **Con 13** (+1)
- Since v1 only uses Str/Dex/Con/Wis (not Charisma), Dwarf's −1 Cha currently has **no mechanical effect** until Charisma is added at 1.0 — dormant, not dropped, per §9.

---

## 4. Race × Class combinations (v1)

4 races × 2 classes = **8 playable combinations** for the vertical slice.

---

## 5. NPC extension

Procedural village NPCs (`docs/gdd/skills.md` §4) also receive a race at generation, using the same modifier table (§3) — but **NPCs use flat single 3d6 rolling**, not the player's best-of-3 method (§10). This is an intentional asymmetry, not an oversight.

**Not included in v1:** race-matched recruitment bonuses or race-gated buildings. Both are natural extensions, deferred to roadmap (§8).

---

## 6. Data model

```json
{
  "id": "race.dwarf",
  "display_name_key": "race.dwarf.name",
  "stat_modifiers": { "con": 1, "cha": -1 },
  "player_choice_modifier": false,
  "saving_throw_bonuses": { "poison": 2, "magic": 2 },
  "combat_bonus_vs": { "monster.goblin": 2, "monster.orc": 2 }
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

Full design in `docs/gdd/character_customization.md` (stub, see §13). For the prototype, even a scale or color-tint variation on the existing placeholder capsule/model is sufficient.

---

## 8. Roadmap (post-slice, not scheduled)

- Additional races (Gnome, Half-Elf, Half-Orc, etc.)
- Race-gated buildings, alongside the existing class-gated system in `docs/gdd/settlements.md`
- Race-matched recruitment bonuses
- Charisma-dependent racial penalties become mechanically live once Charisma is added at 1.0
- Elf's stealth/surprise bonus and secret-door detection, Dwarf's stonework/underground detection (verified real AD&D traits, no mechanical home yet — see `combat.md` §16.4)

---

## 9. Open questions (resolved)

1. ~~Human's bonus~~ — resolved: +1 to one stat of player's choice.
2. ~~Cha-penalty dormancy~~ — resolved: ship as documented, dormant until Charisma added at 1.0.
3. ~~Racial trait beyond stats~~ — resolved: see §12, verified against real AD&D convention (Elf sleep/charm resistance, Dwarf poison/magic resistance + Goblin/Orc combat bonus, Halfling magic/poison resistance).

---

## 10. Stat rolling — best-of-3 per stat, whole-character reroll (supersedes the original 3d6-straight)

**Status:** locked. Revises this document's original decision. Players now roll differently from NPCs (an intentional asymmetry — protagonists get a friendlier method; NPCs stay on flat single 3d6, per §5).

**The mechanic:** for each of the four stats (Str, Dex, Con, Wis), roll 3d6 **three separate times**, keep the **highest** of the three as that stat's value. All four stats use this method simultaneously.

**Reroll behavior:** the Reroll button rerolls **all four stats together** as one unit — there is no mechanism to reroll a single stat in isolation. Each reroll redraws all three 3d6 attempts for all four stats fresh.

**Note:** this supersedes the note in `PRD.md` §10 marking player stat rolling as "resolved: 3d6 straight, same as NPCs" — that resolution is now inaccurate and should be corrected.

---

## 11. Alignment

**Status:** locked. A personal character trait, selected at creation, **separate from and not mechanically tied to** the world-creation win-condition alignment setting already locked in `PRD.md` §4.9.

**Options:** Lawful / Neutral / Chaotic (three-point, matching the existing win-condition axis's categories, not a full 9-point Good/Evil grid).

**Purpose:** flavor text and **faction interaction texture** — intended to influence dialogue tone, NPC reactions, and potentially default disposition nuances layered on top of `docs/gdd/factions.md`'s existing Hostile/Neutral/Allied model (e.g. a Chaotic character might get a different default reception at a Lawful-aligned settlement, as a later refinement — not mechanically wired in this initial pass). No effect on the world-creation win-condition setting.

---

## 12. Racial and class traits

**Status:** locked, verified against real AD&D convention (not invented). Full mechanical detail lives in `docs/gdd/combat.md` §16 (Saving Throws, racial bonuses) and §17 (class traits) — this section is the character-creation-facing summary.

### 12.1 Racial traits

| Race | Trait |
|---|---|
| Elf | +4 Saving Throw bonus vs. sleep/charm-type effects |
| Dwarf | +2 Saving Throw bonus vs. poison and magic; +2 Attack Bonus vs. Goblin and Orc specifically |
| Halfling | +2 Saving Throw bonus vs. magic and poison |
| Human | (existing +1 to any stat of choice, per §3, serves as their distinguishing trait) |

### 12.2 Class traits

| Class | Trait |
|---|---|
| Fighter | Enhanced active-block Target Number bonus (+6 instead of standard +4) |
| Ranger | Lowered Ranged crit "wide margin" threshold (needs total ≥22 instead of ≥24 on a natural 20) |

---

## 13. Visual character customization — deferred to dedicated stub

**Status:** confirmed direction, full design in `docs/gdd/character_customization.md` (new stub). Discrete preset system (2–3 options per customizable trait, not continuous sliders), plus height/weight applied as a scale multiplier on the base model. Age/Height/Weight rolled per race have **no mechanical effect** — cosmetic only, feeding this system.
