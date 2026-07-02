# ADR-0008: Win condition — player-facing toggle

**Status:** Accepted
**Date:** 2026-07-02
**Deciders:** Edu + Claude

## Context

D&D Stronghold (1993) had three alignment-based victory paths tied to Lawful, Chaotic, and Neutral alignments. Valheim and Aska are pure sandbox with boss milestones. Modern coop games often mix: sandbox by default with optional goals.

Different players want different things. Some want a defined end; some want to play forever. Forcing one model excludes half the audience.

## Decision

Win condition is a **player-facing toggle at world creation** with three options:

1. **Sandbox** — no win state, play indefinitely
2. **Sandbox + Boss** — optional milestone bosses provide closure without ending the world
3. **Alignment-based (Stronghold-style)** — Lawful (become Emperor), Chaotic (destroy all enemy strongholds), Neutral (both)

Same game, same mechanics — only the ending conditions and endgame triggers differ.

Not in v1: only Sandbox mode is implemented in the vertical slice. Sandbox+Boss and Alignment modes are Early Alpha work.

## Consequences

**Positive:**
- Serves multiple player preferences with one game
- Alignment mode honors the Stronghold heritage explicitly
- Sandbox lets long-campaign players play forever

**Negative:**
- Alignment mode requires enemy stronghold AI (roadmap), boss encounters (roadmap), Emperor coronation state — more content
- Testing three end-states triples endgame QA work

## Alternatives considered

- **Sandbox only.** Rejected — closes off the Stronghold-style challenge run.
- **Alignment only.** Rejected — excludes long-campaign sandbox players.

## References

- PRD.md §4.9, §6.2
- Original D&D Stronghold (1993, SSI/Stormfront)
