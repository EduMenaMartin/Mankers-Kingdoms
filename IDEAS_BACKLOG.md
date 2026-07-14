# Ideas Backlog

**The pressure release valve.** Every new idea that emerges during dev goes here first. No idea is too small to write down; no idea gets scope-crept into the current milestone without triage.

## Triage tags

Every entry gets one:

- `[trivial-content]` — new item, monster, building, recipe, decorative asset. Cheap to add whenever. Not architectural.
- `[post-slice]` — real feature; add to PRD roadmap after M9.
- `[slice-affecting]` — would change the vertical slice scope. Requires ADR discussion before accepting.
- `[rejected]` — considered and declined. Keep with a "why not" note so we don't relitigate.

---

## Entries

### 2026-07-10 — [slice-affecting] Settlement prerequisite gate system — full plan

All gates use the same pattern: server rejects with `SendWarningToPeer → LocalState.ShowWarning`
→ 2.5 s flash on screen. Gates are checked at assignment time (hard block) or per-tick
(throttled warning, NPC degrades gracefully).

**Implemented (M6.5):**
- Assignment gate: shelter must exist before assigning any worker → flash "warning.assign.no_shelter"
- Haul gate: flash "warning.job.no_stockpile" every 10 s while NPC carry is full and no Stockpile Drop is reachable

**Planned — `[post-slice]` unless noted:**
- **Shelter capacity gate** `[slice-affecting]` — each Shelter has `BedCapacity = 2`; assignment blocked
  when `assignedWorkers >= totalBedCapacity`. Requires `SettlementSystem` to track assigned-worker
  count per shelter and expose `HasFreeBed()`.
- **"No trees in range" idle warning** — after a woodcutter worker idles > 30 s with no target tree
  in `MAX_CHOP_RANGE`, flash "Your woodcutter can't find trees — clear the area or move the post."
  Throttled per-NPC via `_lastWarnTime`.
- **Station removed while NPC assigned** — when a building is demolished (future demolish system),
  VillageSystem detects the node is gone, transitions NPC to Idle, warns player
  "A station was removed — {name} is now idle."
- **Herbalist's Hut Ranger-presence gate** `[M7 scope]` — locked in BuildMenu when no Ranger-archetype
  NPC is assigned to any station; tooltip shows reason. Goes dormant (not destroyed) when Ranger leaves.
  See CURRENT_MILESTONE.md M7 Phase 1.
- **Tool-tier gate** — some stations require a tool tier (e.g. Iron Axe at Logging Camp tier 2).
  Block assignment and flash "This station requires an iron axe — craft one first."
  Requires `ClassKitData.RequiredToolId` field.
- **Multi-resource deposit** — Stockpile Drop accepts any resource, not just wood. Requires
  `AddToStockpile(itemId, count)` to already be generic (it is) and `_npcCarried` to be
  `SortedDictionary<string, int>` (itemId → amount) instead of a flat int. Deferred: only wood now.

### 2026-07-10 — [slice-affecting] NPC assignment panel — settlement worker roster UI

**Slot suggestion: M8** (after M7 adds Herbalist's Hut; by M8 there are enough station types — Woodcutter's Post, Herbalist's Hut — to make the panel genuinely useful, and the current recruit→follow→E-assign flow starts feeling clunky with 6–10 workers).

**What it replaces:** The current flow of recruiting an NPC, having them follow you to a building, and pressing E to assign. That flow becomes a fallback or is removed entirely once the panel exists.

**Design:**
- Opened via a dedicated key (suggested: `N`) or via E-interact on the Kingdom Marker (alongside stockpile).
- Left column: all villagers in the settlement — name, archetype tag, current state (Idle / Assigned to X / Resting / Following).
- Right column: all placed stations — building type, assigned NPC slot (empty / name).
- Interaction: click a villager → click a station → "Assign" button → assignment RPC fires.
- "Unassign" button on any occupied station slot.
- Colour coding: idle = white, assigned = green, resting = blue, following = yellow.

**Implementation shape:**

`client/SettlementPanel.cs` — CanvasLayer Layer=27; `N` key toggle; reads `LocalState.VillagerRoster` (new) and `LocalState.StationAssignments` (new) which are pushed by the server on any change.

`shared/LocalState.cs` — two new state blobs:
- `VillagerRoster`: `IReadOnlyList<VillagerSnapshot>` (id, name, archetypeTag, state string)
- `StationAssignments`: `IReadOnlyDictionary<string, string>` (stationNodeName → villagerId or "")

`server/VillageSystem.cs` — new `BroadcastRoster()` called whenever any villager state changes (recruit, assign, unassign, sleep/wake). Serialises both blobs to JSON → `ClientUpdateRoster` RPC → `LocalState.SetRoster(...)`.

`server/VillageSystem.cs` — new `RequestAssignFromPanel(string villagerId, string stationNodeName)` RPC (AnyPeer): skips the "follower" requirement; validates villager is idle or already following; validates station exists and is unoccupied; transitions directly to Working state. Old `RequestAssignToStation` (requires follower) kept for backwards compatibility until panel ships.

`data/lang/en.json` — `"panel.settlement.title"`, `"panel.settlement.assign"`, `"panel.settlement.unassign"`, `"panel.settlement.state.idle"`, `"panel.settlement.state.assigned"`, `"panel.settlement.state.resting"`, `"panel.settlement.state.following"`.

**Prerequisite:** `VillageSystem` already tracks all state needed — `_workAssignments`, `_followTargets`, `_sleeping`, `_walkingToShelter`. Only missing piece is the broadcast channel to client and the direct-assign RPC.

**Editor task (when implementing):** Add `SettlementPanel` CanvasLayer node to `GameWorld.tscn`; register `"open_settlement"` → Key.N in `MainMenuController`.

### 2026-07-10 — [post-slice] NPC personal inventory + settlement stockpile integration

NPCs have their own personal inventory distinct from the settlement stockpile. Player can
interact directly with an NPC (E key) to view/take from their inventory. NPCs interact with
the settlement stockpile to deposit harvested goods automatically. Prerequisite: M6 settlement
stockpile foundation (already planned).

### 2026-07-15 — [post-slice] Scene-based UI for character sheet and inventory panels

Currently CharacterSheet and InventoryPanel are built entirely in C# code (no `.tscn`). This works and Claude can maintain it fully, but it prevents using Godot's visual tooling: StyleBox editors, NinePatchRect for slot frames, texture backgrounds (parchment, stone), icon sprites for equipment slots.

**When to do it:** After M9 demo gate, as part of a dedicated UI polish sprint.

**Workflow:** Claude writes `.cs` script with `[Export]`-annotated node refs; Edu creates the `.tscn` in the editor and wires up `%UniqueNameAccess` paths. Claude never touches `.tscn` per project rules.

**What it unlocks:** proper slot frame art (NinePatchRect), parchment background textures, equipment slot icons (sword/shield/armor silhouettes), drag-and-drop item assignment.

### 2026-07-02 — [post-slice] Content loading from repo-root `data/` via filesystem

Currently `data/lang/en.json` lives both at repo root (`data/lang/`) and duplicated inside the Godot project (`project/data/lang/`) as a workaround for `res://` access limits. The proper architecture loads all content from the repo-root `data/` directory using filesystem paths resolved relative to the game executable — consistent with how mods load content. Needs a content loader system. Not needed until M3+ (mod loading milestone).

### 2026-07-02 — [trivial-content] GodotSteam editor setup script

Getting GodotSteam working in editor play mode requires manually copying `steam_api64.dll` and `steam_appid.txt` into the Godot editor executable directory on each dev machine. A small `tools/setup-godotsteam-editor.ps1` script would automate this for onboarding. Low effort, useful for second dev PC.

### 2026-07-03 — [post-slice] Right-mouse orbital camera rotation

Right-click + drag to orbit the camera horizontally around the player. Implementation: add a `CameraPivot` Node3D child to `Player.tscn` (between the CharacterBody3D and Camera3D), accumulate mouse delta on `InputEventMouseMotion` while right button held, rotate pivot Y. Pitch stays fixed from editor transform. All client-side, no networking changes.

### 2026-07-03 — [post-slice] Kingdom Marker upgrade system

Kingdom Marker has a level (1–5). Each upgrade increases the territory radius by +10 units (base 40, max 90). Upgrading costs wood + stone (resources unlock at M4). Upgrade UI shown on E-interact with the marker. Visual: marker gets taller / adds decorative rings per level. Pairs naturally with the settlement progression loop — grow your territory as you gather resources.

### 2026-07-04 — [post-slice] Full settlement role hierarchy

Founder/Co-Founder/Officer/Member/Guest tiers. Fully designed in `docs/gdd/settlements.md` §2. Not scheduled. Solves v1's presence-based-guest limitation (guests today have same storage rights as trusted members; post-slice hierarchy introduces explicit Member promotion with persistent access even when founder is offline). Revisit post-M9.

### 2026-07-04 — [post-slice] Menu music

Background music track on the main menu and options screen. Loops seamlessly. Volume controlled by master volume slider (already wired in OptionsMenuController). Implementation: `client/MenuMusicPlayer.cs` — AudioStreamPlayer node added to MainMenu scene; autoplay on _Ready, stop on scene change. Separate "Music Volume" slider is a stretch goal.

### 2026-07-04 — [post-slice] Ambient audio (day/night + biome)

Layered ambient audio system: daytime (birdsong, wind) and nighttime (insects, crickets) layers crossfaded by DayNightClient at sunrise/sunset. Optional biome layers (forest density, near water). Implementation: `client/AmbientAudioSystem.cs` — two AudioStreamPlayer nodes, volume tweened on day/night change. Complements the existing day/night visual system with no new server logic.

### 2026-07-04 — [post-slice] Proximity sound effects for character actions

Spatial audio for all player and NPC actions: footsteps (surface-sensitive — dirt/wood/stone), weapon swings, tool use (axe chop, harvest), building placement, death/respawn. Uses Godot AudioStreamPlayer3D nodes parented to the acting entity for automatic distance attenuation. Server broadcasts action events; clients spawn the sound locally to avoid audio RPC overhead. Includes: swing whoosh, hit impact (meaty vs armour), tree fell crash, fire crackle (Cooking Fire, Campfire).

### 2026-07-04 — [post-slice] Toxic raw foods / poison status effect

Some foods are flagged `IsToxicRaw=true` in FoodData — eating raw inflicts poison for `PoisonDuration` seconds (HP drain over time). Cooked form is safe. Data fields already in `FoodData` and `FoodRegistry`. Implementation blocked on M4's health/damage system. When ready: `NeedsSystem` tracks `PoisonedUntil` timestamp and drains HP per tick; `LocalState` exposes poison state; `NeedsHUD` shows purple indicator. Log server-side already prints `[TOXIC]` warning as a placeholder.

### 2026-07-06 — [slice-affecting] Character creation screen before skill system

User asked whether Phase 5 (CharacterCreateScreen — roll stats, pick race/class) should be built before Phases 2–4 (skill system, inventory panel, char sheet). Argument for jumping to Phase 5 first: players can see and feel their rolled stats immediately, and the char creation UX is the one visible gap right now (`GameSession.RolledStats` is always null so server falls back to StatBlock(13,12,10,10)). Argument against: skills depend on stats so Phase 2 needs Phase 1 complete anyway (it is), but Phase 5 (the screen) is independent of Phases 2–4. Decision deferred to next session.

### 2026-07-03 — [post-slice] Ambient audio tied to day/night and monster proximity

Two ambient audio layers: daytime (birdsong) and nighttime (insects/crickets), crossfaded by DayNightClient on sunrise/sunset. A proximity check silences ambient audio when a hostile entity is within ~20 units — classic "audio tells you something is nearby" tension cue. Implementation: `client/AmbientAudioSystem.cs` — two AudioStreamPlayer nodes, volume tweened on day/night change, muted when enemy detection radius triggers. No new server-side logic needed.

### 2026-07-04 — [slice-affecting] Minimap + world map (M key)

Always-visible minimap (top-right corner, ~180×180 px) showing terrain heightmap as a grey texture, player position (white dot), nest positions (coloured skull icons by type), and Kingdom Marker territory ring. Separate full-screen world map opened with M key showing the same data at larger scale with a legend.

**Why slice-affecting:** Functionally required for the M4 demo gate — "find bandit camp from nest placement" is impractical on a 256×256 map without any map. Also enables players to actually understand the world layout in coop.

**Implementation sketch:**
- `client/MinimapHUD.cs` — CanvasLayer (Layer 30, always visible); renders a SubViewport with orthographic top-down camera OR bakes the heightmap to a texture once on world load; overlays dot/icon sprites for entities
- `client/WorldMapScreen.cs` — full-screen CanvasLayer (Layer 35); shown/hidden on M key; same data as minimap at 4× scale; B/M/Escape closes it
- Nest positions sent to clients via NestSystem RPC on connect (clients need positions to draw icons)
- Player positions from existing LocalState (own dot only; coop partner dots via existing UpdateState RPCs)
- No server logic changes — purely client-side rendering

**Recommend:** Pull into M4 as Phase 4.6. Without it the M4 demo gate cannot be demonstrated.

### 2026-07-05 — [post-slice] Mod loader implementation

Content modding foundation exists (stable string IDs per ADR-0009, content-is-data per CLAUDE.md rule 3, `FactionService.TrySetOverride` already driven by authored data). What's missing: actual `/data/mods/` directory scan, load-order resolution, base-content merge/override logic, mod manifest format (`mod.json` with id/version/dependencies), and conflict detection. Code mods (C# assembly loading or GDExtension) not yet designed. Recommend speccing during M9 or post-slice, not before.

### 2026-07-02 — [post-slice] Full Godot headless build in CI

CI currently only runs xUnit tests (pure .NET, no Godot). A headless Godot build step would catch C# compile errors in client/server scripts that reference Godot types. Requires `chickensoft-games/setup-godot` action on the runner. Worth adding in M1 once server scripts exist.

### 2026-07-09 — [slice-affecting] Third-person / first-person camera modes

Considered adding full third-person-over-shoulder and first-person aiming modes alongside top-down, toggleable by the player. Deferred — would require parallel aiming/geometry-gate systems alongside the existing mouse-aim model in docs/gdd/combat.md, its own balancing pass, and cuts against the coop-presence reasoning behind ADR-0003 (especially first-person hiding the player's own race/class model). Real occlusion and screen-space complaints that prompted this were solved more cheaply instead: angled (not literal 90°) camera, free orbit around the player, and an occlusion fade shader — see ADR-0025. Revisit third/first-person only if a specific, strong reason emerges post-slice — not a default plan.

### 2026-07-09 — [trivial-content] Visual consistency pass — unified post-process treatment
Currently mixing multiple asset sources (KayKit characters, KayKit nature
pack, placeholder primitives) with no shared visual treatment. A single
post-processing profile applied globally (not per-asset) is the highest-
value fix for making mismatched sources read as cohesive — cheaper and
more effective than upgrading individual assets. Apply once a baseline
scene exists to tune against.

### 2026-07-09 — [trivial-content] Ambient occlusion + secondary fill light
Biggest identified fix for "flat, unlit textures" complaint. Godot 4 has
built-in SSAO. Add a second DirectionalLight3D as a fill/shadow light
alongside the primary one. Cheap, high visual impact, no architecture
change.

### 2026-07-09 — [trivial-content] Color grading + subtle vignette
Ties to the locked tone in PRD.md §4.8 ("mystical, not grimdark, not
comedic, high-fantasy adventuring feel"). A warm, slightly desaturated
grade reinforces this passively, every frame, for near-zero cost.

### 2026-07-09 — [trivial-content] Foliage/grass wind sway shader
Cheap vertex-shader technique (no new geometry, no particles) — directly
targets the KayKit nature pack assets already installed. Likely the
single cheapest fix for "plain textures moving plainly" specifically.

### 2026-07-09 — [trivial-content] Water shimmer shader for the locked river
VERTICAL_SLICE.md §3.8 locks "one river" as a terrain feature. Check
whether it's currently a flat plane — if so, a simple scrolling-normal-
map water shader is a well-known, cheap Godot pattern that would make
this specific locked feature look intentional rather than placeholder.

### 2026-07-09 — [trivial-content] Selective bloom for light sources
Apply to torches, campfires (Cooking Fire), and any lit windows at night.
Makes light sources actually read as light sources rather than just
bright-colored geometry. Ties to the existing day/night cycle.

### 2026-07-09 — [post-slice] Day/night visual payoff — verify and possibly add
VERTICAL_SLICE.md §3.8 locks a day/night cycle mechanically (hunger/rest
timing) but it's unclear whether it currently has any VISUAL payoff (sky
color shift, light color temperature change). If it's purely a mechanical
timer with no visible lighting change, that's a real gap — the mechanic
exists but its most obvious payoff may be missing. Worth a quick check
before deciding whether this needs work.

### 2026-07-09 — [rejected] Depth of field post-processing
Considered as a standard polish technique (commonly recommended
alongside bloom/AO/color grading) but rejected for this project
specifically — DoF blurring the periphery works against wanting full
visibility of the settlement, teammates, and incoming threats in a
top-down coop game. Do not add this even if it comes up again as
"standard practice" — the reasoning here is project-specific, not a
general objection to the technique.

### 2026-07-09 — [trivial-content] Godot-specific shader/VFX asset resource
itch.io has curated, engine-tagged shader/VFX packs (some specifically
medieval/fantasy themed) — worth browsing there for ready-made foliage-
sway, water, and ambient-particle shaders rather than building from
scratch, same sourcing pattern already used for KayKit.

### 2026-07-10 — [trivial-content] Footstep sounds via animation keyframes
Godot's AnimationPlayer supports "Call Method" tracks — fire a footstep
sound precisely on the foot-plant frame of Walking_A/B/C and Running_A/B
(KayKit clips already in use per PlayerAnimator work), rather than a
generic timer-based approach. Natural follow-up once PlayerAnimator
exists. Needs sourced footstep sounds — freesound.org flagged as a free
starting point (see docs/research or licensing notes if a dedicated SFX
sourcing doc gets created later).

### 2026-07-10 — [trivial-content] Footstep and landing dust particles
Small particle burst on footstep/landing, distinct from the existing
combat-crit particle effects already backlogged (2026-07-09) — this is
movement-feedback, not combat-feedback. Cheap, reinforces "world reacts
to the player" alongside foliage sway.

### 2026-07-10 — [trivial-content] Player flash-on-pickup
Brief white/colored flash on the player model when collecting a
resource or item, same technique already logged for enemy hit-flash
(2026-07-09's combat.md-adjacent polish), applied to the player side of
pickups instead.

### 2026-07-10 — [trivial-content] Emissive highlight for interactable objects
Subtle glow/emissive outline on resource nodes, crafting stations, or
recruitable NPCs to make them read clearly against the environment
without needing distinct silhouettes or UI markers. Directly serves
legibility (VERTICAL_SLICE.md §2's success criteria), not just cosmetics
— worth prioritizing slightly above purely cosmetic polish items if a
picking order is ever needed.

### 2026-07-13 — [post-slice] Tree regrowth over time
Felled trees should regrow after a configurable delay (e.g. 5–10 in-game minutes).
Requires tracking regrowth timers per felled tree ID in TreeSystem, persisting them
in SaveData alongside `FelledTreeIds`, and respawning the tree node (Rpc) when the
timer expires. Regrowth timer should be saved as elapsed seconds-remaining so a
save→quit→reload cycle preserves how far along each tree's regrowth is.
Depends on: tree state save/load (done M8).

### 2026-07-13 — [post-slice] Woodcutter NPC reforestation job
A second job type for the Woodcutter's Post: "Reforester" role plants saplings near
felled stumps to replenish the tree supply. Requires: sapling item, planting animation
placeholder, TreeSystem.PlantTree(worldX, worldZ) server method that inserts a new
seeded tree ID and starts its regrowth timer. Pairs with tree regrowth above.
Gated on: tree regrowth system.

