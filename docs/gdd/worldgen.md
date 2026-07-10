# World Generation GDD

*Sections §1–§10 are pending — to be authored and added here.*

---

## 11. Early probe: v1 fog of war (pulled forward for M9 playtest signal)

**Status:** locked, scoped to v1 only. Distinct from §1–10's deferred Option A/B world-structure decision — this section applies §2.3's already-designed fog-of-war mechanic to the current single bounded map, specifically to generate real playtest signal on whether fuller map-exploration mechanics are worth investing in later. It does not commit to Option A or Option B; §2.3 was explicitly written as world-model-independent, which is exactly why this is possible without resolving the bigger fork.

### 11.1 Scope

Applied to v1's existing single ~500×500 tile bounded map (`VERTICAL_SLICE.md` §3.8) — not a new world-tier structure, not a new map system. One map, one fog layer on top of it.

### 11.2 Reveal model — shared across the party (locked, confirmed)

**Exploration state is shared across all players in a settlement/world**, not tracked per-player individually. If any player has visited a location, it's revealed for everyone. Confirmed by Edu. Chosen for simplicity — this is a scoped probe meant to generate signal cheaply, not a permanent system worth its own sync/networking complexity. Revisit if the full system (post-conquest, per §7's original scope) ends up wanting per-player exploration instead.

### 11.3 Where it lives — separate from the minimap

The existing minimap (`VERTICAL_SLICE.md` §3.10 — "top-down, showing settlement + player + nearby entities within radius") is **unaffected** and stays exactly as locked — it shows what's currently nearby, no memory, no fog states.

This is a **new, separate, toggle-able full-map screen** (a keybind to open/close, e.g. "M") showing the entire bounded map with the three-state overlay from §2.3:
- **Unseen** — black
- **Previously seen** — greyed/desaturated
- **Currently visible** — full color, within the player's vision radius (reuse the same radius value already used for the minimap's "nearby entities" range — don't invent a second number)

### 11.4 Persistence

Exploration state is a small addition to the existing single-world save (`ARCHITECTURE.md` §8) — a 2D revealed/unrevealed data layer over the map, saved and loaded alongside everything else already locked for M8. Not a new persistence system.

### 11.5 Milestone placement

**M8** (`VERTICAL_SLICE.md` — Save/load and polish), not a new milestone. M8 already touches save/load; this rides along with that work rather than needing its own slot.

### 11.6 What this probe is FOR

Purpose: give real players something to react to during the M9 playtest, specifically to gather signal on whether map-exploration/fog-of-war mechanics are worth investing further in — informing (not pre-deciding) the eventual Option A vs. Option B choice in §1–§10 above. Post-M9, review alongside the other two success-criteria questions (`VERTICAL_SLICE.md` §2) whether this specific feature helped or was noise.

### 11.7 Open questions

1. ~~Confirm shared (not per-player) reveal~~ — **confirmed by Edu, locked.** Shared/party-wide reveal is final for this probe.
2. Full-map screen visual treatment (simple 2D top-down render vs. something stylized) — low priority, doesn't affect the mechanic being tested.
