# ADR-0014: Random encounter recruitment on roadmap

**Status:** Accepted
**Date:** 2026-07-02
**Deciders:** Edu + Claude

## Context

The recruitment loop is central to Mankers Kingdoms — you build your kingdom from the people you gather. Sourcing NPCs only from procedural villages makes the loop feel monotonous over long campaigns.

Real-world / classic-fantasy sources of new members that would fit:
- Ambushed caravans with wounded survivors who join you when saved
- Wandering nomads seeking shelter
- Refugees fleeing monster attacks in the region
- Hermits found in remote areas
- Deserters from enemy forces
- Prisoners freed from enemy strongholds
- Escaped slaves from monster nests

Each provides narrative flavor and creates memorable moments beyond the mechanical village-visit loop.

## Decision

Random encounter NPC sources are a **roadmap feature for Early Alpha (0.1 → 0.3)**, per PRD §6.2. Not in v1 slice.

When implemented, each source has:
- Trigger conditions (traveling in wilderness, world event fired, etc.)
- Narrative context via a short dialogue snippet
- Recruit-yes/no branch with consequences (e.g. saved caravan members have gratitude bonus)

## Consequences

**Positive:**
- Adds flavor and memorable moments to recruitment
- Reduces "same village" fatigue
- Creates natural narrative hooks for future story systems

**Negative:**
- Requires a robust random encounter system to layer these on
- Each encounter type needs writing and design
- Balance risk: too-frequent encounters trivialize village recruitment

## Alternatives considered

- **Add to v1 slice.** Rejected — recruitment loop must first be proven in the simplest form (village only).
- **Skip entirely, villages only forever.** Rejected — feels flat over 40+ hour campaigns.

## References

- PRD.md §6.2
