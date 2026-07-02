# ADR-0013: Ranged combat in v1

**Status:** Accepted
**Date:** 2026-07-02
**Deciders:** Edu + Claude

## Context

Original v1 scope had melee-only combat, with Rogue + Fighter both using melee weapons. This kept combat scope minimal but limited class differentiation and combat variety.

Edu proposed adding ranged combat to v1. Cost: ~1–2 weeks additional in M4 for bow + arrow projectile system + ranged AI variant. Benefit: fundamentally different class identities (Fighter tank / Ranger skirmisher), broader combat surface tested in slice.

Simultaneously, this triggered ADR-0018 (Ranger replaces Rogue) because Ranger fits ranged combat more naturally.

## Decision

Add ranged combat to v1 vertical slice.

**Mechanics:**
- Bow-based ranged with aim (mouse cursor) + click to fire
- Arrow projectile with trajectory (arc) and travel time — can miss
- Arrows crafted at Workbench from wood
- Server-authoritative hit detection on impact
- Ranger class kit includes shortbow + hunting knife
- At least one enemy variant uses ranged attacks (Bandit Archer — reuses same ranged code path)

## Consequences

**Positive:**
- Class differentiation is much stronger (melee vs ranged is night-and-day)
- Full combat surface tested in slice
- Ranger becomes the natural second class (see ADR-0018)
- Bandit Archer variant proves enemy AI can reuse ranged code

**Negative:**
- +1–2 weeks in M4
- Requires projectile physics/trajectory system (not needed for melee)
- Ammo management adds one more mechanic to test

**Accepted:** the scope addition is worth the class-differentiation gain.

## Alternatives considered

- **Keep melee-only, add ranged post-slice.** Rejected — would mean discovering combat feel with only half the surface tested.
- **Add ranged and full stealth for Rogue.** Rejected — stealth is deeper than v1 has time for; keep as scaffold only.

## References

- VERTICAL_SLICE.md §3.4
- ADR-0018 (Ranger replaces Rogue)
- Design conversation session 8
