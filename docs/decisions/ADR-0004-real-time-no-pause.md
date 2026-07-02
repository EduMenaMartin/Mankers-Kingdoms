# ADR-0004: Real-time no pause

**Status:** Accepted
**Date:** 2026-07-02
**Deciders:** Edu + Claude

## Context

Colony sims traditionally offer pause-and-plan mechanics (RimWorld, Dwarf Fortress). This lets players make tactical decisions without time pressure. But in coop, pause becomes a coordination problem: whose input pauses the game? Do all players agree? What if one wants to pause and another doesn't?

Coop survival games (Valheim, Aska, Bellwright) universally solve this by removing pause entirely. Time keeps flowing; players react in real time. This creates urgency but preserves coop presence.

Mankers Kingdoms is a coop game where presence and shared time matter (see ADR-0003).

## Decision

No pause in multiplayer. Time flows continuously. Combat, crises, needs, and world events all unfold in shared time.

Single-player pause is deferred. If added later, it's a solo-mode-only feature.

## Consequences

**Positive:**
- Coop coordination is simpler (no "who paused" question)
- Combat has real stakes and reflex demands
- Simulation is simpler — no pause state to serialize, no "paused while X happens" edge cases

**Negative:**
- Tactical thinking must happen alongside action (harder for some players)
- Cannot use RimWorld-style pause-to-command mechanics
- Menu interactions must not hide the world (inventory, build menu should overlay, not stop time)

## Alternatives considered

- **Consensus pause** (all players must agree). Rejected — too much friction, breaks flow.
- **Time dilation instead of pause** (slow-mo when a player opens a menu). Rejected — griefable, coordination issues.
- **Pause only host, others wait.** Rejected — bad experience for waiting players.

## References

- PRD.md §4.1, §4.3
- ADR-0003 (avatar control)
