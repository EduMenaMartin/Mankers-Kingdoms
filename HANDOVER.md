# Handover — Rolling Session Context

**Update this file at the end of every substantial session.**

---

## Current status

**Milestone:** M10 — World gen quality + river
**Last session:** 2026-07-20 — River generation pipeline, terrain carving, WaterSystem ribbon mesh + Area3D, water_river.gdshader, forest clustering in BushGenerator, NeedsSystem HP decay overhaul. 370 tests, 0 failures.
**Blockers:** None.
**Decisions needed from Edu:** None outstanding.

**Next action:**
1. **Build in Godot** — confirm game compiles clean with all M10 new files (RiverSegment, RiverData, RiverGenerator, WaterSystem) and modified files (TerrainSystem, TreeSystem, BushSystem, TreeGenerator, BushGenerator).
2. **Editor task (M10):**
   - Add `WaterSystem` node (script `res://scripts/server/WaterSystem.cs`) to `GameWorld.tscn` after `TerrainSystem`. Plain `Node`.
   - In the Remote tree at runtime, select `RiverMesh` → Inspector → ShaderMaterial → assign a `NoiseTexture2D` (FastNoiseLite, FBm, 256px, seamless=true, as_normal_map=true) to the `normal_texture` uniform. Tune `flow_speed`, `normal_strength`, `tiling`.
3. **Smoke-test river** — start new game → walk to river → confirm semi-transparent blue ribbon in carved channel; open minimap → confirm river path visible; walk into river → Output logs "[WaterSystem] Player_1 entered river".
4. **M9 pre-playtest still pending:**
   - Shelter capacity gate: `VillageSystem.RequestRecruit` rejects if no Shelter with spare capacity (max 2/shelter).
   - Godot build + smoke-test three M9 bug fixes (death drop, fog restore, NPC save) before playtest.

---

## What was done this session (2026-07-20)

### NeedsSystem HP decay overhaul — COMPLETE ✅

Three BUGS.md P1 entries (Hunger instant kill, Rest no consequence, needs death bypasses HealthSystem) all fixed in one NeedsSystem overhaul:
- **Hunger=0 → HP drain:** `HUNGER_HP_DRAIN = 2f/60f`; `_Process` calls `HealthSystem.Instance?.ApplyDamage(peerId, drain * delta)` instead of the old private `KillPlayer`. Death falls through naturally to `HealthSystem.KillPlayer` (drop + marker + respawn).
- **Rest=0 → three-phase exhaustion:** phase 0 = MoveSpeed×0.5 via `BuffSystem`; phase 1 = +60s → AttackBonus−2 + stumble pulse every 30s; phase 2 = +300s → HP drain at same rate as Hunger. `ClearExhaustionState` resets all phases and removes MoveSpeed buff the moment Rest rises above 0.
- BUGS.md entries marked FIXED (2026-07-20).

### ARCHITECTURE.md §4.4 note — COMPLETE ✅

Added one-line note to §4.4 after the Combat paragraph: MoveSpeed debuffs (exhaustion, combat stagger) are client-predicted via `LocalState.SetMoveSpeedBuff`; server tracks in `BuffSystem` but does not validate movement speed. Accepted trade-off given no PvP.

### M10 — River generation + terrain carving — COMPLETE ✅

**New shared files:**
- `shared/RiverSegment.cs` — record: GridX/Z, WorldX/Y/Z, TangentX/Z, HalfWidthM
- `shared/RiverData.cs` — `IReadOnlyList<RiverSegment> Segments`, `bool[,] ChannelMask`, `IsInChannel(gx, gz)`
- `shared/RiverGenerator.cs` — seeds: `WorldSeed ^ 0xB1A7E600u` (path), `0xB1A7E601u` (width hash). Key constants: N_SOURCE_TRIES=8, MAX_PATH_STEPS=192, MIN_RIVER_STEPS=20, WANDER_FACTOR=0.3, CHANNEL_DEPTH=3, WATER_SURFACE_OFFSET=1.5. Cosine taper carving modifies heightmap in-place.

**New server file:**
- `server/WaterSystem.cs` — ArrayMesh ribbon (UV.x = cross-river, UV.y = arc-length, Tangent = flow direction). Loads `res://shaders/water_river.gdshader` via `GD.Load<Shader>()`; flat-blue `StandardMaterial3D` fallback. `Area3D` WaterTrigger CollisionLayer=64, per-segment BoxShape3D pivots. `body_entered` stub logs server-side only.

**New shader file:**
- `project/shaders/water_river.gdshader` — `shader_type spatial`; scrolls `uv.y += TIME * flow_speed`; `NORMAL_MAP` + `NORMAL_MAP_DEPTH`; ROUGHNESS=0.05, SPECULAR=0.8.

**New doc:**
- `docs/gdd/water.md` — river path algorithm (D8 walk, width noise, monotonic smoothing), carving math, WaterSystem ribbon/collision architecture spec, complete shader spec with wiring instructions.

**Modified files:**
- `server/TerrainSystem.cs` — new pipeline: `GenerateHeightmap()` → `RiverGenerator.Generate()` in-place → `River` static property → `HeightMapShape3D`. Added `IsInRiverChannel()` helper.
- `server/TreeSystem.cs` — passes `TerrainSystem.River?.ChannelMask` to `TreeGenerator.Generate()`.
- `server/BushSystem.cs` — uses `TerrainSystem.Heightmap` (carved); re-generates tree list with same seed; passes both channel masks to `BushGenerator.Generate()`.
- `shared/TreeGenerator.cs` — `Generate(heightmap, riverMask?)` — skips channel cells when mask provided.
- `shared/BushGenerator.cs` — clustering rewrite: NEAR_TREE_CHANCE=0.70 within CLUSTER_RADIUS=3 tiles; ISOLATED_CHANCE=0.30 elsewhere; `trees` + `riverMask` optional params; maxTries=BUSH_COUNT×40.

**New test files:**
- `tests/Shared/RiverGeneratorTests.cs` — 11 tests: determinism, different seeds, bounds, min length (seeds 1–10), tangent normalisation, carving depth, channel mask, IsInChannel, monotonic WaterY, TreeGenerator exclusion integration.
- `tests/Shared/BushGeneratorTests.cs` — 6 tests: determinism without/with trees, height floor, river mask exclusion, clustering concentration (flat heightmap + synthetic trees), target count preserved.

**Modified test files:**
- `tests/Shared/TreeGeneratorTests.cs` — +2 tests: river mask exclusion, no-mask still deterministic.

**Bug found and fixed during implementation:**
- `WalkPath` terminated immediately because the source IS a border cell → path length 1 → degenerate RiverData. Fix: added `MIN_RIVER_STEPS=20` — border-exit allowed only after 20 steps traversed.

**Final test count: 370 tests, 0 failures.**

**Editor tasks required (Edu):**
- Add `WaterSystem` node to `GameWorld.tscn` after `TerrainSystem`.
- Assign `normal_texture` uniform in Inspector (NoiseTexture2D recommended).

---

## What was done this session (2026-07-18, session 2)

### Bug fixes — fog restore, NPC save completeness, death drop — COMPLETE ✅

**Death drop (root cause: compile error):**
- `server/HealthSystem.cs` — `0xHP1234u` → `0xD1CE1234u` (H and P are not valid hex digits). Godot was compiling an old binary predating the entire death-drop system. The death-drop code itself (`KillPlayer → SpawnItemDrop → ClientSpawnItemDrop + ClientShowDeathMarker`) was correct and required no further change.

**Fog of war not restored on load:**
- `server/FogSystem.cs` — `BroadcastFog()` made `public`. `_Process` only broadcasts when `UpdateVisibility()` returns `changed=true`; already-explored cells never re-trigger, so without an explicit broadcast after restore, clients kept all-UNSEEN fog forever.
- `server/SaveSystem.cs` — added `FogSystem.Instance?.BroadcastFog()` immediately after `RestoreFogFromBase64()` in `TryLoad()`.

**NPC workers/settlers gone after save→load:**
- Root cause: `GetAssignmentsForSave()` iterated `_workAssignments` only. Sleeping NPCs (in `_suspendedStation`) and idle settlement members (recruited but unassigned) were silently omitted from the save.
- Fix: Changed to iterate `_settlementNpcs` (the authoritative roster), picking the station from `_workAssignments` first, falling back to `_suspendedStation` for sleeping NPCs. Empty station = idle settler (saved and restored without assigning a job).
- `RestoreAssignmentsFromSave()` updated to handle empty `StationNodeName` gracefully (roster membership restored, no station assigned).

### Tier 1 — Equipment slot integration — COMPLETE ✅

- `client/MeleeController.cs` — `GetEquippedMeleeWeapon()` and `SetBlocking()` shield check now read `LocalState.EquippedMainHand/EquippedOffHand` first; inventory scan fallback retained for legacy saves.
- `client/BowController.cs` — `GetEquippedRangedWeapon()` and `HasMeleeWeapon()` read `LocalState.EquippedMainHand` first; inventory scan fallback.
- `server/BuffSystem.cs` + `shared/LocalState.cs` + `client/PlayerController.cs` — MoveSpeed debuff now fully client-synced: `NotifyMoveSpeedClient` helper RPC pushes multiplier + duration to the affected peer; `LocalState.SetMoveSpeedBuff(multiplier, durationMs)` uses `TickCount64` expiry (no Godot dependency); `PlayerController.ProcessLocalPlayer` applies `LocalState.MoveSpeedMultiplier` to velocity.

### Tier 2 — Code cleanup — COMPLETE ✅

- `shared/BuildingData.cs` — dead `RequiresPresence` field removed (concept removed M7; field was never read).
- `server/VillageSystem.cs` — `_elapsed`, `_sleeping`, `_lastChopTime`, `_lastWarnTime` all changed from `float`/`SortedDictionary<string,float>` to `double`/`SortedDictionary<string,double>` (prevents timer drift over long sessions per determinism policy).
- `shared/VillageGenerator.cs` — bare `Random` → `System.Random` (disambiguation; `Random` alone is ambiguous in the namespace).
- `server/ProjectileSystem.cs` — doc comment corrected: extends `Node` not `Node3D`.

### Tier 3 — SaveUtil Godot dependency removed — COMPLETE ✅

- `shared/SaveUtil.cs` — rewritten to pure C# with zero Godot imports: `SaveDir` const + two `Func<>` provider delegates (`ListSavesProvider`, `PeekSessionProvider`) + thin forwarding methods.
- `server/SaveSystem.cs` — `DoListSaves()` + `DoPeekSession()` private statics implement the `DirAccess`/`FileAccess` logic that was in SaveUtil; both wired into the delegates in `_Ready()` before the `IsServer()` guard.
- `project/tests/MankersKingdoms.Tests.csproj` — removed `<Compile Remove>` exclusion for `SaveUtil.cs` (it is now Godot-free and compiles cleanly in the test project).

### Tier 3 — Docs updated — COMPLETE ✅

- `ARCHITECTURE.md §6` — rewritten: old aspirational ECS design replaced with actual flat-dict model (two ID spaces, per-system `SortedDictionary<long,...>`, player vs NPC divergence, save model). §6.5 calls out the version-bump rule.
- `ARCHITECTURE.md §7` — stale `world.Random.Next()` reference replaced with accurate description of per-system seeded `System.Random` instances.
- `CLAUDE.md rule 6` — updated to match §7 (per-system `System.Random` seeded from `GameSession.WorldSeed ^ <constant>`; no `Random.Shared` or unseeded instances; never share one RNG across systems).
- `CLAUDE.md rule 8` — explicitly requires bump + stub migration for every schema change including additive-only; "additive, no version bump required" is not acceptable.

### ClassSelectScreen removed — COMPLETE ✅

- `project/scenes/ClassSelectScreen.tscn` and `project/scripts/client/ClassSelectScreen.cs` deleted. Superseded by `CharacterCreateScreen` in M8; was dead code.

**Commits: `a6280de` (session work), `1c6b7c0` (ClassSelectScreen delete). Both on `origin/main`.**
**Test count: 349, 0 failures.**

---

## What was done this session (2026-07-18)

### Block system redesign — §15 — COMPLETE ✅

Supersedes `combat.md` §2.5 (hard-negation gate) and §12 (simultaneous block+attack −3 AB penalty).

**Mutual exclusivity (block XOR attack):**
- `client/MeleeController.cs` — LMB branch: `if (_isBlocking) return` before `TryMeleeAttack()`
- `client/BowController.cs` — `TryFireBow()`: `if (LocalState.IsBlocking) return false` after placement guard
- `shared/LocalState.cs` — added `IsBlocking` property + `SetBlocking(bool)` so BowController can read MeleeController's state without a direct reference
- `MeleeController.SetBlocking()` calls `LocalState.SetBlocking(blocking)` to keep both in sync
- `server/CombatSystem.cs` — replaced §2.5 hard-negation gate (on the defender) with mutual-exclusivity gate (on the attacker): `if (IsBlocking(sender)) return`. **This is the authoritative enforcement** per ARCHITECTURE.md §4.4 — client guards are UX-only.
- `server/ProjectileSystem.cs` — `RequestFireProjectile`: added `if (CombatSystem.Instance?.IsBlocking(sender) == true) return` after disarm gate. Same server-authoritative pattern.

**Active block TN bonus +4 (melee):**
- `server/CombatSystem.cs` — `GetPlayerTargetNumber`: `int activeBlockBonus = (IsBlocking(peerId) && shieldBonus > 0) ? 4 : 0` added before return. Shield re-verified at hit time (`shieldBonus > 0` from ArmorRegistry — consistent with melee gate pattern).
- Orc (AB 5) vs Fighter hit-chance: **70% → 50%** while actively blocking (TN 12 → 16).
- Removed `if (IsBlocking(sender)) attackBonus -= 3` (§12 penalty — superseded).

**Ranged crit threshold (ranged):**
- `server/ProjectileSystem.cs` — `BLOCKING_CRIT_THRESHOLD = 24` constant. When target is actively blocking with shield: `isCrit = (roll == 20 && rollTotal >= 24)` instead of just `(roll == 20)`. Shield re-verified at impact (with legacy-save fallback matching all other shield checks).
- Bandit Archer (AB 3) crit chance vs blocking Fighter: **5% → 0%** (nat20 gives rollTotal 23 < 24). Threshold 24 = 20 + 4 mirrors the melee TN bonus numerically.
- Physical hit rate: unchanged (§13.1 — auto-hit on physical contact not affected by blocking).
- Intentional asymmetry: melee → reduced hit frequency; ranged → reduced crit severity.
- `IDEAS_BACKLOG.md` — `[post-slice]` entry: threshold 24 is coincidental to Bandit Archer AB 3; any future ranged enemy with AB ≥ 4 would get zero crit-blocking benefit, reopening the problem at a higher tier.

**Docs:**
- `docs/gdd/combat.md` — §2.5 and §12 marked "Superseded — see §15"; §15 added with full rule text, worked examples (TN table + crit table), and explicit asymmetry documentation in §15.3.

**Test count: 350, 0 failures** (no new tests needed — all changed logic is server plumbing with Godot dependencies; CombatResolver pure logic unchanged).

---

## What was done this session (2026-07-17)

### HP overhaul + Orc + nest tiering — COMPLETE ✅

**Fog of war improvements (carry-over from previous session):**
- `shared/FogOfWarData.cs` — added `IsDiscovered(worldX, worldZ)` helper.
- `client/MinimapHUD.cs` — full fog overlay (`_fogTex`, `BakeFogTexture`, `FogChanged` subscription); fog texture drawn between terrain and entities; nest dots hidden until discovered.
- `client/WorldMapScreen.cs` — fog data passed to `_MapDrawControl`; nest dots hidden until discovered.
- `BUGS.md` — [P1] fog bug entry marked FIXED.

**Nest tiering (VERTICAL_SLICE.md §3.6 + §3.8):**
- `shared/NestTier.cs` — new enum: `Minor = 0`, `Major = 1`.
- `shared/NestData.cs` — added `NestTier Tier = NestTier.Minor` field.
- `shared/NestGenerator.cs` — **Option B**: now produces 3 nests (was 5). Wolf (Minor), Goblin (Minor), Raider camp (Major, includes Orc). Respawn: 45s / 60s / 120s.
- `client/MinimapHUD.cs` + `client/WorldMapScreen.cs` — Major nest dots drawn larger (7 px / 10 px vs 4 px / 6 px for Minor).

**Orc (VERTICAL_SLICE.md §3.6):**
- `shared/MonsterData.cs` — added `HitDiceCount`, `HitDieSize`, `ConstitutionScore` fields.
- `shared/MonsterRegistry.cs` — `ComputeHp(hd, dieSize, con)` static helper; humanoids now use HD formula (Goblin 31.5, Bandit 60.5, BanditArcher 40.5); Orc added: 18 HD d8 Con 16 → 99 HP, AB=5, TN=14, 1d10 slashing, MoveSpeed=3, AggroRange=18, AttackRange=2.2, AttackCooldown=2.
- `data/lang/en.json` — `monster.orc.name/desc` added.

**Player HP: rolled at creation, grows with Athletics:**
- `shared/ClassKitData.cs` — added `HitDiceCount` + `HitDieSize` fields.
- `shared/ClassKitRegistry.cs` — Fighter: 4d8; Ranger: 3d8.
- `shared/SaveData.cs` — `PlayerSave.BaseHp` (default 100f, additive, no version bump).
- `server/HealthSystem.cs` — removed `PLAYER_MAX_HP` constant; added `_playerBaseHp` + `_playerAthleticsBonus` dicts; `RollPlayerHp(peerId, hd, dieSize, con)` seeded-RNG roll; `ApplyConstitution(peerId, con)` rolls HP once and calls `RecomputeMaxHp`; `OnAthleticsLevelUp(peerId, newLevel)` updates bonus; `RecomputeMaxHp` recomputes max and clamps current; `RestoreHpFromSave(peerId, hp, baseHp)` accepts new baseHp arg; `KillPlayer` uses real MaxHp not constant; `GetBaseHp(peerId)` for SaveSystem.
- `server/CombatSystem.cs` — `RequestSetStats` calls `HealthSystem.Instance?.ApplyConstitution(sender, con)`.
- `server/SkillSystem.cs` — `NotifyAction` level-up block: `if (skillId == "skill.athletics") HealthSystem.Instance?.OnAthleticsLevelUp(peerId, newLevel)`.
- `server/SaveSystem.cs` — `ps.BaseHp = HealthSystem.Instance.GetBaseHp(peerId)` in Save(); `RestoreHpFromSave(peerId, ps.Hp, ps.BaseHp)` in TryLoad.

**Test rebuild:**
- `tests/MankersKingdoms.Tests.csproj` — excluded `SaveUtil.cs` (Godot dependency violates ADR-0010); adds `<Compile Remove ...>` entry per the file's own comment rule.
- `tests/Shared/SkillRegistryTests.cs` — `OtherSkills_HaveNoToolTiers` updated to allow Foraging/Cooking tiers (added in M8 but never reflected in tests because project didn't build). Added `Foraging_HasSickleToolTierAtLevel15` + `Cooking_HasStewPotToolTierAtLevel10`.
- `tests/Shared/MonsterRegistryTests.cs` — `All_ContainsFourMonsters` → `All_ContainsFiveMonsters`; 8 new tests: Orc found, Orc HP=99, Goblin HP=31.5, Bandit HP=60.5, Orc stats (AB/TN/DamageDice), humanoids have HD data, wolf has no HD data.
- `tests/Shared/NestGeneratorTests.cs` — new file; 9 tests: 3 nests, 1 Major, 2 Minor, Major has Orc, Minor no Orc, determinism, different seeds differ, bounds, positive respawn, unique IDs.
- `tests/Shared/HpFormulaTests.cs` — new file; 14 tests: StatModifier table, monster HD formula (Goblin/Bandit/Orc), Athletics `floor(level/2)` table, career-ceiling sanity checks (Fighter Con 10/14, Ranger Con 10).
- **350 tests, 0 failures** (up from 272 with --no-build, now fully compiled + rebuilt).

---

## What was done this session (2026-07-15, session 3)

### WorldSeed randomisation — FIXED ✅

- `client/CharacterCreateScreen.cs` `OnConfirm()`: added `GameSession.WorldSeed = (uint)GD.Randi();` before `ChangeSceneToFile`. Each new game now gets a unique procedural world.
- `BUGS.md`: entry marked FIXED (2026-07-15).

### Sickle at Foraging 15 — COMPLETE ✅

`shared/SkillRegistry.cs`: Foraging `ToolTiers` changed from `Array.Empty` to `[new ToolTierData(MinLevel: 15, GrantedItemId: "item.tool.sickle")]`.  
`data/lang/en.json`: `item.tool.sickle.name/desc` added.

### Stew Pot at Cooking 10 — COMPLETE ✅

`shared/SkillRegistry.cs`: Cooking `ToolTiers` changed from `Array.Empty` to `[new ToolTierData(MinLevel: 10, GrantedItemId: "item.tool.stew_pot")]`.  
`data/lang/en.json`: `item.tool.stew_pot.name/desc` added.

### Wooden Wall + Wooden Gate — COMPLETE (code; editor tasks pending) ✅

`shared/BuildingRegistry.cs`: `WoodenWall` (5 wood, 2×3×0.4) and `WoodenGate` (10 wood, 2×3×0.4) added to `All` array.  
`data/lang/en.json`: `building.wooden_wall.name` + `building.wooden_gate.name` added.  
**Editor tasks required:**
- Create `scenes/WoodenWall.tscn` — Node3D + BoxMesh (2×3×0.4) + StaticBody3D + BoxShape3D on collision layer 8
- Create `scenes/WoodenGate.tscn` — same dimensions (interactable door is post-slice)

### Herb system + Forager movement redesign — COMPLETE (code; editor task pending) ✅

**New files:**
- `shared/HerbPatchData.cs` — record: Index, WorldX, WorldZ, WorldY
- `shared/HerbGenerator.cs` — 30 herb patches, XOR salt `0x48455242u` ("HERB"), same slope/height constraints as BushGenerator
- `server/HerbSystem.cs` — spawns purple sphere nodes (Layer 6, 32u); 60s harvest cooldown; server NPC API: `GetAvailableHerbPatchIds()`, `GetHerbPosition()`, `IsAvailable()`, `ForagerHarvestHerb()`

**Modified files:**
- `server/BushSystem.cs` — added NPC API: `GetAvailableBushIds()`, `GetBushPosition()`, `IsAvailable()`, `ForagerHarvestBush()`. ForagerHarvestBush does NOT add to player inventory (VillageSystem stockpile-deposits at carry capacity).
- `server/VillageSystem.cs`:
  - Replaced `_lastForageTime` dict + `FORAGE_COOLDOWN` + `HERB_PER_FORAGE` constants with `_foragerTarget`, `_foragerCarriedHerbs`, `_foragerCarriedBerries`, `_foragerWalkToDeposit` (all SortedDictionary) + `FORAGER_CARRY_CAPACITY=6`, `FORAGE_RANGE=1.5f`, `MAX_FORAGE_SEARCH_RANGE=200f`
  - `TickForagerJob(villagerId, delta)` — movement loop: check carry → find nearest herb/berry target → walk → harvest → when at capacity walk to stockpile
  - `TickForagerDeposit(delta)` — mirrors TickDeposit; deposits herbs to `item.herb` + berries to `item.berry` in stockpile
  - Helpers: `IsForageTargetAvailable`, `FindNearestForageTarget` (prefers herbs over berries), `GetForageTargetPosition`, `HarvestForageTarget`
  - `TickJobs`: added `_foragerWalkToDeposit.ContainsKey` skip guard; routing call updated to `TickForagerJob(villagerId, delta)`
  - `_PhysicsProcess`: added `TickForagerDeposit` call after `TickDeposit`
  - `RequestUnassignNpc` + `SuspendForRest`: both clear new forager state dicts

**Editor task required:**
- Add `HerbSystem` node (script: `res://scripts/server/HerbSystem.cs`) to `GameWorld.tscn` after `BushSystem`. Plain Node, no special properties.

---

## What was done this session (2026-07-15, session 2)

### Delete save button — COMPLETE ✅

`client/LoadGamePanel.cs`:
- Added `_deleteButton` field; Delete button in HBox between Load/Cancel
- `OnDeletePressed()` uses `DirAccess.Open(SaveUtil.SaveDir)?.Remove("{name}.json")`; calls `Refresh()`
- Button disabled when no save selected; enabled on selection and on Refresh auto-select
- `data/lang/en.json`: `load_panel.delete = "Delete"`

### ESC overlap fix — COMPLETE ✅

Root cause: PauseMenu is last child of GameWorld → gets `_UnhandledInput` first → called `Open()` + `SetInputAsHandled()` before panels saw the event.

Fix: `PauseMenu._UnhandledInput` now calls `AnyGamePanelVisible()` before opening. Checks 7 paths: WorldMapScreen, BuildingAssignmentPanel, StockpilePanel, RecruitmentDialogue, InventoryPanel, CharacterSheet, BuildMenu. If any is visible, returns WITHOUT `SetInputAsHandled()` so the event propagates to the panel's own handler.

### Fog of war — COMPLETE ✅ (one editor task pending)

**New files:**
- `shared/FogOfWarData.cs` — 64×64 `byte[,]` grid (UNSEEN=0, SEEN=1, VISIBLE=2); `UpdateVisibility(positions)`: downgrades VISIBLE→SEEN then marks tiles within 40m as VISIBLE; `ToBytes()`/`FromBytes()` for save; `VISION_RADIUS = 40f` (reuses territory radius per worldgen.md §11.3)
- `server/FogSystem.cs` — Node; updates every 1.5s from player positions; `BroadcastFog()` sends Base64 to all peers (CallLocal=true so server also gets update); `SendFogToClient(peerId)` for late-connect; `GetFogBase64()`/`RestoreFogFromBase64()` for save

**Modified files:**
- `shared/LocalState.cs` — `FogSnapshot FogOfWarData?`; `FogChanged` event; `SetFog(fog)` called by FogSystem RPC
- `shared/SaveData.cs` — `string? FogBase64` field (additive; null in old saves = all-UNSEEN; no version bump needed)
- `server/SaveSystem.cs` — `Save()`: `data.FogBase64 = FogSystem.GetFogBase64()`; `TryLoad()`: `FogSystem.RestoreFogFromBase64(data.FogBase64)`; `SendFullStateToClient`: calls `FogSystem.SendFogToClient(peerId)`
- `client/WorldMapScreen.cs` — `_fogTex ImageTexture?`; `BakeFogTexture(fog)` builds RGBA8 overlay (transparent=VISIBLE, dark60%=SEEN, black=UNSEEN); subscribes `LocalState.FogChanged`; `_MapDrawControl.FogTex` drawn after terrain and before entities; `_ExitTree` unsubscribes

**Editor task required:**
- Add `FogSystem` node (script: `res://scripts/server/FogSystem.cs`) to `GameWorld.tscn`, after VillageSystem and before SaveSystem. Plain `Node` — no special properties, Layer, or position needed.

### Localization audit — COMPLETE ✅

25 new loc keys added. All hardcoded player-facing strings eliminated from:
- `BuildingAssignmentPanel.cs` — 8 strings (title, cols, select/selected/idle/unassign/close/hint/status)
- `BuildMenu.cs` — 4 strings (title, place, marker_hint, close_hint)
- `CombatFeedbackHUD.cs` — 2 strings (block!, miss) + added `using MankersKingdoms.Shared`
- `WorldMapScreen.cs` — 9 strings (close hint, YOU, DROP, 6 legend entries)

---

## What was done this session (2026-07-15)

### ESC as game menu fallback — COMPLETE ✅

Single change: `BuildMenu._UnhandledInput` now handles `ui_cancel` before the `build_menu` check, calling `CloseMenu()` (which also restores combat mode) and marking the event handled. All other panels already handled Escape correctly. `PauseMenu` (Layer 50) continues to catch all unhandled Escape presses.

### CharacterSheet — StyleBoxFlat visual polish — COMPLETE ✅

- Palette block of `COL_*` Color constants at class top (one place to change the theme)
- Main panel: near-black warm background + 2px antique gold border + 6px rounded corners + content padding
- Title: gold colour, font size 20
- Separators: gold-tinted (`Color(0.40, 0.30, 0.12, 0.80)`)
- Section headers (Stats / Equipment / Skills): brighter gold, font size 13
- Race/class and column labels: muted warm grey
- Equipment slot buttons: dark-inset normal state, warm amber hover, amber pressed; styled via `StyleSlotButton(btn)`
- Equip picker panel: same dark background, gold border, floating above main panel
- Picker item buttons: gold hover for equip options; reddish-muted tint + red-border hover for Unequip
- Cap labels: same green/orange logic, slightly more saturated for dark-background readability
- Helper methods: `MakePanel`, `MakeFill`, `StyleSlotButton`, `StylePickerButton`, `MakeSectionHeader`, `MakeColLabel`, `MakeSeparator`

---

## What was done this session (2026-07-14, continued)

### M8 Phase 2c — Equipment slots (inventory.md §10) — COMPLETE ✅

**New files:**
- `shared/EquipSlot.cs` — enum MainHand=0, OffHand=1, BodyArmor=2

**Modified files:**
- `shared/PlayerInventory.cs` — three nullable equipped fields; `GetEquipped`/`SetEquipped`/`ClearEquippedSlotsFor`; `Clear()` resets all equipment slots
- `shared/SaveData.cs` — `PlayerSave.EquippedMainHand/OffHand/BodyArmor` (all nullable; additive change, no version bump)
- `shared/LocalState.cs` — `EquippedMainHand/OffHand/BodyArmor` properties; `GetEquipped`/`SetEquipped`; `EquippedSlotChanged` event
- `shared/CombatResolver.cs` — `PlayerTargetNumber` gains optional `armorValue`, `shieldBonus`, `armorCategory` params; ArmorCategory.Medium caps Dex mod at +1, Heavy zeroes it
- `server/InventorySystem.cs` — `RemoveItems`/`ClearItem`/`TakeAll` evict equipped slots on empty; `RestoreInventoryFromSave` with 3 optional equipped params; `SyncInventoryAndHotbarTo` now also syncs equipped; `EquipItem` server API; `RequestEquipItem` AnyPeer RPC; `SyncEquippedSlotsTo` + `ApplyEquippedSlot` Authority RPC
- `server/CombatSystem.cs` — `GetPlayerTargetNumber` reads armor+shield from equipped slots; shield validation in `RequestSetBlocking` and `RequestMeleeAttack` block gate reads equipped slot first (legacy save fallback to inventory check if null)
- `server/HealthSystem.cs` — `AutoEquipKitItems(peerId, kit)` infers slot from WeaponRegistry/ArmorRegistry; called on connect and class change
- `server/SaveSystem.cs` — saves and restores `ps.EquippedMainHand/OffHand/BodyArmor`
- `client/CharacterSheet.cs` — Equipment section between Stats and Skills (3 slot rows with buttons); persistent `_equipPicker` panel built once in `_Ready()`; `OpenPicker(slot)` lists compatible inventory items + Unequip; `OnPickerItemSelected` sends `RequestEquipItem` via node string Rpc (avoids Server import); `OnEquippedSlotChanged` refreshes button labels; Escape closes picker before closing sheet
- `data/lang/en.json` — 6 new charSheet.slot.* and charSheet.equipment keys
- `tests/Shared/InventoryTests.cs` — 6 new equipment slot tests (SetEquipped, GetEquipped, null clears, ClearEquippedSlotsFor, Clear resets, all-three independent)
- `tests/Shared/CombatResolverTests.cs` — 6 new PlayerTargetNumber tests (armor adds value, shield adds bonus, Medium caps high Dex at +1, Heavy zeroes Dex, Heavy+shield stacks, Medium doesn't raise low Dex)

**Architecture note:** `CharacterSheet` (Client) sends `RequestEquipItem` via `GetNodeOrNull<Node>(path)?.Rpc("RequestEquipItem", ...)` — string-based RPC avoids importing Server namespace from Client, same pattern as `CombatFeedbackHUD`.

**Editor task still pending (from Phase 2b):**
- Add `PauseMenu` CanvasLayer to `GameWorld.tscn`; attach `res://scripts/client/PauseMenu.cs`. Layer=50 set in code.

**Smoke test to run after editor task:**
1. Start new game (Fighter); press K → CharacterSheet opens
2. Longsword should show in Main Hand; Shield in Off Hand (auto-equipped by AutoEquipKitItems)
3. Click Main Hand button → picker opens; select a different weapon or Unequip → button label updates
4. Escape → picker closes (not sheet); Escape again → sheet closes
5. Check that armor TN affects combat (fight a goblin, compare with/without armor equipped)

---

## What was done this session (2026-07-14)

### M8 Phase 2b — Named saves + Load Game UI — COMPLETE ✅ (code only; one editor task pending)

**New files:**
- `shared/SaveUtil.cs` — read-only save helpers (`ListSaves()`, `PeekSession()`); shared namespace so client can use without Server import
- `client/LoadGamePanel.cs` — programmatic Control; save list with class/race info; sets `GameSession` fields on load; used by both MainMenu and PauseMenu
- `client/PauseMenu.cs` — CanvasLayer Layer=50; Escape toggle; Resume/Save/Load/Quit buttons; embeds LoadGamePanel; uses `GameSession.RequestSave()` (no Server dependency)

**Modified files:**
- `shared/SaveData.cs` — `SessionSave` class + `Session` field (class/race/stats preserved in save file for Load Game without re-creating character)
- `shared/GameSession.cs` — `SaveName` field; `SaveRequested` event + `RequestSave()` bridge
- `server/SaveSystem.cs` — `SavePath` property (`user://saves/{SaveName}.json`); `EnsureSaveDir()`; saves/restores `data.Session`; subscribes `SaveRequested`
- `client/CharacterCreateScreen.cs` — stamps `GameSession.SaveName` as `"save_{yyyyMMdd_HHmmss}"` on confirm
- `client/MainMenuController.cs` — injects "Load Game" button after StartSolo; adds `LoadGamePanel` overlay
- `data/lang/en.json` — `menu.load_game`, `pause.*`, `load_panel.*` loc keys

**Architecture note:** PauseMenu (client) triggers saves via `GameSession.SaveRequested` event → SaveSystem subscribes. Avoids `using MankersKingdoms.Server` in client code.

**Pending editor task:**
- Add `PauseMenu` CanvasLayer node to `GameWorld.tscn` (any position; Layer=50 is set in code); attach script `res://scripts/client/PauseMenu.cs`

**Next smoke test:**
1. Start new game → play (place building, chop trees, gain XP)
2. Press Escape → "Save Game" button → close pause menu
3. Escape → "Quit to Main Menu"
4. Main menu shows "Load Game" button → click → select save slot → click Load
5. Resumes in same world with same buildings, skills, stockpile

---

## What was done this session (2026-07-13)

### M8 Phase 1 — Save/load core — COMPLETE ✅ (code only; editor task pending)

**Three design decisions locked:**
1. **Serialization order:** world seed → markers → buildings → stockpile → NPC assignments → per-peer (inventory, skills, HP, needs, position)
2. **Fog of war:** per-tile `byte[64,64]` (0=unseen, 1=seen, 2=visible); deferred to Phase 3
3. **Client reconnect:** RPC replay (`SendFullStateToClient`) — no shared save file read; reuses existing sync infrastructure

**New files:**
- `shared/SaveData.cs` — v1 JSON schema: `SaveData`, `MarkerSave`, `BuildingSave`, `NpcAssignSave`, `PlayerSave`; version field from day 1
- `server/SaveSystem.cs` — Node; `Save()` / `TryLoad()` / 5-min autosave / exit save; deferred load (one frame after `OnPlayerConnected`) so system defaults initialize before overwrite; `OnLatePlayerConnected` → `SendFullStateToClient` for reconnect replay

**Modified files:**
- `server/SettlementSystem.cs` — `_placedBuildings List<(string,Vector3)>` populated in `SpawnBuilding` (server-only branch); `GetSaveState()` returns (markers, buildings, stockpile); `RestoreFromSave()` calls `Rpc(SpawnMarker/SpawnBuilding)` so nodes appear on all peers
- `server/SkillSystem.cs` — `GetXpForSave()`, `GetBumpsForSave()`, `RestoreSkillsFromSave()`, `BroadcastLevelsTo()` (public wrapper for reconnect)
- `server/HealthSystem.cs` — `GetPlayerIds()`, `RestoreHpFromSave()` (clamps to ≥1), `SyncHealthTo()`
- `server/NeedsSystem.cs` — `GetNeeds()`, `RestoreNeedsFromSave()` (clamped 1–100 hunger, 0–100 rest)
- `server/InventorySystem.cs` — `RestoreInventoryFromSave()`, `SyncInventoryAndHotbarTo()`
- `server/VillageSystem.cs` — `GetAssignmentsForSave()`, `RestoreAssignmentsFromSave()`, `BroadcastRosterToAll()`
- `TODO.md` — M8 Phase 1 tasks checked off; Phase 2 (editor) + Phase 3 (fog of war, loc, demo gate) added

**Tests:** 257, 0 failures (unchanged — all save/load code is server plumbing with no unit-testable pure logic)

**Editor task (next):**
1. Open `GameWorld.tscn`
2. Add a `Node` as the **last child** of the root (after VillageSystem)
3. Rename it `SaveSystem`
4. Attach script: `res://scripts/server/SaveSystem.cs`
5. Save scene — no Inspector settings needed

**What's blocked:**
- Smoke test can't run until editor task is done (SaveSystem node must be in the scene tree)

---

## What was done this session (2026-07-10, third entry)

### M7 Phases 1–3 — Herbalist's Hut + forager loop + bandage — COMPLETE ✅

**Design pivot (approved this session):** Ranger presence gate removed. Assignment is now shelter-based: any recruited NPC can be assigned to any workable station from a single Building Assignment Panel. Ranger NPC still needed to recruit (follows player to camp), but the *gate* is shelter presence in the settlement, not class/archetype.

**Phase 1 — Herbalist's Hut (no gate)**
- `shared/BuildingRegistry.cs` — `HerbalistsHut` added (`building.herbalists_hut`, 20 wood, 4×3×4 footprint); `RequiresPresence` field removed entirely from all entries
- `server/SettlementSystem.cs` — `RequestCraftBandage` RPC (no dormancy check; uses `BANDAGE_HERB_COST=2`; grants `item.bandage` 1; calls `SkillSystem.Instance?.NotifyAction(sender, "skill.foraging")`)
- `client/BuildMenu.cs` — gate label/button system removed; panel simplified back to -175/+175

**Phase 2 — Forager NPC job loop**
- `server/VillageSystem.cs` — `_settlementNpcs SortedSet<string>` (recruits added, not removed on Leave); `_npcFounder SortedDictionary<string,long>`; `_lastForageTime SortedDictionary<string,float>`; `TickForagerJob` (30s cooldown, `item.herb` → stockpile); `TickJobs` routes: `building.herbalists_hut` prefix → `TickForagerJob`, else woodcutter loop; `RequestAssignNpcToStation` checks `_settlementNpcs` (no proximity/follower requirement); `RequestUnassignNpc`; `BroadcastVillageRoster` + `ClientSetVillageRoster` RPC; `EscapeJson` helper
- `shared/LocalState.cs` — removed `RangerNpcPresent`/`RangerPresent`/`RangerPresentChanged`/`SetRangerNpcPresent`; added `VillageRosterJson` + `VillageRosterChanged` event + `SetVillageRoster`

**Phase 3 — Bandage crafting + use**
- `server/HealthSystem.cs` — removed `_playerClasses`/`GetPlayerClass`/class tracking; added `RequestUseBandage` RPC (uses `item.bandage`; heal formula: `20 + (foraging/5)*1f`, cap 40)
- `client/PlayerController.cs` — Tab (`EatFood`) checks active hotbar slot for `item.bandage` → `RequestUseBandage`; `TryCraftBandage()` added; E at Herbalist's Hut → craft bandage; E at Shelter (founder) → open BuildingAssignmentPanel (was: E at station with follower → assign)
- `data/lang/en.json` — `item.herb.*`, `item.bandage.*` added; `warning.build.no_ranger`, `warning.craft.no_ranger`, `build.gate.no_ranger` removed

**BuildingAssignmentPanel (new)**
- `client/BuildingAssignmentPanel.cs` — CanvasLayer Layer=31; Escape closes; two-column layout (left: NPC list with Select/Unassign buttons; right: station list with current occupant); `RefreshAssignButton` enabled when both NPC + station selected; `OnAssignPressed` → `RequestAssignNpcToStation`; `SendUnassign` → `RequestUnassignNpc`; subscribes to `LocalState.VillageRosterChanged`
- `WORKABLE_PREFIXES` = `["building.woodcutters_post", "building.herbalists_hut"]`

**Tests**
- `ForagerSystemTests.cs` — removed `HerbalistsHut_RequiresRangerPresence` (gate no longer exists); renamed `AllOtherBuildings_HaveNoPresenceRequirement` → `AllBuildings_HaveNoPresenceRequirement` (now asserts ALL buildings have null RequiresPresence)
- `BandageCraftingTests.cs` — 15 tests; heal formula uses integer division `(foraging / 5)` — level 4 → 20f, level 5 → 21f, level 100 → 40f cap
- **255 tests, 0 failures**

**Editor tasks needed (Edu)**
- Add `BuildingAssignmentPanel` CanvasLayer to `GameWorld.tscn`; attach `res://scripts/client/BuildingAssignmentPanel.cs`

**Bug (logged, not fixed)**
- BUGS.md: hotbar slot not cleared when item is fully consumed via Tab (e.g. last bandage used; slot still shows "Bandage")

---

## What was done this session (2026-07-10, second entry)

### M7.5a — Hotbar — COMPLETE ✅

**Rationale:** Pulled forward from M9 to before M7 because bandages (M7) need a quick-access slot to be usable in combat without opening the inventory.

**New files:**
- `client/HotbarHUD.cs` — CanvasLayer Layer=5; always-visible 9-slot bar anchored bottom-centre; yellow highlight on active slot; displays assigned item name. Number keys 1–9 → `LocalState.SetActiveHotbarSlot` + `LocalState.NotifyHotbarKeyPressed`.

**Modified files:**
- `shared/PlayerInventory.cs` — `string?[] _hotbarSlots[9]`; `SetHotbarSlot(int, string?)` with move semantics (item can only be in one slot); `GetHotbarSlot(int)`; `IReadOnlyList<string?> HotbarSlots`
- `shared/LocalState.cs` — `ActiveHotbarSlot`, `HotbarSlotChanged` event, `ActiveHotbarSlotChanged` event, `HotbarKeyPressed` event; `SetHotbarSlot`, `SetActiveHotbarSlot`, `GetHotbarSlot`, `NotifyHotbarKeyPressed`
- `server/InventorySystem.cs` — `RequestAssignHotbar(int slot, string itemId)` AnyPeer RPC (validates item in inventory); `ApplyHotbarSlot` Authority RPC; `SyncHotbarTo` (sends all 9 slots)
- `client/InventoryPanel.cs` — hover detection via `GetGlobalRect().HasPoint(mousePos)` (NOT MouseEntered events — those don't re-fire after row rebuild); `_rowItems Dictionary<HBoxContainer,string>` rebuilt on each `Refresh()`; `RefreshBadges()` lightweight path (updates `[N]` badge labels without rebuilding rows); `OnHotbarKeyPressed` assigns hovered item; `OnHotbarSlotChanged` calls `RefreshBadges()` not full `Refresh()`; hotbar hint added to footer
- `data/lang/en.json` — `inventory.hotbar_hint`

**Bug fixed during session:**
- First overwrite worked but second press of same slot didn't overwrite. Root cause: `OnHotbarSlotChanged` called `Refresh()` which rebuilt all rows; rebuilt rows don't auto-fire `MouseEntered`, so `_hoveredItemId` went null. Fix: replaced `MouseEntered`/`MouseExited` hover tracking with rect-based `GetHoveredItemId()` queried at key-press time; changed `OnHotbarSlotChanged` to call lightweight `RefreshBadges()` to preserve row identity.

**Editor task remaining:**
- Add `HotbarHUD` CanvasLayer to `GameWorld.tscn`; attach `res://scripts/client/HotbarHUD.cs`

**230 tests, 0 failures (unchanged — all changes are UI layer)**

### M7 design decisions — LOCKED

All four design questions from CURRENT_MILESTONE.md resolved this session:
1. **Both** player Ranger AND Ranger-archetype NPC count for presence gate
2. **Dormant** (not destroyed/locked) when Ranger leaves
3. **`item.herb`** — spawns randomly on map; gatherer NPC collects and brings back; consumed 2→1 bandage
4. **Heal amount:** 20 HP base + Foraging skill bonus, cap at +20 max (so range 20–40 HP)

---

## What was done this session (2026-07-10)

### M6 — Village and Recruitment — COMPLETE ✅

All four phases implemented and demo gate passed.

**Phase 1 — Village generation**
- `VillagerData`, `VillageData`, `VillageGenerator` (seeded, best-of-3 3d6 per stat, archetype from highest stat), `data/base/villages/names.json` (30 names)
- `VillageSystem` (server Node, spawns VillagerNode instances from seeded positions)
- `client/VillagerNode.cs` (teal capsule, Layer 256u, Label3D, `SetTarget` via `.Call()`)
- 16 new tests in `VillageGeneratorTests.cs`

**Phase 2 — Recruitment dialogue + follow state**
- `RecruitmentDialogue` (CanvasLayer 28, stat display with gold ★ on highest, Recruit/Leave, FollowerChanged live update)
- `LocalState`: `FollowerNpcId`, `SetFollower`, `ClearFollower`, `FollowerChanged`
- `VillageSystem`: `RequestRecruit` / `RequestLeave` RPCs, follow tick (3 m/s, stop 2 m), `ClientMoveVillager` broadcast (every 3 physics ticks)

**Phase 3 — Woodcutter's Post + stockpile + NPC job loop**
- `BuildingRegistry`: `WoodcuttersPost` (id `building.woodcutters_post`, 15 wood)
- `SettlementSystem`: `_stockpile`, `AddToStockpile`, `RequestTakeFromStockpile`, JSON broadcast, `LocalState.SetMarkerWorldPos`
- `StockpilePanel` (CanvasLayer 29, E near Kingdom Marker, live list, Take All)
- `TreeSystem`: `GetAvailableTreeIds()`, `ServerChopTree()`, `TreeSystem.Instance` static property (was missing — added as post-gate bug fix)
- `VillageSystem`: `RequestAssignToStation` RPC, Following→Working transition, job tick with 20 m tree search + 1 s chop cooldown via elapsed-time comparison

**Phase 4 — NPC needs**
- `VillageSystem`: `_npcHunger`/`_npcRest` dicts (100 at spawn); `TickNeeds` (1 s interval, drains rest 1/min, restores hunger 0.5/s, triggers `SuspendForRest` at < 20); `TickResting` (walk→arrive→sleep 30 s→wake→Idle); `SuspendForRest` (strips Follow/Work, sends `ClientClearFollower`, walks to nearest Shelter); `TickFollow`/`TickJobs` skip sleeping/walking NPCs; `RequestRecruit` rejects sleeping/walking NPCs

**Bug fix (post-gate)**
- `TreeSystem.Instance` static property missing — VillageSystem referenced it in `TickJobs`; added `public static TreeSystem Instance { get; private set; }` + assignment in `_Ready()`

**Docs closed out**
- `TODO.md` — Phase 4 ✅, demo gate ✅, M6 COMPLETE, M7 stub added
- `CHANGELOG.md` — M6 entry written (full Added + Tests)
- `CURRENT_MILESTONE.md` → M7
- `HANDOVER.md` → this entry

**230 tests, 0 failures**

### Editor tasks completed by Edu (before demo gate)
- `scenes/VillagerNode.tscn` — CharacterBody3D + CapsuleMesh + CapsuleShape3D + Label3D; script `client/VillagerNode.cs`
- `GameWorld.tscn` — VillageSystem node added (after TreeSystem); RecruitmentDialogue CanvasLayer added; StockpilePanel CanvasLayer added
- `scenes/WoodcuttersPost.tscn` — StaticBody3D + MeshInstance3D; built and placeble in build menu

---

## What was done this session (2026-07-09)

### M5 demo gate — PASSED ✅
- Woodcutting XP growth confirmed in Output; K key (CharacterSheet) shows skill level in real time.
- Ranger created, 75 trees chopped, Woodcutting reached level 15, bronze axe granted, stat ceiling confirmed.

### Animation system fixes
- `client/PlayerAnimator.cs` — node paths corrected: `Knight/AnimationPlayer` → `CharacterRig/AnimationPlayer`; same for KnightMeshes/RangerMeshes paths. Root cause: the scene node was named `CharacterRig` not `Knight`; `GetNode` threw on every _Ready, silently killing all animation.
- `client/PlayerAnimator.cs` — `FaceMouseCursor()` added: per-frame horizontal-plane raycast from camera through mouse; rotates `CharacterRig` Y axis to face cursor. Model faces +Z at Y=0 → uses `Atan2(dir.X, dir.Z)`.
- **Editor (Edu):** Ranger mesh nodes in Player.tscn were missing `skeleton = NodePath("../..")` — all `MeshInstance3D` children under `RangerMeshes` now have skeleton set; ranger animations work.

### Camera and movement improvements (ADR-0025 follow-up)
- `client/PlayerController.cs` — `GetCameraRelativeInput(Vector2)`: flattens camera -Z/+X axes to XZ plane, applies WASD vector relative to camera yaw. World-space direction sent to server unchanged (same Vector2 field, same server code). C key `toggle_combat` handler removed.
- `client/CameraController.cs` — scroll-wheel zoom: `WheelUp` decrements `_followDistance` by 2 (min 5), `WheelDown` increments (max 30); default 14 unchanged. `FOLLOW_DISTANCE` constant replaced with `_followDistance` field.

### Build mode redesign
- **Old:** C toggles between build mode (default) and combat mode. B blocked in combat mode.
- **New:** Combat mode is always the default (`InCombatMode = true`). B opens the build menu and enters build mode; closing the menu (B again / Close button) or completing/cancelling placement restores combat mode automatically. C key removed.
- `shared/LocalState.cs` — `InCombatMode` default `false` → `true`; `SetCombatMode(bool)` added (fires event only on actual change); `ToggleCombatMode()` kept as legacy wrapper.
- `client/BuildMenu.cs` — combat warning + `CombatModeChanged` subscription removed; B press sets `SetCombatMode(false)` + shows menu; close sets `SetCombatMode(true)`.
- `client/PlacementController.cs` — `CombatModeChanged` subscription + `OnCombatModeChanged` removed; `CancelPlacement()` now calls `SetCombatMode(true)`.
- `client/MainMenuController.cs` — `toggle_combat` → C key registration removed.
- `client/PlayerController.cs` — `toggle_combat` action handler removed.

### M5 formally closed
- CURRENT_MILESTONE.md updated to M6.
- TODO.md M5 items marked complete; post-gate fixes logged; M6 stub added.
- HANDOVER.md updated (this entry).
- BUGS.md — two new FIXED entries: PlayerAnimator path bug, Ranger skeleton bug.
- CHANGELOG.md — M5 formally closed with full Added/Fixed/Tests record.
- **214 tests, 0 failures** (no new tests this session — all changes were client-side presentation or mode-switching logic).

### Docs and planning (end of session)
- `docs/gdd/worldgen.md` — created; §11 (fog of war early probe for M9) written and locked. §1–10 pending authoring.
- `VERTICAL_SLICE.md` M8 — fog of war bullet added to scope list.
- `IDEAS_BACKLOG.md` — 9 new entries added (2026-07-09): 6 `[trivial-content]` visual polish items (post-process, AO, colour grade, foliage shader, water shader, bloom), 1 `[post-slice]` day/night visual payoff check, 1 `[rejected]` depth-of-field, 1 `[trivial-content]` itch.io shader sourcing note.

---

## What was done this session (2026-07-08)

### GDD §13 — Ranged resolution asymmetry ✅ (locked + implemented)

- `docs/gdd/combat.md` — §13 appended (locked): physical contact = automatic hit for ranged; d20+AB roll determines **normal vs. crit only**, never gates hit/miss. Fumble table suppressed for ranged on nat-1 (§13.3). Monster ranged uses same model (§13.5 Q3 pending confirmation). Wide-margin crit threshold deferred to balancing pass (§13.5 Q1).
- `shared/CombatResolver.cs` — `PlayerAttackBonus(weaponId, str, dex, skillLevel)` — gains `skillLevel` param; formula now `skillLevel/10 + StatModifier(stat)` (was stat-only placeholder).
- `server/SkillSystem.cs` — `GetSkillLevel(long peerId, string skillId)` public method added; reuses private `ComputeLevel`.
- `server/CombatSystem.cs` — melee AB now `GetSkillLevel(sender, "skill.melee")` before `PlayerAttackBonus`. Melee hit/miss/crit logic untouched.
- `server/ProjectileSystem.cs` — ranged hit block rewritten: inline `1d20` roll; `isCrit = (roll == 20)`; `rollTotal` kept for future wide-margin threshold; double dice on crit (§5.4); `System.Math.Max(1, dmg)` floor; `isCrit` propagated to `ShowCombatResult` (was hardcoded `false`). No hit/miss gate — nat-1 still deals damage.
- `tests/Shared/CombatResolverTests.cs` — 3 existing `PlayerAttackBonus` tests updated for new signature; 1 new test (`PlayerAttackBonus_SkillLevel_ContributesFloorDiv10`). **214 tests, 0 failures.**

### PlayerAnimator — KayKit Adventurers animation system ✅

- `shared/LocalState.cs` — 4 new events + updated `SetHealth`:
  - `DamageTaken` — HP decreased while alive
  - `PlayerDied` — HP hit 0
  - `PlayerRevived` — HP rose from 0 (respawn)
  - `LocalArrowFired` + `NotifyLocalArrowFired()` — client-side ranged fire signal
- `client/BowController.cs` — `LocalState.NotifyLocalArrowFired()` called after RPC dispatch.
- `client/PlayerController.cs` — `AddChild(new PlayerAnimator())` in local-player `_Ready()` block.
- `client/PlayerAnimator.cs` — new file; state machine (priority: Dead > HitStun > Throw > Jumping > Running > Walking > Idle):
  - `Idle_A` when `Velocity.XZ < 0.15 m/s`
  - `Walking_A/B/C` (cycles on each Walking entry) at normal speed (5 m/s)
  - `Running_A/B` above 7 m/s (future-proof; no sprint yet)
  - `Jump_Start → Queue(Jump_Idle) → Jump_Land` on `IsOnFloor()` edge detection
  - `Hit_A/B` (alternating) on `LocalState.DamageTaken`; returns to movement on `AnimationFinished`
  - `Death_A/B` (alternating) on `LocalState.PlayerDied`; blocks all transitions until `PlayerRevived`
  - `Throw` on `LocalState.LocalArrowFired` (ranged placeholder — no dedicated Shoot/DrawBow clip in pack)
  - `ApplyCharacterMesh()` in `_Ready()`: shows `KnightMeshes` or `RangerMeshes` based on `GameSession.ChosenClassId`; uses `GetNodeOrNull` so it's safe before editor setup
- Animation clips use library-prefixed names: `"Rig_Medium_General/Idle_A"`, `"Rig_Medium_MovementBasic/Walking_A"`, etc.
- Note: confirm `Death_A/B` loop mode = `None` in AnimationPlayer so they hold the final pose.

### Player.tscn editor tasks ✅ (completed by Edu)
- Deleted old `MeshInstance3D` capsule (direct child of Player)
- Wrapped 9 Knight mesh instances into `KnightMeshes` Node3D under `Skeleton3D`
- Added Ranger asset as `RangerMeshes` Node3D (Visible=false) under `Skeleton3D`

---

## What was done this session (2026-07-06 — M5 Phase 4)

### M5 Phase 4 — Character sheet UI ✅ (code complete; editor task pending)

- `client/CharacterSheet.cs` — new CanvasLayer (Layer 26); **K** toggles, **Escape** closes; rebuilds the entire UI tree on each open (so race/class changes during debugging are always fresh); panel 460×500; sections:
  - Race/class line: resolved via `RaceRegistry.Find` + `ClassKitRegistry.Find` → `Loc.T(displayNameKey)`
  - Stats: two lines (Str/Dex, Con/Wis) showing effective values after race modifiers; shows "—" if `GameSession.RolledStats` is null (no char creation done)
  - Skills table: Skill | Level | Cap for all 6 skills; Cap derived from `SkillData.GetCap(effectiveStats)`; cap cell turns orange when `level >= cap` (ceiling reached), green otherwise
- Data sources: all client-side — `GameSession` for race/class/rolled stats, `LocalState.SkillLevels` for current levels, `SkillRegistry` for caps. No new RPCs needed.
- Subscribes to `LocalState.SkillLevelChanged` to refresh skill rows in-place while open.
- `client/MainMenuController.cs` — registered `"char_sheet"` → `Key.K`
- `data/lang/en.json` — 9 new charSheet.* keys
- **213 tests, 0 failures**

**Editor task — add CharacterSheet node to GameWorld.tscn:**
1. Open `GameWorld.tscn`
2. Add a `CanvasLayer` as a child of the root
3. Rename it exactly `CharacterSheet`
4. Attach script: `res://scripts/client/CharacterSheet.cs`
5. Save the scene (no Inspector settings — Layer and Visible are set in `_Ready()`)

---

## What was done this session (2026-07-06 — M5 Phase 3)

### M5 Phase 3 — Inventory UI panel ✅ (code complete; editor task pending)

- `shared/LocalState.cs` — added `InventoryChanged` event; `SetInventory` now fires it after replacing the snapshot
- `client/InventoryPanel.cs` — new CanvasLayer (Layer 25); I key toggles open/closed; Escape closes when open; centred modal panel (380×480) with semi-transparent backdrop, title, scrollable item list, footer hint; each row shows `ItemName × count`; item names resolved via `Loc.T(itemId + ".name")`; refreshes on `LocalState.InventoryChanged` (only when visible)
- `client/MainMenuController.cs` — registered `"open_inventory"` → `Key.I`
- `data/lang/en.json` — added `inventory.title/empty/close_hint`; added `resource.wood.name` (was missing — `.name` suffix pattern now consistent for all item IDs)
- **213 tests, 0 failures** (no new tests; InventoryPanel is pure UI, no testable logic beyond what LocalState already covers)

**Editor task — add InventoryPanel node to GameWorld.tscn:**
1. Open `GameWorld.tscn` in the Godot editor
2. Add a `CanvasLayer` as a child of the root
3. Rename it exactly `InventoryPanel`
4. Attach script: `res://scripts/client/InventoryPanel.cs`
5. Save the scene (no properties to set — Layer and Visible are set in `_Ready()`)

---

## What was done this session (2026-07-06 — M5 Phase 2)

### M5 Phase 2 — Skill system ✅ (code complete; SkillSystem node editor task pending)

- `shared/ToolTierData.cs` — record: MinLevel, GrantedItemId
- `shared/SkillData.cs` — record: Id, DisplayNameKey, GoverningStats[], XpPerAction, XpPerLevel, ToolTiers[]; `GetCap(StatBlock)` returns best cap across governing stats (Athletics uses max of Str/Con)
- `shared/SkillRegistry.cs` — 6 hardcoded skills: skill.melee (Str), skill.ranged (Dex), skill.athletics (max(Str,Con)), skill.woodcutting (Str), skill.foraging (Wis), skill.cooking (Wis); XpPerLevel=5, XpPerAction=1; Woodcutting tier: level 15 → item.tool.bronze_axe
- `shared/LocalState.cs` — `SkillLevels` read-only dict, `SetSkillLevel`, `SkillLevelChanged` event
- `server/SkillSystem.cs` — new Node; per-peer SortedDictionary<skillId, xp> + bump; `NotifyAction` awards XP, levels up, grants tool tiers, sends level RPC; `ApplyBump` clears + resets class bumps on class re-selection; `BroadcastAllLevels` on connect; `ClientApplySkillLevel` RPC → LocalState
- Level formula: effectiveLevel = min(statCap, rawXp/XpPerLevel + classBump). Demo gate: 75 chops = 75 XP / 5 = level 15.
- Wired triggers:
  - `server/CombatSystem.cs` — melee hit → `SkillSystem.Instance?.NotifyAction(sender, "skill.melee")`
  - `server/ProjectileSystem.cs` — player ranged hit → `NotifyAction(proj.OriginPeerId, "skill.ranged")` (monster shots excluded by `< MONSTER_ID_THRESHOLD` guard)
  - `server/BushSystem.cs` — harvest → skill.foraging; cook → skill.cooking
  - `server/TreeSystem.cs` — FellTree: placeholder `_playerXp` dict removed; replaced with `SkillSystem.Instance?.NotifyAction(byPeer, "skill.woodcutting")`
- `server/HealthSystem.cs` — `SkillSystem.Instance?.ApplyBump(sender, kit.SkillBumps)` called in RequestSetClass after kit distribution
- `data/lang/en.json` — added 6 skill name keys + item.tool.bronze_axe name/desc
- `tests/Shared/SkillRegistryTests.cs` — 10 tests: catalog count, Find roundtrip, GetCap per skill, Athletics max(Str,Con) verified, XP demo gate (75 chops → level 15), Woodcutting bronze axe tier
- **213 tests, 0 failures** (up from 198)

**Editor task — add SkillSystem node to GameWorld.tscn:**
1. Open `GameWorld.tscn` in the Godot editor
2. Add a `Node` as a child of the root (after InventorySystem and CombatSystem, since SkillSystem depends on both)
3. Rename it exactly `SkillSystem`
4. Attach script: `res://scripts/server/SkillSystem.cs`
5. Save the scene

---

## What was done this session (2026-07-06 — M5 Phase 5)

### M5 Phase 5 — Character creation screen (code complete; editor task pending)

- Phase order decision: B (Phase 5 first, then Phases 2→3→4)
- `client/CharacterCreateScreen.cs` — new file; 3d6 roll per stat on load; race picker (4 races, tooltip = desc); Human choice row (hidden for non-Human, Confirm disabled until choice made); class picker; Reroll clears human choice; Confirm writes all four GameSession fields → GameWorld.tscn. Stat labels show "Label: raw → effective" when race changes the value.
- `client/MainMenuController.cs` — all three paths (Solo/Host/Join) now route to `CharacterCreateScreen.tscn` (was `ClassSelectScreen.tscn`)
- `data/lang/en.json` — no changes needed; all charCreate.* and race.* keys were already present from Phase 1
- **Editor task pending:** `scenes/CharacterCreateScreen.tscn` (see "What's next" below)
- **196 tests, 0 failures** (no new tests needed — all logic is in existing tested types: StatBlock, RaceData, RaceRegistry, GameSession)

## What was done this session (2026-07-06 — M5 Phase 1)

### M5 Phase 1 — Stat foundation + race system ✅

- `shared/StatBlock.cs` — record: Str/Dex/Con/Wis; `SkillCap(stat)` = floor(99×stat/18) (ADR-0019); `Clamped()` enforces 3–18
- `shared/RaceData.cs` — immutable record with stat modifier dict; `Apply(rolled, chosenStat?)` applies mods and clamps
- `shared/RaceRegistry.cs` — 4 races hardcoded: Human (+1 choice), Dwarf (Con+1/Wis−1), Elf (Dex+1/Con−1), Halfling (Dex+1/Str−1)
- `shared/GameSession.cs` — added `ChosenRaceId`, `RolledStats (StatBlock?)`, `HumanChosenStat`; Reset() clears all
- `shared/ClassKitData.cs` — removed `Str`/`Dex` (stats now player-rolled); added `ClassSkillBump[]` and `SkillBumps`
- `shared/ClassKitRegistry.cs` — Fighter: Melee+5/Athletics+3; Ranger: Ranged+5/Foraging+3 (skill bumps replace stat fields)
- `server/CombatSystem.cs` — `_playerStats` upgraded from `(int str, int dex)` tuple to `StatBlock`; `SetPlayerStats(long, StatBlock)` updated; `GetPlayerStats` now returns `StatBlock`; `RequestSetStats(int,int,int,int)` RPC added; default fallback `StatBlock(13,12,10,10)`
- `server/HealthSystem.cs` — removed all `kit.Str`/`kit.Dex` references; comment notes stats arrive via `CombatSystem.RequestSetStats`
- `server/ProjectileSystem.cs` — `GetPlayerStats` destructure updated from tuple to `StatBlock` (`.Str`/`.Dex`)
- `client/PlayerController.cs` — `AnnounceStats()` added alongside `AnnounceClass()`, both deferred in `_Ready()`; if `RolledStats == null` (no char creation yet), skips silently
- `data/lang/en.json` — race loc keys (human/dwarf/elf/halfling .name/.desc) + charCreate keys
- `tests/Shared/StatBlockTests.cs` — 7 tests: SkillCap table (stat 3/10/16/18), Clamped behaviour
- `tests/Shared/RaceRegistryTests.cs` — 19 tests: catalog completeness, per-race stat deltas, clamp edges, ClassKit SkillBumps regression
- `tests/Shared/ClassKitRegistryTests.cs` — Str/Dex stat tests replaced with SkillBumps tests
- **196 tests, 0 failures** (up from 170)

### User question at session end
- User asked "is there a roll feature, are stats shown anywhere on screen?" → No: `CharacterCreateScreen.cs` is M5 Phase 5, not yet built. `GameSession.RolledStats` is always `null` right now; server uses `StatBlock(13,12,10,10)` fallback.

---

## What was done this session (2026-07-05, session 4)

### M4 demo gate — PASSED
- All checklist items green: setup, minimap/nav, melee, block, ranged, death & respawn
- One post-gate fix: hunger/rest now resets on combat death (`HealthSystem.KillPlayer` calls `NeedsSystem.ResetNeeds`)

### Real player stats wired (M4 close-out)
- `ClassKitData` gains `Str`/`Dex` fields; Fighter: Str=16/Dex=10, Ranger: Str=10/Dex=15 (combat.md §2.4/§4.2)
- `CombatResolver` player helpers now take explicit `(str, dex)` params — constants removed
- `CombatSystem` stores `_playerStats` per peer; exposes `SetPlayerStats`, `GetPlayerTargetNumber`, `GetPlayerStats`
- `HealthSystem.RequestSetClass` RPC added — remote clients call it on spawn to announce their class; server re-distributes correct kit and stats. Fixes pre-existing gap where joining clients always got the host's class
- `PlayerController._Ready()` calls `AnnounceClass()` deferred for the local player
- `MonsterSystem.TickAttack` uses `CombatSystem.GetPlayerTargetNumber` (real Dex-derived TN)
- `ProjectileSystem` uses `CombatSystem.GetPlayerStats` for player ranged damage mod
- 7 new `CombatResolverTests` stat tests, 3 new `ClassKitRegistryTests` stat tests

### Style cleanup
- `CLAUDE.md` C# style section updated: K&R → Allman, added `partial` requirement, property-over-field, Godot.Collections boundary rule, modifier ordering
- Full 71-file codebase audit: 0 violations on `partial`, public fields, modifier ordering, Godot.Collections; 5 inline K&R blocks fixed in MeleeController, PlayerController, MonsterSystem (×2), NestGenerator
- **170 tests, 0 failures**

---

## What was done this session (2026-07-05, session 3)

### Faction allegiance system (ADR-0024)
- `shared/FactionType.cs` — enum: MonsterNest, Village, PlayerSettlement
- `shared/FactionRelationship.cs` — enum: Allied, Neutral, Hostile
- `shared/FactionService.cs` — two-layer model: type-level defaults + instance overrides; §4 hard rule (no PlayerSettlement×PlayerSettlement Hostile override); `Reset()` for test isolation
- `shared/NestData.cs` — `FactionId` field added (default `""`, backward compatible)
- `server/NestSystem.cs` — registers PLAYER_FACTION_ID at startup; assigns unique `faction.nest.{id}` per nest; passes FactionId to SpawnMonster
- `server/MonsterSystem.cs` — FactionId on MutableMonster; SpawnMonster signature extended; aggro gate wrapped in `FactionService.IsHostile(m.FactionId, PLAYER_FACTION_ID)`; `GetMonsterFactionId(long id)` public API
- `server/ProjectileSystem.cs` — replaced MONSTER_ID_THRESHOLD ad-hoc patch with `FactionService.IsHostile(originFaction, targetFaction)` gate; also extended shooter exclusion to monster nodes (see bug fix below)
- `tests/Shared/FactionServiceTests.cs` — 16 tests: type defaults, instance overrides, symmetry, §4 hard rule, unknown-faction fallback, IsHostile convenience
- `docs/decisions/ADR-0024-faction-allegiance-system.md` — documents problem, decision, alternatives, consequences

### Bug fixes
- **[P1] Bandit archer arrows invisible:** ProjectileSystem excluded only `Players/Player_{id}` from sphere query; for monster origin (id ≥ 10001) the node wasn't found, projectile immediately re-hit the firing monster, ClientRemoveArrow fired in same tick as ClientSpawnArrow. Fixed: also look up `Monsters/Monster_{id}`.
- **[P1] Shield blocking ineffective vs monster melee:** MonsterSystem.TickAttack had no blocking check. Fixed: added `CombatSystem.Instance?.IsBlocking(m.TargetPeer)` gate before dice roll, with "Block!" feedback RPC.
- **[P1] Shield blocking ineffective vs projectiles:** no gate in ProjectileSystem. Fixed as part of §12.4 (see below).

### GDD §12 — Simultaneous block-and-attack
- `docs/gdd/combat.md` §12 appended: block+attack allowed but attacker takes −3 Attack Bonus penalty (placeholder, balancing pass); shield also intercepts projectiles (§12.4)
- `server/CombatSystem.cs` — `if (IsBlocking(sender)) attackBonus -= 3;` before ResolveAttack
- `server/ProjectileSystem.cs` — `CombatSystem.Instance?.IsBlocking(targetId.Value)` check; blocked arrows show "Block!" RPC and are removed
- `tests/Shared/CombatResolverTests.cs` — `ResolveAttack_BlockPenalty_ReducesHitRate` regression test (same-seed RNG, AB=5 vs AB=2 vs TN=12)

### UX
- Floating combat text font sizes halved: Block!=13, Miss=12, Crit=16, Normal=12

### Totals
- **163 tests, 0 failures**

---

## What was done this session (2026-07-05, continued)

### Phase 4.7 — Equipment catalog (wrap-up)
- `WeaponRegistryTests.cs` rewritten: new `item.weapon.*` IDs, DamageDice/DamageType assertions, 37-weapon count check, OldId_ReturnsNull guard
- `ArmorRegistryTests.cs` created (15 tests): count, category counts, individual armor values vs GDD, shield ShieldBonus=2
- `MonsterRegistryTests.cs` — stale `weapon.shortbow` ID corrected to `item.weapon.shortbow`
- `data/base/weapons/` directory deleted (5 superseded files: sword, shield, shortbow, hunting_knife, arrow)
- 96 tests passing at end of 4.7

### Phase 4.8 — Dice-based combat resolution
- `shared/CombatResolver.cs` — new file: `StatModifier` (floor((stat-10)/4) with correct floor division), `RollDice` (XdY+Z notation), `ResolveAttack` (1d20 + AB vs TN; nat20 = crit, double dice; nat1 = always miss; min 1 damage on hit), `PlayerAttackBonus`/`PlayerTargetNumber`/`PlayerDamageMod` (all return 0/10/0 in Phase 4.8 with placeholder stats)
- `shared/MonsterData.cs` — replaced `MeleeDamage: float` with `AttackBonus: int`, `TargetNumber: int`, `DamageDice: string`, `DamageType: string`
- `shared/MonsterRegistry.cs` — authored values for all 4 monsters: Wolf AB=3/TN=12/1d6 piercing (exact GDD §6.2 example), Goblin AB=2/TN=11/1d6 slashing, Bandit AB=3/TN=13/1d8 slashing, BanditArcher AB=3/TN=12/1d6 piercing; fixed ranged_weapon_id to item.weapon.shortbow
- `data/base/monsters/*.json` — all 4 updated (melee_damage removed, attack_bonus/target_number/damage_dice/damage_type added)
- `server/CombatSystem.cs` — blocking is now a **hard gate** (attack nullified entirely per GDD §2.5, not 50% reduction); then ResolveAttack; `GetEntityTargetNumber` helper (looks up MonsterData.TargetNumber or PlayerTargetNumber); seeded `_combatRng` (WorldSeed ^ 0xC0BA7001)
- `server/MonsterSystem.cs` — TickAttack melee now uses ResolveAttack vs PlayerTargetNumber(); miss is logged and skipped; `GetMonsterData(long id)` public API added; seeded `_monsterRng` (WorldSeed ^ 0xD1CE5EEDu); `ParseFlatDamage` not present (was only in CombatSystem/ProjectileSystem)
- `server/ProjectileSystem.cs` — `ParseFlatDamage` removed; ranged hits use `CombatResolver.RollDice(weapon.DamageDice) + PlayerDamageMod` (player) or `+0` (monster); seeded `_projectileRng` (WorldSeed ^ 0xAB0570FFu)
- `tests/Shared/CombatResolverTests.cs` — new (36 tests): full StatModifier table (15 cases), RollDice bounds (5 notations including 1d1 and empty), ResolveAttack scenarios, player helper return values
- `tests/Shared/MonsterRegistryTests.cs` — MeleeDamage test replaced with AttackBonus/TargetNumber/DamageDice tests + Wolf GDD example verification
- **132 tests, 0 failures**

---

### Phase 6 — Class kit selection (code complete)
- `shared/ClassKitData.cs` + `ClassKitItem` (top-level record, not nested — C# primary constructor scope limitation)
- `shared/ClassKitRegistry.cs` — Fighter: longsword + shield; Ranger: shortbow + 20 arrows
- `shared/GameSession.cs` — `ChosenClassId` field (default "class.fighter", cleared in Reset())
- `client/ClassSelectScreen.cs` — two-button Control; sets ChosenClassId → goes to GameWorld
- `client/MainMenuController.cs` — Solo/Host/Join all route through ClassSelectScreen.tscn now
- `server/HealthSystem.cs` — debug kit removed; ClassKitRegistry.Find(ChosenClassId) loop with Fighter fallback
- `data/lang/en.json` — 6 new class keys
- `tests/Shared/ClassKitRegistryTests.cs` — 14 tests
- **146 tests, 0 failures**
- **Editor task pending:** `scenes/ClassSelectScreen.tscn` — Control + VBoxContainer(%TitleLabel, %SubtitleLabel, HBoxContainer(%FighterButton, %RangerButton)); script = client/ClassSelectScreen.cs

---

## What was done previously (2026-07-05, earlier)

### Phase 4.6 — Minimap + world map
- `shared/NestGenerator.cs` — pure-C# deterministic nest placement extracted from NestSystem; called identically by server and client (same seed → same positions, no RPC needed)
- `server/NestSystem.cs` — refactored to use NestGenerator; diagnostic prints added then removed during debug
- `client/MinimapHUD.cs` — CanvasLayer Layer=30, top-right 150×150; terrain texture baked from CachedHeightmap; nest dots (wolf=orange, goblin=green, bandit=red); player dot; Kingdom Marker ring; **death drop red X** (new this session)
- `client/WorldMapScreen.cs` — CanvasLayer Layer=31, hidden; M key / Escape toggles; 700×700 centred panel; colour legend; "YOU" label; **death drop red X + "DROP" label** (new this session)
- `client/MainMenuController.cs` — registered `open_map` → M key
- Fixed `partial` missing on inner `_DrawControl` / `_MapDrawControl` classes (GD0001 error)
- **Editor tasks done by Edu:** MinimapHUD + WorldMapScreen CanvasLayer nodes added to GameWorld.tscn

### Phase 5 — Nests & death penalty
- `shared/NestData.cs` — record: Id, MonsterTypeIds[], WorldX, WorldZ, RespawnDelaySec
- `server/NestSystem.cs` — 5 seeded nests (2 wolf/45s, 2 goblin/60s, 1 bandit camp/90s); respawn on full clear
- `server/MonsterSystem.cs` — SpawnMonster() public + returns ID; NestId on MutableMonster; NestMonsterDied static event; loot → SpawnItemDrop; removed hardcoded SPAWNS; **Y snapped to terrain height each tick** (monster sinking fix, new this session)
- `server/HealthSystem.cs` — KillPlayer drops inventory as ItemDrop; SpawnItemDrop() public API; RequestPickupDrop RPC; ClientSpawnItemDrop / ClientRemoveItemDrop RPCs; **ClientShowDeathMarker RPC** (death drop map marker, new this session)
- `client/PlayerController.cs` — E-interact mask adds 128u; ItemDrop pickup priority
- `shared/LocalState.cs` — DeathDropWorldPos (float X, float Z)?, SetDeathDrop(), ClearDeathDrop() — no Godot.Vector3 in shared/
- `client/BuildMenu.cs` — fixed `AddChild` during `_Ready()` → `CallDeferred` (was causing "parent busy" error)
- **Editor tasks done by Edu:** NestSystem node added to GameWorld.tscn (after MonsterSystem); Monster.tscn created (CharacterBody3D + MonsterNode.cs)
- **Debug trap:** NestSystem had NeedsSystem script attached by mistake — corrected by Edu

### GDD — Combat resolution
- `docs/gdd/combat.md` — full locked design saved (overwrite of prior stub)
- Phase 4.7 implementation plan presented and waiting for go (see below)

---

## What was done previously (2026-07-04 — M4 Phases 1–4.5)

### Phase 1 — Health & damage
- `shared/HealthData.cs`, `shared/MsgEntityHealth.cs`
- `server/HealthSystem.cs` — HP map for players + monsters; Damage/Heal/Kill; death → drop inventory + respawn at shelter
- `client/HealthHUD.cs` — HP bar top-left
- `shared/LocalState.cs` — CurrentHp, MaxHp, SetHealth()

### Phase 2 — Weapons & melee
- `shared/WeaponData.cs`, `shared/WeaponRegistry.cs` — sword, shield, shortbow, hunting knife
- `data/base/weapons/*.json` — 5 weapon reference files
- `server/CombatSystem.cs` — RequestMeleeAttack (range, cooldown, alive checks), RequestSetBlocking (50% flat reduction), RequestCraftArrows (3 wood → 5 arrows)
- `client/MeleeController.cs` — LMB swing, RMB block, sphere target query; gated on InCombatMode

### Phase 3 — Ranged combat
- `shared/ProjectileState.cs` — flat-float record (xUnit compatible)
- `server/ProjectileSystem.cs` — parabolic tick, sphere hit detection, FireFromMonster() API
- `client/BowController.cs` — horizontal-plane mouse aim, arrow ghost meshes; gated on InCombatMode + PreferRanged
- Arrow crafting: E key at Workbench → CombatSystem.RequestCraftArrows
- `scenes/Arrow.tscn` created by Edu

### Phase 4 — Monsters & AI
- `shared/MonsterData.cs`, `shared/MonsterRegistry.cs` — wolf, goblin, bandit, bandit_archer
- `data/base/monsters/*.json` — 4 monster reference files
- `server/MonsterSystem.cs` — Idle/Aggro/Attack AI; position broadcast; death → loot to nearest player; bandit_archer uses ProjectileSystem.FireFromMonster; monster IDs start at 10001L
- `client/MonsterNode.cs` — colour-coded capsule, server-lerp, hit flash, death hide; CollisionLayer=64u
- `scenes/Monster.tscn` + MonsterSystem node + Monsters container added to GameWorld.tscn by Edu

### Phase 4.5 — Combat/build mode state + WeaponHUD
- `LocalState.InCombatMode` (default false = build mode), `ToggleCombatMode()`, `CombatModeChanged` event
- `LocalState.PreferRanged` + `ToggleWeaponMode()` — melee/ranged toggle within combat mode
- `C` key = toggle combat/build; `Q` key = toggle melee/ranged
- `BuildMenu` blocks B in combat mode with 2s warning flash; closes on mode switch
- `PlacementController` cancels ghost on mode switch to combat
- `client/WeaponHUD.cs` — bottom-centre label: `[Build Mode]` (blue) / `[Combat · Melee · Sword]` (red)
- WeaponHUD CanvasLayer (Layer 13) added to GameWorld.tscn by Edu

---

## What's next

1. **Smoke-test Load Game + equipment slots:** new game → play → Escape → Save → Quit to Menu → Load Game → select slot → resumes correctly; press K → Equipment section shows auto-equipped longsword/shield; click a slot → picker opens; Escape closes picker then sheet.
2. **M8 Phase 3:** fog of war probe, loc audit, demo gate.
3. **M8 Phase 3:** fog of war probe, loc audit, demo gate (30-min play → quit → resume).
4. **Post-M8 (M9):** NPC idle at village marker; shelter capacity (4 NPCs/shelter). Plan in TODO.md §M9.

---

---

## Blocked

Nothing.

## Decisions needed from Edu

Nothing blocked.

## Important: localization file

**Always edit `project/data/lang/en.json`** — that is the file Godot loads via `res://data/lang/en.json`.
The root `data/lang/en.json` was a stale duplicate and has been deleted. Do not recreate it.

---

## Session log

### 2026-07-20 — M10: river generation + terrain carving + forest clustering + NeedsSystem HP decay
- **NeedsSystem overhaul:** Hunger=0 → HP drain at 2f/60f/s (was: instant kill). Rest=0 → three-phase exhaustion (MoveSpeed×0.5 → AttackBonus−2 + stumble → HP drain). Private KillPlayer removed; death routes through HealthSystem.KillPlayer.
- **RiverGenerator:** D8 downhill walk, cosine taper carving, 1D width noise. MIN_RIVER_STEPS=20 prevents degenerate 1-step paths from border-start sources.
- **TerrainSystem pipeline:** GenerateHeightmap → RiverGenerator.Generate() in-place carve → River property → HeightMapShape3D. Trees + bushes receive carved heightmap automatically.
- **TreeGenerator + TreeSystem:** `riverMask` exclusion prevents trees in carved channel.
- **BushGenerator clustering:** NEAR_TREE_CHANCE=0.70 within 3-tile radius of any tree; ISOLATED_CHANCE=0.30 elsewhere. Channel mask exclusion.
- **WaterSystem:** ArrayMesh ribbon (UV.y = arc-length, tangent = flow). Loads water_river.gdshader at runtime; flat-blue fallback. Area3D WaterTrigger per-segment.
- **water_river.gdshader:** Scrolling normal map, Roughness=0.05, Specular=0.8.
- **docs/gdd/water.md:** Full algorithm + carving + ribbon + shader spec.
- **ARCHITECTURE.md §4.4:** MoveSpeed client-prediction note added.
- 370 tests, 0 failures. 3 NeedsSystem BUGS.md P1 entries closed.

### 2026-07-18 (session 2) — Post-playtest bug fixes + weapon slots + MoveSpeed sync + docs cleanup
- **Death drop (compile fix):** `0xHP1234u` → `0xD1CE1234u` in HealthSystem. H/P are not valid hex digits; Godot was running a stale binary predating the death-drop system. No logic change needed — the drop code was already correct.
- **Fog not restored on load:** `FogSystem.BroadcastFog()` made public; `SaveSystem.TryLoad()` now calls it after `RestoreFogFromBase64`. Previously clients kept all-UNSEEN state forever because `_Process` only broadcasts on newly-explored cells.
- **NPC workers gone after load:** `GetAssignmentsForSave()` now iterates `_settlementNpcs` (not just `_workAssignments`), capturing sleeping NPCs (`_suspendedStation`) and idle settlers. `RestoreAssignmentsFromSave()` handles empty station for idle members.
- **Tier 1:** MeleeController + BowController read from `LocalState.EquippedMainHand/OffHand` first (inventory scan fallback for legacy saves). MoveSpeed buff RPC now pushes multiplier to affected client peer; `LocalState.SetMoveSpeedBuff(multiplier, durationMs)` uses TickCount64 expiry; `PlayerController` applies `MoveSpeedMultiplier`.
- **Tier 2:** Dead `RequiresPresence` field removed from BuildingData; VillageSystem time accumulators float→double; VillageGenerator bare `Random` → `System.Random`.
- **Tier 3:** SaveUtil.cs Godot dependency removed via provider delegate pattern; implementation moved to SaveSystem as private statics; test .csproj exclusion removed.
- **Docs:** ARCHITECTURE.md §6 (entity model — actual flat-dict, two ID spaces) and §7 (per-system seeded System.Random) rewritten. CLAUDE.md rule 6 and rule 8 tightened to match.
- **ClassSelectScreen removed:** dead file superseded by CharacterCreateScreen.
- 349 tests, 0 failures. Commits: a6280de + 1c6b7c0 on origin/main.

### 2026-07-18 — Block system redesign (§15): mutual exclusivity + TN bonus + ranged crit threshold
- **Mutual exclusivity:** RMB block now disables LMB attack input. Enforced client-side (MeleeController + BowController early-return) AND server-side (RequestMeleeAttack + RequestFireProjectile gate on `IsBlocking(sender)`). `LocalState.IsBlocking` added as shared bridge between controllers.
- **Melee TN bonus +4:** `CombatSystem.GetPlayerTargetNumber` adds +4 while `IsBlocking(peerId)` and shield confirmed equipped. Orc (AB 5) hit chance: 70% → 50%. Removed §12's −3 AB penalty (superseded).
- **Ranged crit threshold:** `BLOCKING_CRIT_THRESHOLD = 24` in ProjectileSystem. Bandit Archer (AB 3) crit chance vs blocking Fighter: 5% → 0%. Physical hit rate unchanged (§13.1). Intentional asymmetry: melee → hit frequency reduction; ranged → crit severity reduction.
- **combat.md §15** added (full rule text, worked examples, asymmetry docs). §2.5 and §12 marked superseded.
- **IDEAS_BACKLOG:** [post-slice] entry: BLOCKING_CRIT_THRESHOLD = 24 is coincidental to Bandit Archer AB 3; revisit before adding any ranged enemy with AB ≥ 4.
- 350 tests, 0 failures.

### 2026-07-15 — M9 pre-playtest content fixes (session 3)
- **WorldSeed randomisation**: `CharacterCreateScreen.OnConfirm()` now calls `GameSession.WorldSeed = (uint)GD.Randi()`. Every new game produces a different procedural world. BUGS.md [P2] closed.
- **Sickle at Foraging 15**: `SkillRegistry` Foraging ToolTiers now grants `item.tool.sickle`. `en.json` name/desc added.
- **Stew Pot at Cooking 10**: `SkillRegistry` Cooking ToolTiers now grants `item.tool.stew_pot`. `en.json` name/desc added.
- **Wooden Wall + Gate**: `BuildingRegistry` adds `WoodenWall` (5 wood, 2×3×0.4) and `WoodenGate` (10 wood, 2×3×0.4); `en.json` keys added. Scenes are editor tasks.
- **Herb system (new)**: `shared/HerbPatchData.cs` + `shared/HerbGenerator.cs` (30 patches, XOR salt `0x48455242`); `server/HerbSystem.cs` — purple sphere nodes, 60s cooldown, NPC API (`GetAvailableHerbPatchIds`, `GetHerbPosition`, `IsAvailable`, `ForagerHarvestHerb`). Needs editor task: add HerbSystem node to GameWorld.tscn after BushSystem.
- **Forager NPC movement redesign**: replaced static 30s timer with woodcutter-style movement loop. NPC now walks to nearest herb patch or berry bush, harvests, carries up to 6 items, walks to stockpile to deposit. New state: `_foragerTarget`, `_foragerCarriedHerbs`, `_foragerCarriedBerries`, `_foragerWalkToDeposit`. New tick: `TickForagerDeposit`. BushSystem gained NPC API (`GetAvailableBushIds`, `GetBushPosition`, `IsAvailable`, `ForagerHarvestBush`). Forager prefers herbs over berries when both are in range.

### 2026-07-15 — Buff/debuff system (combat.md §5.4 Phase A implementation)
- New shared: `CritEffect` (5 entries), `FumbleEffect` (4 entries), `BuffStat`, `BuffAmountType`, `ActiveBuff` (record), `BuffCalculator` (pure static, testable)
- New server: `BuffSystem` Node — `AddBuff/RemoveBuffs/ClearAllBuffs`, query API (`GetAdditiveModifier`, `GetMultiplicativeModifier`, `IsBuffActive`), `ApplyCritEffect`/`ApplyFumbleEffect`, bleed DoT tick (1s interval via `_Process`), lazy expired-entry sweep
- `CombatResolver`: `ResolveAttack` gains `isFumble` return (§5.2 asymmetry: nat-1 only fumbles if 1+AB < TN); `RollCritEffect`/`RollFumbleEffect` static methods
- `CombatSystem`: stun/disarm gates before cooldown commit; AB debuff applied to attack roll; armor debuff applied in `GetPlayerTargetNumber`; crit/fumble effects applied after resolution
- `HealthSystem`: `ApplyDamage` multiplies by `IncomingDamage` multiplier (vulnerability); `KillPlayer`/`KillMonster` clear buffs
- `MonsterSystem`: stun gate in `TickAttack`; monster AB debuff; monster crit/fumble (§5.2 symmetry)
- `ProjectileSystem`: stun/disarm gates in `RequestFireProjectile`; AB debuff on shooter; crit effect on hit
- New tests: 4 new `CombatResolverTests` (fumble asymmetry ×2, crit/fumble coverage ×2); 22 new `BuffCalculatorTests` (condensation correctness including the two-+50%-buffs = ×2.0 key case)
- MoveSpeed buff: tracked server-side, client sync (RPC to peer) deferred with TODO comments

### 2026-07-15 — ESC fallback fix + CharacterSheet StyleBoxFlat polish
- BuildMenu: ui_cancel handler added — Escape now closes build menu (+ restores combat mode) before PauseMenu sees it
- CharacterSheet: full StyleBoxFlat pass — dark parchment panel, antique gold borders/separators, inset slot buttons with hover/pressed states, gold section headers, muted subtext labels, reddish Unequip button; palette in COL_* constants

### 2026-07-14 — M8 Phase 2c: equipment slots (inventory.md §10) — 12 new tests
- EquipSlot enum; PlayerInventory equipped fields; LocalState equipped state + EquippedSlotChanged event
- CombatResolver.PlayerTargetNumber armor/shield/category optional params
- InventorySystem: EquipItem server API + RequestEquipItem RPC + SyncEquippedSlotsTo + ApplyEquippedSlot
- CombatSystem: GetPlayerTargetNumber reads from equipped slots; shield gates check EquippedOffHand first
- HealthSystem: AutoEquipKitItems infers slot from WeaponRegistry/ArmorRegistry at kit distribution
- SaveSystem: saves and restores 3 equipped slot fields per player (additive, no version bump)
- CharacterSheet: Equipment section with slot buttons + inline item picker; string-based Rpc to avoid Server import
- en.json: charSheet.equipment + charSheet.slot.* keys
- 6 new InventoryTests + 6 new CombatResolverTests

### 2026-07-14 — M8 Phase 2b: named saves + Load Game UI
- SaveUtil (shared): ListSaves() + PeekSession() — client-safe, no Server import
- SaveData: SessionSave class embedded in save (class/race/stats for Load Game without CharacterCreateScreen)
- GameSession: SaveName field; SaveRequested event + RequestSave() bridge (avoids client→server import)
- SaveSystem: named save paths (user://saves/{SaveName}.json), EnsureSaveDir(), Session save/restore, SaveRequested subscription
- CharacterCreateScreen: stamps SaveName timestamp on OnConfirm
- LoadGamePanel (new): programmatic Control; auto-selects newest save; double-click to load; class/race detail from PeekSession
- PauseMenu (new): CanvasLayer Layer=50; Escape toggle; Resume/Save/Load/Quit; embeds LoadGamePanel
- MainMenuController: "Load Game" button injected after StartSolo; LoadGamePanel overlay
- UX bug fixed: Load button was disabled by default — now auto-selects first save so it's immediately active
- Editor task pending: PauseMenu CanvasLayer to GameWorld.tscn

### 2026-07-06 — M5 Phase 4: character sheet (213 tests)
- CharacterSheet.cs: Layer 26, K key, race/class/stats/skills+caps, orange cap indicator
- char_sheet K key registered; charSheet.* loc keys added
- Editor task pending: add CharacterSheet CanvasLayer to GameWorld.tscn

### 2026-07-06 — M5 Phase 3: inventory panel (213 tests)
- InventoryPanel.cs: Layer 25 modal, I key, Escape, scrollable item list, InventoryChanged event
- LocalState.InventoryChanged event; resource.wood.name loc key added; open_inventory I key registered
- Editor task pending: add InventoryPanel CanvasLayer to GameWorld.tscn

### 2026-07-06 — M5 Phase 2: skill system (213 tests)
- ToolTierData, SkillData, SkillRegistry (6 skills), SkillSystem (Node, XP+bump, level-up, RPC)
- LocalState: SkillLevels dict + SetSkillLevel + SkillLevelChanged event
- Triggers wired: melee hit, player ranged hit, harvest, cook, tree fell
- TreeSystem placeholder _playerXp dict removed; HealthSystem.RequestSetClass calls ApplyBump
- en.json: 6 skill keys + bronze_axe; 10 new tests; 213 total, 0 failures

### 2026-07-08 — §13 ranged asymmetry + PlayerAnimator (214 tests)
- GDD §13 locked: ranged physical contact = auto-hit; d20+AB roll → normal/crit only
- CombatResolver.PlayerAttackBonus gains skillLevel param; SkillSystem.GetSkillLevel() added
- CombatSystem melee AB now uses real skill.melee level; ProjectileSystem ranged hit rewritten
- PlayerAnimator: full KayKit animation state machine (Idle/Walk/Run/Jump/Hit/Death/Throw)
- LocalState: DamageTaken/PlayerDied/PlayerRevived/LocalArrowFired events
- Player.tscn: old capsule deleted; KnightMeshes + RangerMeshes added (editor done by Edu)
- 214 tests, 0 failures (+1 PlayerAttackBonus skill level test)

### 2026-07-06 — M5 Phase 5: character creation screen (196 tests)
- Phase order decision: B (Phase 5 first)
- CharacterCreateScreen.cs: 3d6 roll, 4 races, Human bonus stat, class picker, reroll, confirm → GameWorld
- MainMenuController: all three paths rerouted to CharacterCreateScreen.tscn
- Editor task pending: CharacterCreateScreen.tscn
- 196 tests, 0 failures

### 2026-07-06 — M5 Phase 1: stat foundation + race system (196 tests)
- StatBlock, RaceRegistry (4 races + Apply()), GameSession race/stats fields
- ClassKitData Str/Dex removed → SkillBumps (Fighter Melee+5/Athletics+3, Ranger Ranged+5/Foraging+3)
- CombatSystem._playerStats upgraded from tuple to StatBlock; RequestSetStats RPC added
- PlayerController.AnnounceStats() deferred — no-op until CharacterCreateScreen sets RolledStats
- 26 new tests (StatBlockTests + RaceRegistryTests + updated ClassKitRegistryTests); 196 total, 0 failures
- User asked about char creation UI → confirmed Phase 5 not yet built; phase order decision pending

### 2026-07-05 — M4 complete: stats wired, demo gate passed, style cleanup (170 tests)
- M4 demo gate passed (all sections green)
- Hunger/rest reset on combat death
- Real player stats (Fighter Str=16, Ranger Dex=15) wired end-to-end through CombatResolver, CombatSystem, MonsterSystem, ProjectileSystem
- RequestSetClass RPC fixes remote-client class selection gap
- CLAUDE.md style rules updated (Allman, partial, modifier ordering, Godot.Collections)
- 71-file style audit: 5 K&R blocks fixed, no other drift found
- 170 tests, 0 failures

### 2026-07-05 — Faction system, block-and-attack §12, three P1 bug fixes (163 tests)
- Faction allegiance system (ADR-0024): FactionService two-layer model, per-nest faction IDs, ProjectileSystem + MonsterSystem faction gates replace MONSTER_ID_THRESHOLD patch
- Three P1 bug fixes: bandit archer arrows invisible (shooter exclusion gap), shield not blocking monster melee (TickAttack had no gate), shield not blocking arrows (no gate in ProjectileSystem)
- GDD §12 implemented: −3 Attack Bonus penalty when blocking while swinging; shield intercepts projectiles
- Floating combat text font sizes halved
- 163 tests, 0 failures

### 2026-07-05 — M4 Phases 4.6 + Phase 5 complete; combat GDD locked
- Minimap (150×150 top-right) and world map (M key, 700×700) implemented with terrain, nests, player, Kingdom Marker
- Death drop red X marker on both maps, cleared on item pickup
- Monster Y terrain-snapping fix (monsters no longer sink into hills)
- BuildMenu deferred AddChild fix (was throwing "parent busy" on B key)
- NestSystem script mis-attachment debugged (had NeedsSystem attached, now NestSystem)
- `docs/gdd/combat.md` locked v0.1 — all five open questions resolved
- Phase 4.7 plan written, awaiting Edu go
- 74 tests passing

### 2026-07-04 — M4 Phases 1–4.5
- Health, melee, ranged, monsters, AI all implemented and tested
- Combat/build mode state machine added (C key); weapon mode toggle (Q key)
- WeaponHUD bottom-centre label for mode feedback
- 74 tests passing

### 2026-07-03 — M1 complete
- ENet multiplayer, WASD, main menu, splash transition
- M1 demo verified: two instances over LAN, both capsules visible, smooth movement

### 2026-07-02 — M0 complete
- Godot project structure, Loc system, GodotSteam, xUnit, GitHub Actions CI
