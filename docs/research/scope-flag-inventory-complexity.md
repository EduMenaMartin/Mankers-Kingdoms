# Scope Flag: Inventory System Complexity vs. Original M3 Estimate

**Date:** 2026-07-02
**Raised during:** Design of `docs/gdd/inventory.md`
**Status:** Flagged, not yet resolved — needs a decision, not urgent to block current work

---

## What changed

`VERTICAL_SLICE.md` §3.5 and §3.10 originally described the inventory system in one line: **"grid, drag-drop, functional not pretty."** That phrasing implied something simple — a basic slot grid like early Minecraft or Stardew Valley.

The actual locked design in `docs/gdd/inventory.md` is:
- **Hybrid capacity** (grid footprint AND weight cap, both enforced)
- **Shape-based grid** — items occupy rectangular W×H footprints, with rotation support
- **Separate Storage Chest system** with a dual-pane transfer UI

This is much closer to **Escape from Tarkov / Diablo-style inventory Tetris** than a simple slot grid. That's a deliberate, good design choice for the game's identity — not a mistake — but it's a heavier build than the original estimate assumed.

## Why this adds real time

Compared to a simple slot grid, this design requires:
- Placement validation logic (does this W×H shape fit at this grid position, accounting for already-occupied cells)
- Rotation logic (swap W/H, re-validate against the grid)
- Drag-preview rendering (ghost outline following the cursor, showing valid/invalid placement in real time)
- Dual-pane UI for the Storage Chest (two synced grids, drag between them)
- Server-side validation for all of the above (per ADR-0005 — client prediction + server correction, not just client-side placement)
- Weight-cap enforcement layered on top of grid placement (both checks must pass)

None of this is exotic or risky engineering — it's well-trodden territory (many games do exactly this) — but it is measurably more work than a basic grid, and it touches UI, networking, and server validation all at once.

## What I'd like from you (Claude Code)

1. **Give an honest estimate** of how much additional time this adds to M3 compared to the original "basic grid" scope, based on what you can see of the current codebase and how much of the surrounding systems (networking, server-authoritative validation patterns) already exist to build on.
2. **Tell me if you'd recommend phasing it** — e.g. ship M3 with a simple slot grid first (to unblock testing hunger/rest/build systems that depend on inventory existing at all), then add shape-based placement as a follow-up pass before M4, rather than building the full Tetris-grid system in one go.
3. **Flag any risk** you see in building this now vs. deferring the shape/rotation complexity to later — for instance, if a simple grid now would require a painful rework to become shape-based later (data model incompatibility), versus if the two can coexist cleanly (start simple, upgrade the placement logic without touching the data schema).

Don't just proceed and build the full system silently — this is exactly the kind of scope shift that should get a quick "here's my recommendation" back to me before implementation starts, per CLAUDE.md's Operating Instructions (task capture before implementation, PRD-first).

## Reference

- `docs/gdd/inventory.md` — full design, all sections
- `docs/gdd/inventory.md` §7 — the original scope note where this was first raised
- `VERTICAL_SLICE.md` §3.5, §3.10 — the original one-line descriptions that undersold the complexity
- ADR-0005 — server-authoritative validation pattern this must follow
