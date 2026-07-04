# Mankers Kingdoms — Vertical Slice Definition

**Status:** v0.1 — this is the definition of done for the playable prototype. All content lists are v1 scope, expected to evolve. Success or failure of this vertical slice determines whether Mankers Kingdoms is worth continuing to build.

**Target timeline:** ~3 months of focused solo work. Not a promise, an estimate.

---

## 1. Purpose

Prove that the core loop — **WASD coop + class-picked character + station-based NPC recruitment + settlement building + real-time survival** — is fun.

If it is fun, everything else is content, polish, and scale.

If it is not fun, we learn that in 3 months instead of 3 years.

This is a **discovery vehicle**, not a shippable product. It will look bad, feel rough, and have obvious missing features. That is by design.

---

## 2. Definition of Done

The vertical slice is complete when **two people can sit down in front of two PCs, connect over Steam, and play for 30–60 minutes without needing the developer to intervene**, and can honestly answer:

- **Do we want to keep playing?**
- **Do we understand what to do next when we log in tomorrow?**
- **Does class + skill + settlement feel meaningfully different from Valheim?**

If the answer to all three is yes, the slice succeeded regardless of graphics quality, missing features, or rough edges.

---

## 3. Scope — IN

### 3.1 Multiplayer

- 2 players over Steam (using GodotSteam)
- Host + 1 client architecture, but with the **server code path already isolated** to enable dedicated server later without refactoring
- Character progression persists across sessions
- Save/load the whole world including all pawns, buildings, and inventory
- Basic disconnect/reconnect handling (client can rejoin an ongoing session)

### 3.2 Character creation

- **2 selectable classes:** Fighter (melee-primary) and Ranger (ranged-primary). Rationale in §7.
- **4 stats** rolled on character creation: Strength, Dexterity, Constitution, Wisdom
  - Stat generation: 3d6 straight down or 4d6-drop-lowest (final choice deferred; both trivial to implement)
- **Class picks provide:** starting kit (2–3 items — Fighter: sword + shield; Ranger: shortbow + hunting knife), initial skill bumps (see §3.3), cosmetic differentiation (armor color or simple sprite variant)
- Character rerolls unlimited pre-commit; character is permanent after entering world

### 3.3 Skills

Six skills for prototype, drawn from the framework in `docs/gdd/skills.md`:

| Skill | Group | Grows via | Stat cap | Class bonus |
|---|---|---|---|---|
| Melee | Physique | Combat with melee weapons | Str | Fighter +5 |
| Ranged | Physique | Combat with bows | Dex | Ranger +5 |
| Athletics | Physique | Running, jumping, carrying | Str/Con (higher) | Fighter +3 |
| Woodcutting | Trades | Chopping trees | Str | — |
| Foraging | Trades | Gathering plants/herbs | Wis | Ranger +3 |
| Cooking | Trades | Preparing meals at Cooking Fire | Wis | — |

Additional skill scaffold for Ranger class kit: **Stealth** (+3), but with limited functionality in v1 (movement noise reduction only; no full stealth mechanics).

Skill level range: 1–99. Effective ceiling = **floor(99 × stat / 18)**, or equivalently floor(stat × 5.5). Example: character with Str 8 caps Melee at 44; character with Str 18 caps at 99. Full cap table in `docs/gdd/skills.md` §3.1.

**Tool tier gating on Trades skills:** Woodcutting unlocks bronze axe at level 15 (vs. starting stone hand-axe). Foraging unlocks herbalist's sickle at level 15. Cooking unlocks stew pot at level 10. Aska-style progression; specific numbers tuned in balancing.

Medicine, Craft/Mend, Perception, Survival, Stamina, Agility, and Charisma/Intellect/Magic-group skills are **defined in the framework but not exposed in v1 gameplay**. Their data files exist as scaffolds so post-slice work is additive, not architectural.

### 3.4 Combat

- **Real-time melee and ranged.** No player magic.
- **Melee (Fighter primary):** directional swing/block system — click to swing in facing direction, hold to block. Simple stamina cost. Basic hit detection.
- **Ranged (Ranger primary):** aim with mouse cursor, click to fire arrow projectile. Arrow has travel time and trajectory (arc), can miss. Basic hit detection on impact. Ammo consumed from inventory (arrows craftable at Workbench from wood).
- Enemies use the same combat systems as players. At least one enemy variant uses ranged attacks in v1 (proposed: Bandit Archer variant, using the same ranged code path).
- Death: player respawns at their settlement's respawn point with configurable penalty
  - Default penalty: drop all carried inventory at death site (recoverable), lose 1 level in highest skill, no XP debt
  - Toggleable at world creation

### 3.5 Settlement

- **1 buildable settlement** per player, or shared founder + guests
- Founder plants a **Kingdom Marker** to found; only one per player
- **6 building types in v1:**
  1. **Shelter** (basic housing, enables respawn point)
  2. **Storage Chest** (shared inventory)
  3. **Workbench** (crafts tools and basic gear)
  4. **Woodcutter's Post** (station for NPC or player woodcutting)
  5. **Cooking Fire** (converts raw food to meals, restores hunger)
  6. **Wooden Wall + Gate** (basic defense)
- All 6 are founder-gated (no class requirements in v1 since only 2 classes exist)
- Presence-gating **implemented but with only 1 gated example**: the **Herbalist's Hut** unlocks when a Ranger (or Ranger-recruit) is present, produces bandages and antidotes. This proves the mechanic without demanding a full class-per-building content build.

### 3.6 NPCs

Five NPC entity types in v1:

| Entity | Role | Behavior |
|---|---|---|
| **Villager** | Recruitable civilian in the procedural village | Idles, works villagers' assigned tasks, recruitable via dialogue |
| **Bandit** | Hostile humanoid | Patrols; attacks players on sight |
| **Wolf** | Hostile beast | Wanders; attacks below-threshold-health prey |
| **Goblin** | Hostile humanoid | Nests in one location; aggressive; drops crafting mats |
| **Orc** | Elite hostile | Rare; guards higher-value nests; harder combat encounter |

Each has: rolled stats, 1 hidden archetype tag (combatant/artisan/scholar/scout — used for Villagers, dormant for hostiles in v1 as pattern proof), basic pathfinding, needs (hunger + rest, applied only to recruited pawns).

**Recruited villager behavior:**
- Assigned to a **station** (Woodcutter's Post, Cooking Fire, etc.)
- Executes station job loop until interrupted by needs (hunger/rest) or combat
- Levels up appropriate skills as they work
- Can be un-assigned and re-assigned freely

### 3.7 Village

- **1 procedural village** spawned on world generation
- 6–10 generated villagers with rolled stats, archetype biases, and randomized names
- Villagers are recruitable via a simple dialogue: talk → offer to join (based on a minimal happiness/trust check that in v1 is effectively always successful) → they leave village and follow player to settlement
- No village population growth in v1 (defer even the "abstract passive growth" — v1 villages are static number of villagers)

### 3.8 World

- **Bounded procedural map, ~500×500 tiles** (roughly 250m × 250m in-world; ~5–10 minute walk corner-to-corner)
- **One biome** — temperate grassland/forest mix. No biome variety in v1.
- Terrain features: trees, rocks, water tiles, one river, one small settlement footprint at spawn, one procedural village elsewhere on the map, 2–3 monster nests
- Day/night cycle: 20-minute in-game days
- Basic weather: clear or overcast (visual only, no gameplay effect in v1)

### 3.9 Needs

Player and recruited NPC needs:
- **Hunger:** ticks down over time; eat food to restore; reaches 0 → gradual health loss
- **Rest:** ticks down over time; sleep in Shelter at night to restore; low rest → reduced skill growth rate

Not in v1: thirst, warmth, mood, sanity, disease. Deferred to later milestones.

### 3.10 UI

Minimum viable:
- **Main menu:** Start Solo / Host Multiplayer / Join Multiplayer / Options / Exit. Nested Options (audio, graphics basics, controls). Language selector present (v1 exposes English only, but selector UI exists so extra language files added later Just Work).
- Health / hunger / rest bars
- Inventory panel (grid or list, functional not pretty)
- Character sheet (stats + skill levels + tool tier progress)
- Build menu (list of buildable structures, with class-gate indicators)
- NPC assignment panel (list of unassigned NPCs and stations)
- Chat box for player-to-player text communication
- Minimap (top-down, showing settlement + player + nearby entities within radius)
- Language file dropdown in Options (v1 English only; hooks for future)

Not in v1: quest journal, achievement UI, cosmetics menu, options beyond above basics, tutorial.

### 3.11 Modding infrastructure

- Mod loader stub that boots the game as a content pack from `/mods/base/`
- All NPC types, monster types, buildings, items, and skills defined in `.tres` files or JSON, loaded at startup
- Stable string IDs everywhere
- **No actual community modding support in v1** — no mod discovery UI, no version checking, no MP mod handshake. But the *shape* is correct: adding a second mod folder should Just Work at the code level, even if we don't advertise it.

### 3.12 Persistence

- Save on host exit (or every 5 minutes as autosave)
- Load resumes world state including all pawns, needs, positions, inventories, buildings, day/time, and character progression
- Save format: JSON for prototype (readable, moddable, easy to debug). Binary format for performance is a later optimization.
- **Schema version field from day one.** Migration path stubbed but not tested.

---

## 4. Scope — OUT

Explicitly deferred. Do not build these in the vertical slice, no matter how easy they seem. Every one of these has been considered and cut on purpose.

- **Player magic** (deferred to post-slice)
- **More than 2 classes**
- **More than 6 skills** (excluding Stealth scaffold and unimplemented framework scaffolds)
- **More than 4 stats** (Int and Cha to be added post-slice)
- **Alignment win conditions** (only sandbox mode in v1)
- **Boss encounters**
- **Baby / aging simulation** (villages don't even passively grow in v1)
- **Reactive or competitive enemy AI**
- **Enemy settlements as claimable objectives** (conquest mechanic — deferred to 0.1→0.3)
- **Economic layer** (resource/manpower trading between settlements — deferred with conquest)
- **Random encounter NPC sources** (caravans, nomads, refugees — deferred to 0.1→0.3)
- **Village population growth** (v1 villages are static NPC count)
- **Multiple biomes**
- **Open streaming world**
- **Mounts, ships, fast travel**
- **PvP**
- **More than 2 players concurrently** (design supports it, prototype only tests 2)
- **Character customization beyond class**
- **Full-featured quest system**
- **Village diplomacy beyond simple recruit-yes/no**
- **Ownership permission tiers** (founder is only elevated role in v1)
- **Trade with villages**
- **Weather effects on gameplay**
- **Seasons**
- **Complex crafting trees** (recipes are 1-tier only in v1)
- **Advanced NPC social simulation** (relationships, morale beyond needs)
- **Dedicated server binary distribution** (code path exists; binary distribution deferred)
- **Steam Workshop integration**
- **More than one language exposed** (English only in v1; framework supports adding languages via file drop)
- **Tutorial / onboarding**
- **Steam achievements / cloud saves**
- **Options menu beyond basics**

---

## 5. Milestones

Each milestone is small, demoable, and unambiguous. Each takes 1–4 weeks depending on complexity. Order matters.

### M0 — Project scaffolded (1 week)
- Repo initialized with folder structure per ARCHITECTURE.md
- Godot 4 + C# project runs, opens a window with "Mankers Kingdoms" splash
- CI (basic) runs tests on push
- All foundational docs live in repo
- Localization file scaffold (`data/lang/en.json`) — even if only "Mankers Kingdoms" string is in it
- **Demo:** Godot window opens on both dev PCs with the project, splash reads from localization file.

### M1 — Main menu and two clients see each other (2–3 weeks)
- Main menu with Start Solo / Host Multiplayer / Join Multiplayer / Options / Exit
- Options menu with audio, graphics basics, language dropdown (English only exposed)
- Local host + 1 client connect over LAN using GodotSteam via menu buttons
- Both players see WASD-controllable placeholder capsule/sprite representing each other
- Movement is server-authoritative with client prediction for local player
- **Demo:** From menu, one player hosts, other joins, both run around an empty plane and can see each other move smoothly.

### M2 — World with things in it (2 weeks)
- Procedural terrain generation (simple: heightmap → grass, tree placement, rock placement)
- Day/night cycle
- Basic physics for player movement (walking on terrain)
- Trees can be chopped → yield wood (Woodcutting skill increments)
- **Demo:** Two players explore a small procedural map and chop trees.

### M3 — Settlement basics (3 weeks)
- Kingdom Marker plantable
- Shelter, Storage, Workbench buildable using resources
- Death → respawn at Shelter with penalty
- Basic inventory system (grid, drag-drop, drop-on-death)
- Hunger + rest needs functioning
- Cooking Fire buildable; raw food → cooked food
- **Demo:** Two players cooperatively build a shelter, sleep, eat, and continue.

### M4 — Combat and monsters (3–4 weeks)
- Directional swing/block melee combat
- Ranged combat: bow, arrow projectiles with trajectory, arrow crafting at Workbench
- Fighter kit (sword + shield) and Ranger kit (shortbow + hunting knife)
- Wolves and goblins spawn on map, attack players (melee variant)
- Bandit Archer variant on map (ranged AI, reuses same ranged code)
- Player and NPC damage system with health persistence
- Death penalty applied on player death
- Monster nests spawn goblins/bandits
- **Demo:** Two players — one Fighter, one Ranger — clear a bandit camp cooperatively using both combat styles.

### M5 — Class, stats, skills, and inventory panel (3–4 weeks)
- Class selection at character creation (Fighter, Ranger) with class kits distributed
- Stats rolled (3d6 or 4d6-drop-lowest — final call before implementation) and displayed
- Skill system live: 6 skills (Melee, Ranged, Athletics, Woodcutting, Foraging, Cooking) grow through use, capped by stats
- Character sheet UI shows stats, skills, and tool tier progress
- **Inventory UI panel — Phase A:** simple slot list, `I` key to open; reuses existing `PlayerInventory` dict backend; no shape-based placement (Phase B is post-slice)
- Tool tier progression: at least one skill (Woodcutting) unlocks a better axe at level 15
- **Demo:** Player creates a Ranger, chops wood, watches Woodcutting skill level up, unlocks bronze axe at 15, hits stat ceiling and stops.

### M6 — Village and recruitment (3 weeks)
- Procedural village generates with 6–10 villagers
- Villagers have rolled stats, hidden archetype tags, generated names
- Simple dialogue interface for recruitment
- Recruited NPC follows player, can be assigned to a station
- Station-based NPC job loops (Woodcutter's Post drives an NPC to chop trees automatically; Foraging station for herbs)
- NPC needs (hunger/rest) tick down; NPCs return to Shelter to sleep
- **Demo:** Player travels to village, recruits a high-Str villager, brings them home, assigns to Woodcutter's Post; NPC chops trees while player does something else.

### M7 — Class-gated building (1–2 weeks)
- Herbalist's Hut buildable only when Ranger class present in settlement
- Foraging skill produces herbs at the Hut
- Bandages craftable from herbs at Hut (used with Medicine — but Medicine deferred; use fixed "heals X HP" for v1)
- Presence-gating logic tested with player leaving settlement + coming back
- **Demo:** Fighter alone can't build Herbalist's Hut → recruits Ranger villager → hut becomes buildable → Ranger leaves settlement → hut becomes non-functional (behavior TBD: locked or dormant).

### M8 — Save/load and polish (2 weeks)
- JSON save/load of full world state
- Autosave every 5 minutes + on host exit
- Client reconnect handling
- All existing systems tested with save→quit→reload cycle
- Basic UI polish (readable, not pretty)
- Localization file audit — confirm no hardcoded strings remain in gameplay code
- **Demo:** Play for 30 minutes, quit, restart, resume exactly where left off.

### M9 — Vertical slice playtest (2 weeks)
- Play with a friend for a real 30–60 minute session
- Log what breaks, what confuses, what feels bad
- Fix critical bugs only; do not add features
- Answer the three definition-of-done questions honestly
- **Demo:** The vertical slice itself.

**Rough total: ~22–26 weeks / 5–6 months** with realistic slack. Ranged combat and main menu shifted the estimate slightly upward. This is a discovery vehicle, not a commercial deadline.

---

## 6. Success criteria

The vertical slice **succeeded** if, after the M9 playtest, all three answers are yes:

1. **Fun.** Two people want to keep playing after the first session.
2. **Legible.** Both players understand what to do next without needing developer help.
3. **Differentiated.** The experience feels distinct from just playing Valheim — the class + skill + stat + station loop provides something Valheim doesn't.

The vertical slice **partially succeeded** if any of the three is a "yes with caveats." Iterate on the failing dimension for one more milestone before making a go/no-go decision.

The vertical slice **failed** if two or more are no. In that case: honest post-mortem, decide whether to pivot or shelve. Do not just push forward.

---

## 7. Rationale — why Fighter and Ranger for v1?

The vertical slice needs two classes to prove that class differentiation matters. The **Fighter (melee) + Ranger (ranged)** split is chosen because it:

- **Tests both combat systems in v1 with minimal class content.** Fighter validates the melee swing/block system; Ranger validates the ranged projectile system.
- **Creates meaningful stat divergence.** Fighter is Str-primary (Melee, Athletics); Ranger is Dex-primary (Ranged, Stealth) with Wis-secondary (Foraging). Proves that stats matter.
- **Enables the class-gated building demo cleanly.** Ranger's Foraging skill drives the Herbalist's Hut, the presence-gated building in v1.
- **Are archetypal.** Both classes exist in every likely final class list; investment here is not wasted.

Rejected alternatives:
- **Fighter + Cleric**: Cleric implies healing magic; we defer magic to post-slice.
- **Fighter + Rogue**: Rogue-with-bow is a thematic compromise (Rogues are canonically melee/stealth); Ranger fits ranged combat more naturally.
- **Fighter + Wizard**: Wizard requires the magic system; deferred.
- **Fighter + Barbarian**: too similar in role — both melee brawlers, minimal differentiation.

This is expected to evolve. At 1.0 we target 5–7 classes. Fighter and Ranger exist in every likely final list.

---

## 8. Risks and mitigations

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Netcode desyncs eat weeks | Medium | High | Authoritative host (not lockstep) chosen specifically to reduce this; unit-test simulation logic; deterministic where cheap |
| Scope creep during M3–M6 | High | High | This document. Reject any "just one more feature" that isn't in §3 |
| GodotSteam maturity issues | Low-Medium | Medium | Verify current version compatibility before M1; fallback to Godot's native ENet if blocked |
| Solo dev burnout on 5-month prototype | Medium | High | Weekly milestone cadence; every milestone is demoable; playtest early even if ugly |
| Slice succeeds but scaling to 6 players breaks everything | Medium | Medium | Design M1's networking with N-player in mind even if only testing 2; profile early |
| Save format needs breaking change post-slice | High | Low | Expected; schema version field from day one; migration is a solved problem |
| Fun bar not met | Medium | Terminal | Explicit go/no-go at M9. Post-mortem, not death spiral |

---

## 9. What comes after the slice

If M9 succeeds, next milestones extend the slice toward 0.1:

- Third class (candidate: Cleric — enables magic system introduction)
- Skill system expansion to 8 skills
- Bestiary expansion to 10+ monsters
- Second biome
- Reactive enemy AI tier
- Full save robustness pass
- Second procedural village
- Playtest with 4 players
- **Inventory rework — Phase B** (`docs/gdd/inventory.md`): shape-based Tetris grid (W×H footprints, rotation, weight cap tied to Strength, dual-pane Storage Chest UI, save-format migration). UX upgrade, not core-loop-validating — prioritise only if the M9 playtest shows the Phase A slot list is actively painful to use.

This is a rough sketch; the actual 0.1 milestone plan is written after slice success, informed by what we learned.

---

*Change control: this document freezes at M0 start. Any change to §3 or §4 during M0–M9 requires a written ADR and a soul-searching moment about whether the change is real or a scope crack.*
