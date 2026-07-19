# ADR-0026: Shift from vertical-slice discovery mode to committed long-term production

**Status:** Accepted
**Date:** 2026-07-11
**Deciders:** Edu + Claude

## Context

`VERTICAL_SLICE.md`'s original framing (echoed in `PRD.md`, `PITCH.md`) was explicit: ~3 months, a discovery vehicle, an honest go/no-go decision at M9 based on three questions (fun? legible? differentiated from Valheim?).

M9 self-assessment, done honestly by Edu actually playing the game, found: the core proof-of-concept genuinely works (LAN multiplayer, combat resolution, settlements, recruitment, skills, save/load). But the experience is not yet compelling enough to bring a friend into — world generation lacks density and scale, no metal/stone industry exists, no food ecosystem exists, and several already-locked features (water rendering in the actual 3D world, tree regrowth, visible progression feedback) are incompletely wired even where the underlying systems work.

Rather than treat this as a failed slice, Edu sees real potential in the concept and is choosing to continue development over a much longer horizon than originally scoped.

## Decision

Mankers Kingdoms transitions from **"vertical slice discovery vehicle with a hard go/no-go gate"** to **"committed long-term solo development project."**

Practical implications:

- The M0–M9 milestone structure and everything built within it remains valid — it is the foundation, not a discarded experiment. No work is being thrown away.
- Future milestones (M10+) will formally take on systems previously deferred to `PRD.md` §6's roadmap: world-generation quality pass, the Trades industry (Stonecutting, Mining, Smithing), the food ecosystem (Farming, Hunting, Fishing), a proper art direction pass, and other items already logged in `IDEAS_BACKLOG.md`.
- **Asset sourcing policy expands.** Free assets remain first choice, but paid asset packs (visual, SFX, music) are now in-budget wherever they fill a genuine gap. Commissioned/custom work remains deferred until the game is validated with real friend playtesting.
- **Team remains solo (Edu) + Claude Code.** No additional contributors at this time — explicitly revisited, not yet.
- **Release ambition:** development continues privately. Steam publication remains the eventual goal, contingent on the game proving fun in real friend playtests first — not tied to any fixed calendar date.

## Consequences

**Positive:** removes false time pressure from an artificial 3-month deadline that no longer reflects reality. Permits proper investment in previously-deferred systems without every addition being treated as scope creep against a vertical slice that was never meant to hold this much. Existing documentation and architecture discipline (PRD, ADRs, GDDs) continues to scale cleanly to a larger project — nothing about the process needs to change, only the scale of what it's applied to.

**Negative:** without a redefined roadmap, the risk of unbounded scope drift returns. Mitigated by treating this ADR as the trigger for drafting a proper Phase 2 roadmap next, not as an open-ended "build everything" mandate.

## Alternatives considered

- **Treat M9 as a strict go/no-go gate and shelve the project** if fun/legible/differentiated criteria aren't met as originally scoped. Rejected — Edu explicitly reassessed and sees real potential; the honest issue is scope insufficiency for a genre this ambitious, not a fundamentally broken core loop.
- **Add newly-identified features piecemeal** without formally acknowledging the timeline/scope shift. Rejected — would violate the project's own documentation discipline (PRD-first, ADR for scope changes) established since day one.

## References

- `VERTICAL_SLICE.md` (original 3-month scope; content remains valid, timeline framing superseded)
- `PRD.md` §6 (deferred features roadmap — industry/food systems already listed here, now moving from roadmap to active planning)
- `IDEAS_BACKLOG.md` (existing polish/content backlog)
- Session M9 self-assessment findings (world-gen density, water rendering, industry absence, progression legibility)
