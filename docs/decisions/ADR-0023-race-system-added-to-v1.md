# ADR-0023: Race system (Human/Dwarf/Elf/Halfling) added to vertical slice scope

**Status:** Accepted
**Date:** 2026-07-05
**Deciders:** Edu + Claude

## Context

The original D&D Stronghold (1993) used **race and class as separate, orthogonal choices** in its character creation flow. The manual confirms: roll attributes first, then choose race, then class — with race affecting recruitment and building access alongside class. Verified directly against the game manual and period sales material.

Our vertical slice (`VERTICAL_SLICE.md` §3.2) had two classes locked (Fighter, Ranger) and four stats locked (Str, Dex, Con, Wis), but no race system. Adding race is consistent with the original's structure, extends the already-locked NPC stat-rolling system to one more axis, and is low implementation cost (a modifier table lookup + a data file per race).

Two design decisions were open at the time of this ADR:
- Player stat rolling method (PRD.md §10 Q8): random like NPCs, point-buy, or hybrid?
- Whether to add a race system to v1 at all.

## Decision

**Race system added to vertical slice scope.** Four playable races for v1: **Human, Dwarf, Elf, Halfling** — the same four from the original game.

**Stat rolling locked at 3d6 straight** (same method already used for NPCs). This resolves PRD.md §10 Q8.

**Race applies a stat modifier** (one bonus + one penalty, per classic AD&D 1st/2nd Edition conventions) to the 3d6-rolled stat before it feeds skill caps (ADR-0019) or combat formulas (docs/gdd/combat.md):

| Race | Bonus | Penalty |
|---|---|---|
| Human | +1 to one stat of player's choice | none |
| Dwarf | +1 Constitution | −1 Charisma |
| Elf | +1 Dexterity | −1 Constitution |
| Halfling | +1 Dexterity | −1 Strength |

**Existing class list is unchanged** — Fighter and Ranger are retained rather than reverting to the original's Cleric/Mage/Fighter/Thief. Magic is deferred (ADR-0006); Thief's niche is covered by Fighter; more classes arrive at 1.0 per PRD.md §10 Q2.

**NPC race generation** is included at the same time — procedural village NPCs receive a race using the same modifier table (one extra generation parameter on the already-locked NPC system, not a new system).

**Race-gated buildings and race-matched recruitment bonuses are explicitly deferred** to roadmap — not in v1.

Full design detail in `docs/gdd/character-creation.md`.

## Consequences

**Positive:**
- 4 races × 2 classes = 8 playable combinations, meaningfully increasing character variety within the vertical slice.
- Resolves PRD.md §10 Q8 (stat rolling) as a side effect of locking race modifiers — now both players and NPCs use 3d6 straight.
- Low implementation cost: a modifier table + JSON data files per race. No new systems.
- NPC villages gain racial flavour (a Dwarf-heavy village is a natural Con-workforce recruit target) without adding mechanics.
- Future races (Gnome, Half-Elf, Half-Orc, etc.) are trivial additions — the system is just a lookup table.

**Negative:**
- Dwarf's −1 Charisma has no mechanical effect in v1 (v1 uses only Str/Dex/Con/Wis). This is documented and dormant, not silently dropped — activates naturally when Cha is added at 1.0.
- Eight character creation paths to QA instead of two. Acceptable for a two-player prototype.
- Human's "+1 to any stat" is an open question — not fully locked (see docs/gdd/character-creation.md §9 Q1).

## Alternatives considered

1. **No race system in v1** — simpler, but diverges from the original's core structure without good reason. Rejected.
2. **Race-as-class** (Dwarf Fighter is just "Dwarf") — inconsistent with the original, which explicitly separated race and class. Rejected.
3. **More than four races in v1** — easy to add later; not worth scope-creeping the vertical slice. Deferred to roadmap.
4. **Race-gated buildings** (parallel to class-gated buildings in docs/gdd/settlements.md) — natural extension but unnecessary for the demo gate. Explicitly deferred.
5. **Point-buy stat generation for players** — rejected in favour of 3d6 straight (same as NPCs) for simplicity and parity with the original's confirmed "roll for abilities" flow.

## References

- `docs/gdd/character-creation.md` — full design, modifier table, data model, open questions
- `VERTICAL_SLICE.md` §3.2 — character creation scope
- `PRD.md` §4.4 (locked stats), §10 Q8 (stat rolling, now resolved)
- ADR-0006 (no magic in v1)
- ADR-0009 (stable string IDs — used for race IDs)
- ADR-0018 (Ranger replaces Rogue — class list rationale)
- ADR-0019 (skill cap formula — racial stat modifiers feed this)
- `docs/gdd/combat.md` (racial stat modifiers feed combat formulas)
- `docs/gdd/skills.md` (NPC stat generation — race extends this)
- D&D Stronghold (1993) game manual — race + class flow verified directly
