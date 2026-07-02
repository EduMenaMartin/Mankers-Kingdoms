# ADR-0016: Rename to "Mankers Kingdoms"

**Status:** Accepted (supersedes ADR-0001)
**Date:** 2026-07-02
**Deciders:** Edu + Claude

## Context

The original working title "Petty Kingdoms" (ADR-0001) was chosen for genre literacy — the historical term for early medieval kingdoms. But the phrase is common enough to be difficult to trademark defensibly, and Edu proposed a more distinctive name during the design phase.

**"Mankers Kingdoms"** derives from Spanish *manco* (meaning noob / clumsy person, roughly), evolved to *manker* for English pronunciation. The tone is self-deprecating — "noob kingdoms" — which fits a game about petty rulers scraping together a settlement.

The name is:
- Distinctive (not a common phrase or historical term)
- Easy to search for uniquely
- Has a story behind it (good for dev blogs, Steam page)
- Sets a tone (humble, self-aware, indie-friendly)
- No conflict on Steam as of check

## Decision

Rename working title to **Mankers Kingdoms**. Supersedes ADR-0001.

Renaming applied globally across all docs, folder names (project folder → `mankers-kingdoms/`), and eventual code namespace.

Subtitle still deferred to commercial release.

## Consequences

**Positive:**
- Distinctive title
- Trademark-defensible with future subtitle
- Tone matches the "solo indie building something bigger than expected" energy
- Story-behind-the-name is marketing gold

**Negative:**
- Global search-replace across docs (already done)
- If commercial release is far in the future, may drift from the name; check-in required at each milestone

## Alternatives considered

- Keep "Petty Kingdoms." Rejected — less distinctive.
- Other candidates from the original session: Kith & Keep, Bannerholds, etc. All rejected in favor of Mankers Kingdoms.

## References

- Supersedes ADR-0001
- PITCH.md, PRD.md, VERTICAL_SLICE.md all updated
