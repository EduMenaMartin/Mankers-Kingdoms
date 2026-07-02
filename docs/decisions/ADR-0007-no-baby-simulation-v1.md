# ADR-0007: No baby simulation in v1

**Status:** Accepted
**Date:** 2026-07-02
**Deciders:** Edu + Claude

## Context

The recruitment loop is powered by NPCs. To keep a settlement growing over long campaigns, NPC sources must expand somehow — otherwise late-game becomes static.

Options for NPC generation over time:
1. **Nothing** — village NPC count is fixed at world creation
2. **Passive village growth** — villages abstractly gain population over time
3. **Full baby simulation** — parents, gestation, aging, childhood, inheritance

Option 3 is a scope grenade: Dwarf Fortress and CK3 spend enormous engineering on this. It requires courting AI, pregnancy state, aging systems (which then apply to all characters, not just babies), childhood progression, and orphan handling.

## Decision

No baby simulation in v1, and no aging system. Villages in v1 are static in population.

Passive village growth (option 2) is added in Early Alpha per PRD §6.2. Full baby simulation (option 3) is deferred to Beta / post-1.0 as an opt-in feature.

## Consequences

**Positive:**
- Removes a huge system from v1 scope
- No aging system means characters have permanent identity forever (aligns with pillar 5)
- Playtest data will inform whether babies are even worth the eventual effort

**Negative:**
- Long-campaign viability in v1 is limited (settlements can't organically grow without recruitment)
- Some genre players expect Dwarf Fortress-level depth

**Accepted:** v1 is a discovery vehicle. Long-campaign feel is tested in later milestones.

## Alternatives considered

- **Passive growth in v1.** Rejected — even "abstract growth" requires a system, and it isn't proven core to the slice.
- **Full baby sim in v1.** Never seriously considered. Massive scope.

## References

- PRD.md §4.5, §6.2, §6.4
- VERTICAL_SLICE.md §3.7, §4
