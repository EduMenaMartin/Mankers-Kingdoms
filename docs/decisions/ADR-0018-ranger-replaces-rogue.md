# ADR-0018: Ranger replaces Rogue for v1 second class

**Status:** Accepted
**Date:** 2026-07-02
**Deciders:** Edu + Claude

## Context

Initial v1 class choice was Fighter + Rogue with both classes using melee (Rogue with daggers). This was rejected because ADR-0013 added ranged combat to v1 — and "Rogue with bow" is a thematic compromise. Rogues are canonically melee/stealth (D&D 5e Rogue is primarily a Dex-based melee striker).

Ranger is canonically:
- Dex/Wis-based
- Ranged combat primary (bow)
- Wilderness / herbalism / tracking flavor
- Natural fit for a class-gated Herbalist's Hut

Fighter + Ranger gives:
- Full class kit distinction (sword+shield vs shortbow+hunting knife)
- Genuine stat divergence (Str-primary vs Dex+Wis-primary)
- Clean fit for the Foraging → Herbalist's Hut demonstration of presence-gating
- Two archetypal classes that will remain in the final 5–7 class list

## Decision

Replace Rogue with **Ranger** as the second class in v1 vertical slice.

**Ranger class kit:**
- Shortbow (ranged primary weapon)
- Hunting knife (melee sidearm)
- Skill bumps: Ranged +5, Foraging +3, Stealth +3 (Stealth still scaffold only)
- Cosmetic: leaf-green cloak

## Consequences

**Positive:**
- Class differentiation is thematically clean
- Ranged combat has a class that canonically owns it
- Foraging → Herbalist's Hut demo is thematically grounded
- Both classes are top candidates for the 1.0 class list

**Negative:**
- Rogue class is now deferred; some players will miss stealth-oriented play
- Any prior code referencing Rogue must be updated (n/a at this stage — no code yet)

## Alternatives considered

- **Keep Rogue.** Rejected — thematic mismatch with ranged combat.
- **Ranger + Cleric.** Cleric implies magic, deferred to post-slice.
- **Fighter + Barbarian.** Both melee brawlers — insufficient differentiation.
- **Fighter + Wizard.** Wizard requires magic system.

## References

- ADR-0013 (ranged in v1)
- VERTICAL_SLICE.md §3.2, §7
