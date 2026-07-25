# Mankers Kingdoms — World Structure & Map Generation (GDD)

**Status:** v0.1 — **two competing long-term models, NOT yet chosen.** Not scheduled to any milestone. v1 vertical slice's single bounded map (`VERTICAL_SLICE.md` §3.8) is unaffected by either option and serves as a working building block for both.

**Related:** `PRD.md` §4.8, `VERTICAL_SLICE.md` §3.8, `docs/gdd/settlements.md`, ADR-0015 (conquest mechanic), ADR-0022 (determinism policy)

---

## 0. Why this document presents two options, not one vision

This started as a single drafted vision (two-tier World Map + Local Map, RimWorld/Stronghold-style). During further discussion, a second, meaningfully different model came up — a single continuous seamless world with fog-of-war exploration (7 Days to Die / Valheim-style) — prompted partly by a claim that "people complain about the RimWorld-style world map." That claim was fact-checked (§4) and found to be **imprecise but pointing at something real**, which is exactly why this is worth treating as a genuine open fork rather than picking one and moving on.

**Neither option is decided.** Both are compatible with everything currently locked in the PRD and ADRs. This document exists so the fork is visible and deliberate, not accidentally decided by whichever gets implemented first.

---

## 1. Option A — Two-tier World Map + Local Map (RimWorld / Stronghold model)

### 1.1 The model

- **World Map tier** — an abstract, non-real-time overworld made of tiles, each with a biome (or biome mixture). A strategic/navigational view, not a playable 3D space.
- **Local Map tier** — the actual played, real-time 3D scene (WASD avatar, buildings, combat — everything the vertical slice already builds). Generated when a player travels to a world tile, reflecting that tile's biome(s) (including blending at biome boundaries).
- Travel between Local Maps happens via the World Map screen — not by walking continuously.

### 1.2 Why this is the lower-engineering-cost option

- **v1's current single bounded map already IS one Local Map instance.** No rework needed to existing terrain generation, resource placement, or village/nest spawning.
- No chunk-streaming engineering required — each Local Map is small enough to load entirely into memory at once, exactly like v1 today.
- World Map generation is cheap (a 2D biome/feature assignment, not full terrain) and can be generated once per world seed, consistent with the determinism policy (ADR-0022).
- **Local Maps can be lazily generated** — full detail only computed the first time a player actually visits that tile, keeping world-creation time and save size manageable even as the World Map grows large.

### 1.3 Conquest tie-in (confirmed direction if this option is chosen)

Conquest (ADR-0015) implies traveling to a **separate Local Map** — an enemy settlement exists on its own World Map tile with its own Local Map instance. Raiding = travel via World Map -> engage in that tile's Local Map -> capture buildings there. This gives ADR-0015's "satellite settlement" concept an actual spatial/travel mechanic.

### 1.4 Persistence model

Two-speed: World Map layer (all tiles' biome/feature assignment) is cheap and can be fully generated upfront. Local Map layer (full terrain, structures, NPC state) is expensive and only generated/persisted for tiles a player has actually visited or settled.

### 1.5 Multiplayer implications — the hardest open question either option faces

**Can the coop party split across different Local Maps simultaneously?** (One player raiding a distant tile while another manages home.) Two paths:

- **A1. Party always travels together.** Simplest — server only ever simulates one "active" Local Map at a time (plus the always-hot home settlement). Matches coop-presence pillars (ADR-0003, ADR-0004) most directly.
- **A2. Party can split.** More like RimWorld's caravan system — different players can be in different Local Maps concurrently, each fully simulated in real time. Real architectural cost: more server tick load, more complex per-client networking (ARCHITECTURE.md §4).

**Draft opinion (not locked):** home settlement's Local Map always stays "hot"; other Local Maps are hot only when a player is present (or always-hot if owned/conquered, dormant if unclaimed wilderness). Leans toward A2, scoped so server load grows only with *owned* locations.

This question is **shared with Option B** (§2.4) — it doesn't go away regardless of which world model is chosen.

---

## 2. Option B — Continuous seamless world with fog of war (7 Days to Die / Valheim model)

### 2.1 The model

One large, continuous, walkable world. No map-screen travel — the player just walks (or eventually rides/sails) across it. A **fog-of-war** overlay tracks what's been explored:

- **Unseen** — black, never rendered on the map
- **Previously seen** — greyed out / desaturated, revealed but not currently visible
- **Currently visible** — full color, a circle (vision radius) around the player

This is arguably a more natural fit than it first appears: PRD §4.2 already locks in "Valheim-model persistence," and Valheim itself uses exactly this continuous-world approach — this isn't a random departure from what's already committed, it's arguably closer to the stated primary reference.

### 2.2 Why this is the higher-engineering-cost option

No real game — including 7 Days to Die — actually simulates an entire large world at full fidelity simultaneously; that's computationally impossible at any real size. What they do instead is **chunk streaming**: the world persists as data on disk, but only a bubble around active players loads into memory and actively ticks. This requires real, nontrivial engineering we don't currently have:

- Async chunk loading/unloading without stutter
- Per-chunk save/load (not per-whole-map, which is what v1's current save system does)
- LOD (level of detail) management for distant terrain
- **Networked area-of-interest culling** — the server must track which chunks matter to which connected players and only stream relevant state to each client. This becomes *mandatory*, not a later optimization, the moment the world exceeds what fits in one screen's worth of state.

This directly stresses two budgets already locked in ARCHITECTURE.md: the <=25ms/tick server budget (§13) and the <30 KB/s per-client bandwidth target (§4.5) — both assume a small, fully-loaded scene today, not a streamed world with distant hot zones.

### 2.3 Fog of war — the easy part, same either way

Fog of war itself is well-understood and **roughly the same implementation difficulty regardless of which option is chosen**:

- A 2D data layer (separate from the 3D world) tracks per-cell state: unseen / previously-seen / currently-visible
- Implemented as a texture mask sampled against player position; a shader greys out or blackens the map render based on the mask
- Updates every tick: cells within vision radius -> "currently visible"; cells that were visible but aren't anymore -> demoted to "previously seen," never reverting to black

This can be layered onto **either** Option A (fog of war per Local Map, revealed as you explore that bounded scene) or Option B (fog of war across the single continuous world) — it's an independent feature, not a reason to pick one option over the other.

### 2.4 Multiplayer implications

Same core question as §1.5 — can players be far apart and both be "live," or does the game keep the party together? Option B makes this question unavoidable rather than optional: a genuinely large continuous world with real distances between players *requires* an answer, whereas Option A can defer it by simply not letting the party split (A1) if that's the easier path.

---

## 3. Fact-check: "people complain about the RimWorld-style world map"

This claim came up in discussion and was checked directly rather than assumed. **Verdict: real signal, but imprecise as stated.**

**What's actually true:** RimWorld players have extensive, well-documented complaints about the **caravan system's logistics** — packing animals, pawns falling asleep mid-load, unpredictable travel-time formulas, general tedium in *setting up* a trip. A long-running community thread titled "Fix the caravans!" and multiple dev-tracker posts confirm this is a real and persistent frustration, going back years across multiple game updates.

**What's NOT well-supported:** widespread complaint about the *abstract-travel concept itself* (i.e., "I wish the world map were a seamless walkable space instead"). The complaints found are about execution friction in RimWorld's *specific* caravan implementation, not a rejection of menu-driven world travel as a paradigm.

**What IS well-supported, as a separate and legitimate point:** genre-wide game design commentary consistently rates seamless, walkable exploration (Valheim, Subnautica) as delivering a specific, valued feeling — building "a mental map from memory and fear" rather than clicking icons on a screen — that abstract/menu-driven travel doesn't replicate. This is a real, defensible design preference, just a different and more precise claim than "people hate RimWorld's system."

**Practical takeaway:** if Option A is chosen, the lesson from RimWorld isn't "abandon the concept" — it's **"if we build abstract travel, keep the travel-execution mechanics simple and low-friction, unlike RimWorld's caravan packing/logistics."** If Option B is chosen, it's chasing a real and well-documented positive feeling (exploration reward), not just avoiding a disliked mechanic — but it comes with the real engineering cost in §2.2.

---

## 4. Relationship to v1 / VERTICAL_SLICE.md

Neither option changes v1's scope. The vertical slice's single bounded map (§3.8) works as: (a) the first Local Map instance if Option A is eventually chosen, or (b) a same-sized starting region of a larger continuous world if Option B is eventually chosen. **This decision can be deferred well past M9** without cost — nothing about the vertical slice depends on which way this goes.

The decision becomes load-bearing at the point ADR-0015 (conquest) moves from roadmap to active implementation, since conquest requires *some* answer to "how do you get from your settlement to the enemy's" — that's the natural forcing function for finally choosing between A and B, not before.

---

## 5. Modding surface

- **Option A:** World Map biome distribution, per-tile feature spawn tables, and biome blend rules are strong candidates for data-driven definition (ADR-0009).
- **Option B:** Chunk-based biome/feature generation rules would similarly be data-driven, though the underlying chunk-streaming code itself is not modding-exposed in either option.

---

## 6. Open questions

### Shared by both options
1. **Can the coop party split across separate simulated spaces simultaneously?** (§1.5 / §2.4 — the single most important open question in this document, and the one most likely to force a decision between A and B early, since Option B makes it unavoidable while Option A can defer it.)
2. Final biome list and count (companion to PRD §10's existing open questions).
3. Transport/economy mechanic between settlements — literal travel-with-risk vs. abstracted instant transfer (ties to ADR-0015's economic layer either way).

### Option A-specific
4. World Map scale — tile count, real-world distance per tile.
5. Travel time model — abstracted/instant vs. real elapsed in-game time (RimWorld-caravan-style, but keep it low-friction per §3's lesson).
6. World Map visual style — painterly overworld (RimWorld-style) vs. abstract region-select (closer to original Stronghold).
7. Settlement-to-tile relationship — does founding claim a whole tile, or a footprint within a Local Map shared by multiple settlements?
8. Biome blending rules at tile boundaries.
9. Regional/kingdom-level grouping above individual tiles (Stronghold-style)?

### Option B-specific
10. Realistic world size given our tick/bandwidth budgets (ARCHITECTURE.md §13, §4.5) — needs a technical spike/prototype before committing, not just a design decision.
11. Chunk size and streaming radius — balancing visual draw distance against server/network load.
12. How does settlement founding/ownership work in a continuous world — is there still a discrete "claimed area," or is ownership itself a zone painted onto continuous terrain?
13. How does conquest work spatially — walking/traveling overland to a rival settlement's location within the same continuous world, with real distance and real travel risk along the way (this is arguably a more natural fit for "conquest" as raiding than Option A's map-screen jump, but costs the most to build).

---

*This document will be revisited once ADR-0015 (conquest) moves from roadmap to active implementation — that's the point this decision becomes load-bearing rather than aspirational. Until then, both options remain open and neither blocks any current milestone.*

---

## 11. Early probe: v1 fog of war (pulled forward for M9 playtest signal)

**Status:** locked, scoped to v1 only. Distinct from §1–10's deferred Option A/B world-structure decision — this section applies §2.3's already-designed fog-of-war mechanic to the current single bounded map, specifically to generate real playtest signal on whether fuller map-exploration mechanics are worth investing in later. It does not commit to Option A or Option B; §2.3 was explicitly written as world-model-independent, which is exactly why this is possible without resolving the bigger fork.

### 11.1 Scope

Applied to v1's existing single ~500×500 tile bounded map (`VERTICAL_SLICE.md` §3.8) — not a new world-tier structure, not a new map system. One map, one fog layer on top of it.

### 11.2 Reveal model — shared across the party (locked default)

**Exploration state is shared across all players in a settlement/world**, not tracked per-player individually. If any player has visited a location, it's revealed for everyone. Chosen for simplicity — this is a scoped probe meant to generate signal cheaply, not a permanent system worth its own sync/networking complexity. Revisit if the full system (post-conquest, per §7's original scope) ends up wanting per-player exploration instead.

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
3. **GPU-level terrain optimization (Option B only, explicitly not scheduled):** If Option B (continuous seamless world) is ever chosen over Option A (many small bounded maps), revisit GPU-level terrain techniques from session research: UV-free vertex shaders using `gl_VertexID` for position generation, compressed normals, triangle strips instead of individual triangles, and LOD with terrain-sinking to hide seams between detail levels. Not relevant if Option A is chosen — per-instance map size would stay comparable to what already exists. Tied to the Option A/B decision, not to general project timeline.
4. **River-network generation (Option B only, explicitly not scheduled):** If Option B (continuous seamless world) is ever chosen, revisit GDQuest's heightmap-driven river-network generation approach — multiple branching rivers routed across a whole world map rather than a single river per bounded map. Under Option A's current single-map-per-instance model this is not relevant: a single `RiverGenerator` walk suffices. Under Option B the problem becomes substantially different (confluence handling, drainage basins, avoiding self-intersection across a large persistent world). Research this together with item 3 above, not independently.
