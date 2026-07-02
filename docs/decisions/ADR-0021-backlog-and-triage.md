# ADR-0021: Backlog and triage process

**Status:** Accepted
**Date:** 2026-07-02
**Deciders:** Edu + Claude

## Context

Pure design freezes fail. New ideas emerge from playtesting, from playing other games, from shower thoughts. Pretending otherwise leads to either:
- **Freeze violation** — ideas silently scope-creep into the current milestone
- **Idea loss** — good ideas forgotten because they had nowhere to go
- **Analysis paralysis** — every idea triggers a full design discussion

We need a lightweight process for capturing ideas without either dropping them or derailing.

## Decision

**Introduce `IDEAS_BACKLOG.md` at repo root.** Every new idea gets captured immediately with a triage tag:

- `[trivial-content]` — new item, monster, building, recipe, decorative asset. Cheap to add whenever. Not architectural.
- `[post-slice]` — real feature; add to PRD roadmap after M9.
- `[slice-affecting]` — would change the vertical slice scope. Requires ADR discussion before accepting.
- `[rejected]` — considered and declined. Kept with a "why not" note so we don't relitigate.

Triage cadence: at each milestone review. Ideas move to PRD roadmap, get scheduled, or stay in backlog.

## Consequences

**Positive:**
- No idea is lost
- No argument about "should we discuss this now?" — the answer is always "no, backlog it"
- Ideas that resurface get "why not" pointer to their rejection
- Milestone reviews become the natural triage point
- The freeze remains real during a milestone; creativity remains free
- Written record of the design's evolution

**Negative:**
- One more file to maintain
- Requires discipline to actually triage rather than let the backlog grow forever

## Alternatives considered

- **Everything into the PRD immediately.** Rejected — makes PRD volatile and clutters roadmap.
- **Freeze strictly; new ideas verbal only.** Rejected — Edu explicitly noted new ideas will arise; forcing them to be verbal-only loses them.
- **GitHub Issues as backlog.** Deferred — will migrate once repo is on GitHub. Markdown file works for now.

## References

- PRD.md §7 (non-goals is the "why not" record for rejected)
- Edu's Operating Instructions in CLAUDE.md
