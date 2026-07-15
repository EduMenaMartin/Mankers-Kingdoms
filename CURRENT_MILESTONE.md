# Current Milestone: M9 — Vertical slice playtest

**Started:** 2026-07-15
**Target demo:** Play with a friend for a real 30–60 minute session. Log what breaks, what confuses, what feels bad. Fix critical bugs only.

## Scope (from VERTICAL_SLICE.md §3.6)

- Play a real session with another person (not solo dev testing)
- Both players on the same world (host + join over LAN/ENet)
- Log: crashes, confusing UI, blocking bugs, missing feedback
- Fix: P0 (crash) and P1 (major blocker) bugs found during playtest
- Do NOT add features — this is a bug-fix and quality pass only

## Success criteria (VERTICAL_SLICE.md §6)

After the M9 playtest, all three must be yes:

1. **Fun.** Two people want to keep playing after the first session.
2. **Legible.** Both players understand what to do next without developer help.
3. **Stable.** No crashes or state corruption during the session.

## Out of scope for M9

- New buildings, monsters, items, classes
- UI redesigns
- Performance optimization
- Content additions
- Inventory Phase B (shape-based grid — post-slice)
