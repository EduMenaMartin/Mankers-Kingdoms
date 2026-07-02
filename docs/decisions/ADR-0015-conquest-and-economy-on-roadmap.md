# ADR-0015: Conquest mechanic and economic layer on roadmap

**Status:** Accepted
**Date:** 2026-07-02
**Deciders:** Edu + Claude

## Context

D&D Stronghold's alignment-based Chaotic victory requires destroying enemy strongholds. In our design, "destroying" a stronghold is less interesting than *taking it over* — the ability to raid, capture, and convert enemy settlements creates a rich strategic layer.

The mechanic implies:
- Raiding enemy monster/villain villages
- Non-destroyed buildings can be claimed by the raider
- Captured settlements can be run by the player, becoming a satellite outpost
- Multi-settlement play emerges naturally
- An economic layer becomes necessary: resource and manpower trading between the player's original settlement and captured outposts, and between the player and NPC villages

This is a big feature bundle. Not v1 material.

## Decision

**Conquest mechanic + economic layer are roadmap features for Early Alpha (0.1 → 0.3)**, per PRD §6.2. They arrive together because conquest without economy is thin, and economy without conquest has less to trade.

## Consequences

**Positive:**
- Extends the strategic layer significantly
- Provides a real Chaotic-alignment victory path
- Adds a mid-to-late-game progression axis (settlement network vs. single settlement)
- Economic layer opens the door for future player-to-player trade

**Negative:**
- Large scope: settlement-conversion logic, ownership permission, resource/manpower flow simulation
- Balance risk: conquest may trivialize peaceful play
- AI enemies need to be interesting to fight — pushes enemy AI up the priority list

## Alternatives considered

- **Conquest only, no economy.** Rejected — captured settlements would be pointless.
- **Economy only, no conquest.** Rejected — the pair naturally reinforce each other.
- **Skip entirely.** Rejected — the Chaotic alignment path needs a conquest mechanic to work.

## References

- PRD.md §4.9, §6.2
- ADR-0008 (win condition player toggle)
