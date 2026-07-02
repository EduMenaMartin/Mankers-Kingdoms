# ADR-0019: Skill cap formula

**Status:** Accepted
**Date:** 2026-07-02
**Deciders:** Edu + Claude

## Context

The soft-class model requires stats to gate skill ceilings — otherwise all characters converge to identical maxed-out endgame stats and the point of stat variety is lost.

Stats are rolled 3d6 straight (range 3–18, AD&D standard). Skills range 1–99 (RuneScape-style granularity for feedback).

Candidate formulas:
- **stat × 5** — stat 3 caps at 15, stat 18 caps at 90. Never reaches 99.
- **stat × 5.5** (= floor(99 × stat / 18)) — stat 3 caps at 16, stat 18 caps at 99. Clean map from full stat range to full skill range.
- **stat × 8** — stat 3 caps at 24, stat 18 caps above 99 (effectively no cap for high stats). Softer, more forgiving.
- **stat × 10** — very soft cap; only very low stats are meaningfully limited.

Multi-stat skills (governed by two stats, e.g. Athletics = Str + Con) need a rule: higher-of, average, or lower-of.

## Decision

**Skill ceiling = floor(99 × stat / 18)**, equivalently floor(stat × 5.5).

**Multi-stat skills use higher-of.**

Full table:

| Stat | Ceiling |
|---|---|
| 3 | 16 |
| 6 | 33 |
| 10 | 55 |
| 14 | 77 |
| 18 | 99 |

**Additional decisions (locked at the same time):**
- No decay (skills don't reduce with disuse)
- No legendary stats (18 is the hard cap; no mechanism to exceed it)
- No grandmaster tier above 99
- No prestige (skills don't reset for permanent bonuses)

## Consequences

**Positive:**
- Full 3d6 stat range meaningfully maps to full 1–99 skill range
- Low-stat characters have permanent identity — a Str 3 character will never be a warrior
- Multi-stat higher-of rewards character strengths (one good stat is enough)
- Recruitment strategy has real teeth — a Str 16 villager is a permanent asset
- Simple mental model for players (skill cap ≈ stat × 5.5)

**Negative:**
- Bad rolls create permanently limited characters (mitigated by allowing reroll before commit)
- No progression path for a low-stat character to become elite in that skill

## Alternatives considered

- **stat × 8 or × 10.** Rejected — too soft; low stats aren't meaningfully limited.
- **stat × 5.** Rejected — max stat can't reach 99.
- **Multi-stat average.** Rejected — punishes character strengths.
- **Multi-stat lower-of.** Rejected — "roll well twice" frustration.
- **Legendary stats via magic items.** Rejected — muddies the "stats are permanent identity" pillar.

## References

- docs/gdd/skills.md §3
- PRD.md §4.4
- ADR-0011 (skill framework)
