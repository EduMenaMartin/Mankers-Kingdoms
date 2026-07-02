# ADR-0006: No player magic in v1

**Status:** Accepted
**Date:** 2026-07-02
**Deciders:** Edu + Claude

## Context

A D&D-flavored fantasy game with no magic feels incomplete. But a full magic system — spell schools, learning, casting, effects, animations, balance — is a substantial engineering commitment that touches combat, UI, progression, and content pipelines.

The vertical slice needs to prove the core loop, not deliver every system. Player magic is a natural post-slice addition: the skill framework already supports it (Magic group with Alchemy, Sorcery, Weirding — see docs/gdd/skills.md), so adding it later is additive, not architectural.

## Decision

No player-castable magic in v1 vertical slice.

Monsters and environmental hazards can still be "magical" in flavor (a goblin shaman's fire trap, a haunted glade with hallucinations) — but the underlying mechanic is scripted content, not a magic *system*.

Player magic is deferred to Early Alpha (0.1 → 0.3) per PRD §6.2. When added, it uses the same skill-based use-to-level progression as everything else, with Magic group skills (Alchemy, Sorcery, Weirding) capped by governing stats.

## Consequences

**Positive:**
- Removes a huge scope block from v1
- Combat system stays focused on melee + ranged, both testable
- Content pipeline for spells can be designed with full context after playtesting

**Negative:**
- Setting feels less D&D-esque in v1
- Wizard/Sorcerer class options must wait
- Some players may bounce off the slice for lack of magic — accepted risk for a discovery vehicle

## Alternatives considered

- **Magic in v1, thin scope.** Rejected — even a "thin" magic system requires spells, animations, UI, balance. Costs too much for v1.
- **Magic gated behind a boss unlock.** Rejected — no bosses in v1 either.

## References

- PRD.md §4.3, §6.2
- docs/gdd/skills.md §2.4
- VERTICAL_SLICE.md §4 (OUT list)
