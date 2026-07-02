# ADR-0020: XP-per-tick-while-working

**Status:** Accepted
**Date:** 2026-07-02
**Deciders:** Edu + Claude

## Context

Skill progression needs an XP mechanic. Options:

- **XP-per-action** (RuneScape): each discrete action (swing, chop, cast) grants XP. Encourages spam.
- **XP-per-tick-while-working**: while an entity is actively engaged in a skill-relevant behavior, XP accumulates at a per-tick rate. Idle characters gain nothing.
- **XP-per-milestone**: XP granted only when tasks complete (e.g. tree fully chopped). More granular but bursty.
- **Flat + logarithmic decay**: XP rate slows as skill approaches ceiling.

The tick-based server simulation already knows what each character is doing every tick — feeding XP off that state is essentially free.

## Decision

**XP-per-tick-while-working.**

- Server tick loop iterates over all entities
- If an entity has an active `WorkComponent` referencing a station or a target (e.g. "chopping this tree"), it accumulates XP in the relevant skill at a per-tick rate
- Idle characters (unassigned NPCs, players standing still) gain no XP
- Combat XP accumulates while engaged in combat, allocated to the weapon-skill actually used per tick

Specific XP rates per skill are balancing values, tuned during v1 and later.

## Consequences

**Positive:**
- Ties skill progression directly to gameplay engagement — no AFK grind
- Server tick is already computing what each entity is doing; XP is a free additional field
- Visible feedback loop ("Woodcutting +1" every N seconds while chopping)
- Combat XP is naturally weighted by actual weapon use (a Ranger who switches to sword mid-fight grows Melee, not Ranged)

**Negative:**
- Players who like to grind may find fewer optimizations
- Balancing XP rates across many skills is ongoing work
- Passive-play players (leaving character parked at a workstation) still gain some XP — mitigated by "actively working" state requiring recent interaction

## Alternatives considered

- **XP-per-action.** Rejected — encourages spam-clicking.
- **XP-per-milestone.** Rejected — burstier feedback loop, less continuous.
- **Logarithmic curves per-skill.** Deferred — will layer on top of the base per-tick rate if playtest shows late-game feels flat.

## References

- docs/gdd/skills.md §6
- ADR-0011 (skill framework)
- ADR-0019 (cap formula)
