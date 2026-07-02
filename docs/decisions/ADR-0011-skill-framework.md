# ADR-0011: Skill framework — SkillSetRPG 4-group + Trades

**Status:** Accepted
**Date:** 2026-07-02
**Deciders:** Edu + Claude

## Context

The soft-class model needs a concrete skill system. Options considered:
- **RuneScape / Elder Scrolls linear list** — flat list of 20+ skills, all use-to-level, no grouping.
- **D&D-style organized by ability score** — skills grouped by governing stat.
- **SkillSetRPG framework** — tabletop RPG system organizing ~24 skills into 4 groups (Physique, Intellect, Charisma, Magic).
- **Aska-style resource professions** — separate progression per gathering profession (Woodcutting, Stonecutting, Foraging, etc.).

SkillSetRPG offers clean structure but doesn't cover the granular gathering professions Aska does. Aska has granular gathering but no broader framework.

## Decision

Adopt SkillSetRPG's 4-group structure (**Physique, Intellect, Charisma, Magic**) as the framework, and add a **5th "Trades" group** specifically for Mankers Kingdoms' gathering and production professions (Aska-inspired).

Full detail in `docs/gdd/skills.md`.

**Group summary:**
- **Physique:** Agility, Athletics, Melee, Ranged, Stamina, Stealth
- **Intellect:** Academics, Craft/Mend, Languages, Medicine, Perception, Survival, Trade, Warfare
- **Charisma:** Artistry, Command, Investigate, Persuade, Ride/Team, Willpower
- **Magic:** Alchemy, Sorcery, Weirding
- **Trades:** Woodcutting, Stonecutting, Mining, Foraging, Farming, Fishing, Hunting, Cooking, Smithing

Each Trades skill has tool tier unlocks at level thresholds (Aska pattern — stone → bronze → iron → steel → mithril, etc.).

v1 slice uses a subset of six skills (Melee, Ranged, Athletics, Woodcutting, Foraging, Cooking) plus Stealth as a scaffold.

## Consequences

**Positive:**
- Clean thematic organization inspired by well-considered tabletop system
- Trades group provides Aska-style granular feedback
- All skills use the same underlying mechanic (use-to-level, stat-capped)
- 5-group model maps cleanly to AD&D stats

**Negative:**
- ~30 total skills is a lot to balance
- Some SkillSetRPG skills (Languages, Trade, Warfare) don't translate cleanly to video game action verbs — deferred
- More content pipeline work to define all skills as data

## Alternatives considered

- **Pure SkillSetRPG.** Rejected — no granular gathering feedback.
- **Pure Aska/RuneScape flat list.** Rejected — no structural organization.
- **D&D 5e skill list.** Rejected — mostly focused on social/exploration checks, less on action.

## References

- SkillSetRPG: https://skillsetrpg.com/fantasy-skills
- docs/gdd/skills.md — full spec
- PRD.md §4.4
- ADR-0019 (cap formula)
- ADR-0020 (XP formula)
