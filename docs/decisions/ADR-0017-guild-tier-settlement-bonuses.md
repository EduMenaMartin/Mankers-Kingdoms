# ADR-0017: Guild-tier settlement bonuses on roadmap

**Status:** Accepted
**Date:** 2026-07-02
**Deciders:** Edu + Claude

## Context

Per-character skill progression works well but has a ceiling: an individual character can only get so good. For long campaigns to feel like they progress *beyond* individual character growth, there needs to be a system that ties investment in a settlement's specialty to some form of progression.

Precedent:
- Anno series: production buildings level up based on aggregate output
- Cities: Skylines: districts specialize and gain policies
- RimWorld: not applicable (per-pawn only)

The natural design: settlement-wide guild buildings that accumulate cumulative XP across all workers in the settlement and grant passive bonuses to all workers in that trade when guild tiers up.

For v1, this is out of scope. Per-character progression must be proven first.

## Decision

**Per-character skill progression only in v1** (already locked). Guild-tier settlement bonuses added in **Alpha → Beta (0.3 → 0.8)** per PRD §6.3.

**Design sketch (subject to change):**
- Each Trade skill has an associated Guild building (Woodcutter's Guild, Farmers' Guild, etc.)
- Guild building accumulates settlement-wide XP for that trade (sum of all workers' XP earned while working)
- Guild levels up at XP thresholds
- Each guild tier grants passive bonuses to all workers in that trade in the settlement (+% output, +% XP gain, unlock rare drops, unlock higher tool tiers earlier)
- Similar guilds for combat trades (Fighters' Guild, Rangers' Guild)

Full design work happens when v1 slice succeeds.

## Consequences

**Positive:**
- Gives late-game settlements a distinctive character (a "Woodcutter's Guild kingdom" vs a "Smithing kingdom")
- Rewards long-term investment in a specialty
- Creates a reason to grow beyond a small band
- Complements the conquest mechanic (satellite settlements can specialize)

**Negative:**
- Requires per-character system to be stable first
- Balance risk: guild bonuses could trivialize character stat differences
- Content bloat: one guild per trade adds many buildings

## Alternatives considered

- **Per-character only forever.** Rejected — late-game feels flat.
- **Guild systems in v1.** Rejected — must prove per-character system first.

## References

- PRD.md §6.3
- docs/gdd/skills.md §7
- ADR-0011 (skill framework)
