# ADR-0003: Perspective and control — top-down + WASD avatar

**Status:** Accepted
**Date:** 2026-07-02
**Deciders:** Edu + Claude

## Context

The game's spiritual ancestors offer two very different control models:
- **RimWorld / Dwarf Fortress:** disembodied god-view cursor. Players don't have avatars; they command pawns via UI.
- **Valheim / Aska / Bellwright:** each player has a physical avatar in the world, controlled by WASD.

The choice affects:
- Whether coop players see each other on screen (avatar model: yes; god-view: no)
- Whether real-time combat feels good (avatar: yes; god-view: awkward)
- Whether pausing is compatible (god-view: yes; avatar coop: no)
- Whether players feel like they're *there* vs. commanding from above

Mankers Kingdoms is a coop game where presence matters. Two players who can see each other's characters, walk together, fight side by side — that's a fundamentally different experience from two cursors on a shared map.

## Decision

Top-down camera, each player controls one physical avatar via WASD. Mouse aim + left/right click for combat. Avatar can walk, run, jump, attack, block, interact with objects and NPCs.

Camera stays top-down (or slight 3/4 angle if visual tests show it reads better). No third-person, no first-person.

## Consequences

**Positive:**
- Coop presence and camaraderie work as intended
- Real-time combat has a clear character to embody
- Simpler mental model — players are their character, not commanding an army
- Aligns with the survival-adjacent games we're taking inspiration from

**Negative:**
- No pause is compatible (see ADR-0004)
- Cannot do RimWorld-style global priority-list job assignment; must use station-based (which is what we want anyway)
- Combat feel matters more — players will notice bad hit detection immediately

## Alternatives considered

- **RTS-style disembodied cursor** (RimWorld model). Rejected — kills coop presence.
- **First-person avatar.** Rejected — top-down suits a settlement-view game better and cheaper to render.
- **Multiple selectable avatars per player** (Bellwright's late-game model). Rejected — adds complexity, breaks coop presence, defers to later if needed.

## References

- PRD.md §4.1
- Design conversation, session 2
