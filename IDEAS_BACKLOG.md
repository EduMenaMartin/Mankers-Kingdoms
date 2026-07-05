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

