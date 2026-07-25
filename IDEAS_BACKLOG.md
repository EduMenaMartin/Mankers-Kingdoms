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

### 2026-07-25 — [post-slice] True flow-map for water shader

Upgrade from the current tangent-scroll approximation to a proper baked flow-map texture. The current approach — per-segment tangent-direction scrolling in `water_river.gdshader` — is a valid, lighter-weight approximation of the technique used by the Waterways plugin and similar Godot river tools. The "proper" technique bakes a flow-map texture once from the path geometry, encoding scroll direction and speed per pixel, giving accurate flow speed and direction variation across the full river width rather than uniform per-segment scrolling.

Real visual upgrade, real complexity. The existing tangent data is already computed per upsample step in `WaterSystem.UpsampleSegments` and could seed a bake pass. Defer until the base water system is proven in playtesting to need it.

### 2026-07-25 — [trivial-content] Foam at river banks

Blend a foam effect into the water shader based on distance to the channel edge. Standard, cheap technique: sample a foam noise texture; attenuate by distance from the channel boundary; add to the diffuse output. `IsInRiverChannel()` (already in `TerrainSystem`) provides exactly the channel-mask data a foam distance calculation needs.

Low cost to add once the base water shader is stable. No new data — the channel mask and bank-region logic from the recent terrain-carving work (`TerrainRenderer.AddBankSurface`, `FogSystem.IsInRiverChannel`) already have the inputs.

### 2026-07-21 — [post-slice] Character-reactive grass displacement

Grass bends away from moving players and NPCs within a configurable radius, using a character-position array passed to the grass shader. Technique: a manager node tracks positions of every entity in a "character" group and uploads them as a fixed-size array of `vec4` (position.xyz + bend radius in .w) to a global shader variable — fixed array size avoids Godot shader dynamic-array limits.

This is a richer, independent feature from the simpler "basic wind-sway" item already logged (2026-07-09) — wind sway is vertex noise, this is player-position-driven displacement. Both can coexist.

**⚠️ Art-direction dependency — do NOT implement without resolving first:**
The majority of reference material and tutorials for this technique (including the most-cited "GPU grass" write-ups) assumes **toon/cel-shading** as the base art style and **orthographic camera** for the "fake perspective" billboard trick. **Neither applies to this project.** ADR-0025 locks us to a perspective camera, and no art-direction decision has been made on toon vs. PBR shading. Do not adopt toon-shading-specific techniques (accent grass colour banding, hybrid toon light-band smoothing, billboard trick) until an explicit art-direction decision is made — flag this dependency before any implementation.

### 2026-07-21 — [post-slice] Cloud shadow system (weather flag visual payoff)

VERTICAL_SLICE.md §3.8 locks "basic weather: clear or overcast (visual only, no gameplay effect)" but this currently has no real visual expression — overcast mode looks identical to clear. A world-space noise texture sampled via light-direction raymarching (stored as a global shader variable) could give "overcast" a real visual payoff: moving cloud shadows scrolling across the terrain.

Ties to the still-open day/night visual payoff item already logged, and naturally pairs with the `DayNightSystem` overcast flag.

**⚠️ Art-direction dependency — same as grass entry above:**
Cloud shadow tutorials frequently assume **toon/cel-shading** pipeline (Godot 4 `CanvasItem` or custom `WorldEnvironment` toon pass) and/or rely on a top-down **orthographic** projection to trivially unproject shadow rays. Neither applies here. Do not adopt toon-specific shadow banding or hybrid toon light-band smoothing techniques until an art-direction decision is made — flag before implementation. The core raymarching technique (noise texture + directional light angle) is style-agnostic and is safe to evaluate independently.

### 2026-07-20 — [post-slice] Godot AI MCP tool (godotengine.org/asset-library/asset/5050)

Trial completed 2026-07-20. Found genuinely solid MCP mechanics (43 tools, clean HTTP/SSE transport, scene hierarchy manipulation, node create/property/script all working) but a real near-miss risk discovered during testing: when `scene_manage/create` fails due to a wrong parameter name, all subsequent `node_create`, `node_set_property`, and `scene_save` calls silently redirect to the currently-open scene with no warning. Additionally, `script_attach` does not accept the `scene_file` guard parameter that other tools support, so there is no safety net for that step specifically. In the trial this caused `GameWorld.tscn` to receive a rogue `TestLabel` node, a script attachment, and a save — all silently, requiring a `git checkout` recovery.

Tool is not adopted. `docs/SCENE_WORKFLOW.md`'s rule stays in force unchanged: Claude Code never touches `.tscn`/`.tres` files; node trees are described for Edu to build in the editor. Revisit once the tool has more track record/maturity, and only with a written, tested, error-checked wrapper procedure in place first that: (1) confirms each step's return value before proceeding, (2) opens the target scene explicitly before any node operations, (3) runs a `git diff --stat` guard before any save call.

### 2026-07-19 — [trivial-content] Low-rest skill growth penalty (Rest < 20, per VERTICAL_SLICE.md §3.9)

VERTICAL_SLICE.md §3.9 specifies: "low rest → reduced skill growth rate." This was deliberately excluded from the M11 rest-exhaustion fix (which addressed Rest=0 consequences) because it is a separate, quieter effect: skill XP gain is halved when Rest is below 20, regardless of how long the player has been tired.

**Implementation shape:** `SkillSystem.NotifyAction` checks `NeedsSystem.Instance?.GetNeeds(peerId).rest < 20f` before awarding XP; if true, XP awarded is halved (integer floor). No new fields needed. `NeedsSystem.GetNeeds` is already public.

**Why deferred:** Touches SkillSystem in isolation with no architectural risk. Can be added as a standalone fix whenever convenient — no milestone dependency.

### 2026-07-19 — [slice-affecting] Configurable death-penalty toggles (skill loss / XP debt per VERTICAL_SLICE.md §3.4)

VERTICAL_SLICE.md §3.4 specifies three toggleable death penalties: (1) drop carried inventory at death site (recoverable), (2) lose 1 level in highest skill, (3) XP debt. "Toggleable at world creation."

**Current state:** Only (1) inventory drop is implemented (HealthSystem.KillPlayer → SpawnItemDrop). Options (2) skill loss and (3) XP debt were never built. The "toggle at world creation" UI does not exist.

**This is not a nice-to-have — it is an already-locked v1 feature with a genuine gap.** The spec text is unambiguous. Discovered during the M11 needs-system audit when the death routing was being unified through HealthSystem.KillPlayer.

**Implementation shape:**
- `shared/GameSession.cs` — three bool fields: `DropInventoryOnDeath` (default true), `SkillLossOnDeath` (default true), `XpDebtOnDeath` (default false)
- `client/CharacterCreateScreen.cs` — world creation step exposes the three toggles
- `server/HealthSystem.KillPlayer` — reads `GameSession.SkillLossOnDeath` → calls `SkillSystem.LoseLevel(peerId, highestSkill)` if true; reads `XpDebtOnDeath` → calls `SkillSystem.ApplyXpDebt(peerId)` if true; `DropInventoryOnDeath` gates the existing SpawnItemDrop call
- `SkillSystem` — `LoseLevel(peerId, skillId)`: subtracts one level's worth of XP; `ApplyXpDebt(peerId)`: adds a persistent XP debt that must be cleared before further XP accrues in that skill

**Priority:** Flag for attention before M9 playtest if possible — this affects the feel of death, which is one of the three playtest success criteria ("fun"). Not blocking M11 work, but worth scheduling before any external playtesting where death consequence expectations will be tested.

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

**Wind parameters — use Shader Globals, not per-shader uniforms:**
Expose wind direction, speed, strength, and noise texture as Godot Shader
Globals (Project Settings → Globals → Shader Globals). This lets one set
of values drive every foliage shader consistently from a single source;
any future shader (bushes, herbs, crops) can opt in without re-wiring
parameters individually.

**REQUIRED companion fix — static shadow mesh:**
Once foliage animates from wind, per-pixel shadows on that foliage will
flicker as the mesh moves. Fix: duplicate the animated mesh; set the
original to not cast shadows; set the duplicate to "Shadows Only"
(invisible, static, no wind animation applied) so shadows stay stable
while the visible mesh animates. This must ship together with wind sway —
flickering shadows would be an immediately visible defect the moment wind
sway lands, and cannot be deferred as a follow-up.

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

### 2026-07-22 — [rejected] Spatial Gardener / hand-placement foliage plugins
Considered via video research. Conflicts with the existing procedural,
seeded generation architecture (TreeGenerator/BushGenerator/
HerbGenerator) confirmed by the M-era codebase audit — same reasoning
already established for HeightMap Terrain/Waterways/Scatter earlier
this session. Not applicable regardless of art style.

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

### 2026-07-17 — [post-slice] Drag-to-place wall segments

When placing WoodenWall or WoodenGate, hold the place button and drag to auto-fill a line of wall segments rather than placing one at a time. Implementation sketch: `PlacementController` detects drag start position on LMB-down; on LMB-up, snaps a straight line of segments (capped at some max, e.g. 10) between start and end positions, each spaced by the building's footprint width; fires one `RequestPlaceBuilding` RPC per segment; deducts total wood cost in one validation step server-side. Requires `SettlementSystem` to support batched placement (or a loop of individual RPCs). The ghost-preview during drag should show the projected segment line. Only relevant for wall-type buildings (footprint ratio >> 1 in one axis).

### 2026-07-13 — [post-slice] Woodcutter NPC reforestation job
A second job type for the Woodcutter's Post: "Reforester" role plants saplings near
felled stumps to replenish the tree supply. Requires: sapling item, planting animation
placeholder, TreeSystem.PlantTree(worldX, worldZ) server method that inserts a new
seeded tree ID and starts its regrowth timer. Pairs with tree regrowth above.
Gated on: tree regrowth system.

### 2026-07-18 — [post-slice] Ranged crit threshold is coincidental to Bandit Archer's AB

The `BLOCKING_CRIT_THRESHOLD = 24` in `ProjectileSystem` was chosen to mirror the +4
melee active-block TN bonus (20 + 4 = 24). This has a specific consequence: any ranged
attacker with AB ≥ 4 (rollTotal on nat20 = 24) gets **zero** benefit from their target
blocking — crit chance stays at 5% regardless. Currently the only ranged NPC is Bandit
Archer (AB 3), which falls below the threshold and gets crit-potential fully blocked.

If a second ranged enemy type is ever added with AB ≥ 4, blocking will do nothing against
their crits, reopening the "blocking has no ranged effect" problem at a higher tier.

Revisit before adding any new ranged enemy: either raise the threshold relative to that
enemy's AB, or redesign the ranged blocking mechanic to scale with attacker AB (e.g.
`BLOCKING_CRIT_THRESHOLD = 20 + max(attackBonus, 0) + 4` computed per-shot).

