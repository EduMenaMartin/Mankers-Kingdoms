# Changelog

Notable changes to the project. Not a git log — a human-facing history of what shipped in each milestone or version.

Follows [Keep a Changelog](https://keepachangelog.com/) conventions loosely.

---

## [M9] — in progress — Vertical slice playtest

### Pre-playtest content fixes (2026-07-15)

- **WorldSeed now randomised per new game** — `CharacterCreateScreen.OnConfirm()` calls `GameSession.WorldSeed = (uint)GD.Randi()` before scene transition. Previously every new game produced the same world (seed was always `42u`).
- **Herb patch system** — `shared/HerbPatchData.cs` + `shared/HerbGenerator.cs` (30 patches, XOR salt `0x48455242u`); `server/HerbSystem.cs` spawns purple sphere nodes on all peers deterministically; 60s harvest cooldown; exposes server NPC API.
- **Forager NPC movement loop** — replaced static 30s timer (`TickForagerJob` produced herbs without moving) with a woodcutter-style loop: NPC finds nearest herb patch or berry bush, walks to it, harvests, carries up to 6 items, walks to Stockpile Drop, deposits herbs and berries separately into settlement stockpile. `BushSystem` gained `GetAvailableBushIds`, `GetBushPosition`, `IsAvailable`, `ForagerHarvestBush` NPC API.
- **Sickle at Foraging 15** — `SkillRegistry` Foraging `ToolTiers` now grants `item.tool.sickle`; loc keys added.
- **Stew Pot at Cooking 10** — `SkillRegistry` Cooking `ToolTiers` now grants `item.tool.stew_pot`; loc keys added.
- **Wooden Wall + Wooden Gate** — `BuildingRegistry` adds both building types (Wall: 5 wood, 2×3×0.4; Gate: 10 wood, same); loc keys added. Scenes are editor tasks.

---

## [M8] — 2026-07-15 — Save/load and polish (COMPLETE)

### Added (2026-07-14 – 2026-07-15)

**Named saves + Load Game UI (Phase 2b)**
- `shared/SaveUtil.cs` — `ListSaves()` + `PeekSession()` client-safe helpers; no Server import needed
- `shared/GameSession.cs` — `SaveName` field; `SaveRequested` event + `RequestSave()` bridge (PauseMenu → SaveSystem without cross-namespace import)
- `shared/SaveData.cs` — `SessionSave` class (class/race/stats) + `Session` field; embedded in every save so Load Game restores character without re-running CharacterCreateScreen
- `server/SaveSystem.cs` — `SavePath` uses `GameSession.SaveName`; `EnsureSaveDir()`; saves/restores `data.Session`; subscribes `SaveRequested`
- `client/LoadGamePanel.cs` — programmatic Control; `SaveUtil.ListSaves()` populates list newest-first; class/race detail from `PeekSession()`; auto-selects slot 0; double-click to load
- `client/PauseMenu.cs` — CanvasLayer Layer=50; Escape toggle; Resume/Save/Load/Quit; embeds `LoadGamePanel`
- `client/MainMenuController.cs` — "Load Game" button injected after StartSolo; `LoadGamePanel` overlay added
- `data/lang/en.json` — `menu.load_game`, `pause.*`, `load_panel.*` keys

**Equipment slots — inventory.md §10 (Phase 2c)**
- `shared/EquipSlot.cs` — enum: MainHand=0, OffHand=1, BodyArmor=2
- `shared/PlayerInventory.cs` — three nullable equipped fields; `GetEquipped`/`SetEquipped`/`ClearEquippedSlotsFor`; `Clear()` resets equipped slots
- `shared/SaveData.cs` — `PlayerSave.EquippedMainHand/OffHand/BodyArmor` (nullable; additive — no version bump)
- `shared/LocalState.cs` — equipped properties; `GetEquipped`/`SetEquipped`; `EquippedSlotChanged` event
- `shared/CombatResolver.cs` — `PlayerTargetNumber` optional `armorValue`, `shieldBonus`, `armorCategory` params; Medium caps Dex mod at +1, Heavy zeroes it
- `server/InventorySystem.cs` — `EquipItem` server API; `RequestEquipItem` RPC; `SyncEquippedSlotsTo`; `ApplyEquippedSlot` RPC; slot eviction on `RemoveItems`/`ClearItem`/`TakeAll`
- `server/CombatSystem.cs` — `GetPlayerTargetNumber` reads equipped armor + shield; block gate uses equipped OffHand first
- `server/HealthSystem.cs` — `AutoEquipKitItems` infers MainHand/OffHand/BodyArmor from WeaponRegistry/ArmorRegistry at kit distribution
- `server/SaveSystem.cs` — saves and restores three equipped fields per player
- `client/CharacterSheet.cs` — Equipment section (3 slot rows + buttons); inline `_equipPicker` panel; compatible-item filtering; `RequestEquipItem` via string-based Rpc (no Server import); live refresh on `EquippedSlotChanged`

**ESC fallback + CharacterSheet visual polish (Phase 3 partial)**
- `client/BuildMenu.cs` — `ui_cancel` handler: Escape now closes build menu (and restores combat mode) before reaching PauseMenu; all other panels already handled Escape correctly
- `client/CharacterSheet.cs` — StyleBoxFlat pass: dark warm-charcoal background, antique gold border + separators, inset slot buttons with hover/pressed states, muted subtext labels, gold section headers; `COL_*` palette constants for easy re-theming

### Tests
- 12 new tests: 6 `InventoryTests` (equipment slot CRUD) + 6 `CombatResolverTests` (`PlayerTargetNumber` armor/shield/ArmorCategory variants)

---

## [M7] — 2026-07-11 — Class-gated building (COMPLETE)

M7 demo gate passed. All gameplay code, editor tasks, and post-gate fixes complete.

### Added

**Herbalist's Hut building (Phase 1)**
- `BuildingRegistry` — `HerbalistsHut` added (id `building.herbalists_hut`, cost 20 wood, 4×3×4 footprint); `RequiresPresence` field removed from all buildings — gating redesigned to be shelter-based NPC roster
- `SettlementSystem` — `RequestCraftBandage` RPC: costs 2 `item.herb` from player inventory, grants 1 `item.bandage`, awards `skill.foraging` XP; no class/presence gate
- `BuildMenu` — presence-gate label/button system removed; panel simplified

**Forager NPC job loop (Phase 2)**
- `VillageSystem` — `_settlementNpcs SortedSet<string>` (NPC added on recruit, stays in roster after Leave — settlement membership is permanent once recruited); `_npcFounder SortedDictionary<string, long>`; `_lastForageTime SortedDictionary<string, float>`; `FORAGE_COOLDOWN = 30s`, `HERB_PER_FORAGE = 1`
- `TickForagerJob` — called from `TickJobs` when station starts with `"building_herbalists_hut"` (Godot normalises `.` → `_` in node names); produces herb → `SettlementSystem.AddToStockpile` every 30 s
- `RequestAssignNpcToStation` RPC — checks `_settlementNpcs` (no proximity/follower requirement; any recruited NPC can be assigned from the panel); validates shelter presence; resolves stationNode via GetNodeOrNull path; clears any active follow state before assigning
- `RequestUnassignNpc` RPC — removes from `_workAssignments`, clears job state, broadcasts roster
- `BroadcastVillageRoster` + `ClientSetVillageRoster` RPC — JSON array `[{id, name, archetypeKey, station}]` pushed to founder on every assignment change
- `LocalState` — removed `RangerNpcPresent`/`RangerPresent`/`RangerPresentChanged`/`SetRangerNpcPresent`; added `VillageRosterJson`, `VillageRosterChanged` event, `SetVillageRoster(json)`

**Bandage crafting and use (Phase 3)**
- `HealthSystem` — removed `_playerClasses`/`GetPlayerClass`/class tracking (no longer needed without presence gate); added `RequestUseBandage` RPC: validates player alive + below max HP + has `item.bandage`; heal formula: `Mathf.Min(40f, 20f + (foraging / 5) * 1f)` (integer division for floor behaviour); range 20–40 HP
- `PlayerController` — `EatFood()` (Tab): if active hotbar slot is `item.bandage` → `RequestUseBandage`; if not, falls through to food eat path; `TryCraftBandage()` added; E near Herbalist's Hut (priority 5b) → `RequestCraftBandage`; E near Shelter (founder, priority 1) → `BuildingAssignmentPanel.Open()`; removed `TryAssignFollowerToStation` helper
- `BuildingAssignmentPanel` — new CanvasLayer Layer=31; Escape closes; two-column layout (left: NPC list with Select/Unassign; right: station list with current occupant); `RefreshAssignButton` enabled when both NPC + station selected; station scanning via `SettlementSystem` node children with `WORKABLE_PREFIXES = ["building_woodcutters_post", "building_herbalists_hut"]` (underscore form)
- `data/lang/en.json` — `item.herb.name/desc`, `item.bandage.name/desc`, `building.herbalists_hut.name`; removed `warning.build.no_ranger`, `warning.craft.no_ranger`, `build.gate.no_ranger`

### Fixed

- **Station list empty in BuildingAssignmentPanel** — Godot 4 normalises dots to underscores in node names; `"building.woodcutters_post_x_z"` is stored as `"building_woodcutters_post_x_z"`. `WORKABLE_PREFIXES`, `StationDisplayName`, and `VillageSystem.TickJobs` forager route check all updated to use underscore form.

### Tests
- 254 tests, 0 failures
- New suite: `ForagerSystemTests` (9 tests): registry presence, footprint, cost, station node name routing, archetype derivation (Wis→forager, Str→woodcutter, tie-break); `HerbalistsHut_RequiresRangerPresence` test removed (gate no longer exists); `AllBuildings_HaveNoPresenceRequirement` asserts null RequiresPresence on all entries
- New suite: `BandageCraftingTests` (15 tests): heal formula at key breakpoints (level 0→20, 4→20, 5→21, 49→29, 50→30, 100→40 capped, 150→40 capped), integer division floor behaviour confirmed, base never below 20, cap never above 40

---

## [M6] — 2026-07-10 — Village and recruitment (COMPLETE)

M6 demo gate passed. All gameplay code, editor tasks, and post-gate fixes complete.

### Added

**Village generation (Phase 1)**
- `VillagerData` — immutable record: Id, Name, Stats (StatBlock), WorldX/Z; `ArchetypeTag` computed from highest stat (Str→woodcutter, Con→laborer, Dex→guard, Wis→forager); `ArchetypeNameKey` derived
- `VillageData` — immutable record: Id, WorldX/Z, VillagerIds
- `VillageGenerator` — seeded (salt `worldSeed ^ 0xB11A6E00u`); 1 village placed 45–70 units from origin at random angle; 6–10 villagers; each stat = best-of-three 3d6 rolls; Fisher-Yates name draw from pool; returns (VillageData, IReadOnlyList&lt;VillagerData&gt;)
- `data/base/villages/names.json` — 30 medieval fantasy names
- `VillageSystem` — server Node; spawns `VillagerNode` instances from seeded positions; holds all mutable NPC state in `SortedDictionary`s; NPC metadata set on node (id, name, archetype, stats)
- `client/VillagerNode.cs` — CharacterBody3D; teal capsule (r=0.15, h=0.70); Label3D `"{name}\n[{archetype}]"` billboard, NoDepthTest; CollisionLayer 256u; `SetTarget(Vector3)` called via `.Call()` for server position broadcasts
- `data/lang/en.json` — `archetype.*.name` (4 keys) + `village.title`

**Recruitment dialogue (Phase 2)**
- `RecruitmentDialogue` — CanvasLayer Layer=28; opened by E key within 3 m of villager; shows name, archetype, all four stats (highest highlighted gold ★); Recruit / Leave buttons with live state (disabled if player already has follower, or this NPC isn't the current follower); Escape closes; sends `RequestRecruit` / `RequestLeave` RPC via untyped path
- `LocalState` additions: `FollowerNpcId`, `SetFollower`, `ClearFollower`, `FollowerChanged` event
- `VillageSystem` — `RequestRecruit` / `RequestLeave` RPCs; 3 m proximity check; `_followTargets` + `_followerByPeer` reverse-lookup dicts; follow movement in `_PhysicsProcess` (3 m/s, stops 2 m behind player); `ClientMoveVillager` position broadcast (every 3 ticks); sleeping/walking NPCs blocked from recruit
- `PlayerController` — 256u added to interact sphere mask; villager is Priority 1 (above shelter); dialogue guard prevents double-trigger; `TryAssignFollowerToStation` helper
- `data/lang/en.json` — `recruit.*` keys (3)

**Woodcutter's Post + settlement stockpile + NPC job loop (Phase 3)**
- `BuildingRegistry` — `WoodcuttersPost` added (id `building.woodcutters_post`, cost 15 wood, 4×3×4)
- `SettlementSystem` additions: `_stockpile` (SortedDictionary); `AddToStockpile`; `RequestTakeFromStockpile` RPC (moves all to player inventory); `BroadcastStockpile` (JSON string via `System.Text.Json`); `ClientUpdateStockpile` RPC → `LocalState.SetStockpile`; `SpawnMarker` now calls `LocalState.SetMarkerWorldPos`
- `LocalState` additions: `StockpileSnapshot`, `SetStockpile(json)`, `StockpileChanged`; `MarkerWorldPos`, `SetMarkerWorldPos` (float pair, no Godot.Vector3 in shared/)
- `StockpilePanel` — CanvasLayer Layer=29; E key within 3 m of Kingdom Marker; live item list (name via Loc stem lookup + count); "Take All" + Escape close; subscribes to `StockpileChanged`
- `TreeSystem` — `GetAvailableTreeIds()` public API (returns `SortedDictionary.Keys`); `ServerChopTree(treeId)` routes wood yield to `SettlementSystem.AddToStockpile` (no SkillSystem — NPC chops don't award player XP); `TreeSystem.Instance` static property added
- `VillageSystem` — `RequestAssignToStation` RPC; Following→Working state transition (5 m proximity check); job tick: `FindNearestTree` (20 m radius), move toward tree (shared `MoveNpcToward`), `ServerChopTree` (1 s cooldown via elapsed-time comparison)
- `data/lang/en.json` — `building.woodcutters_post.name` + `stockpile.*` keys (4)

**NPC needs (Phase 4)**
- `VillageSystem` needs state: `_npcHunger` + `_npcRest` (0–100, initialized to 100); `_walkingToShelter`; `_sleeping` (villagerId → wake `_elapsed` time)
- `TickNeeds` (1 s interval): drains rest 1/min, restores hunger 0.5/s; rest < 20 → `SuspendForRest`
- `SuspendForRest`: strips Follow or Work state (with `ClientClearFollower` RPC to owning peer), walks NPC to nearest Shelter node under SettlementSystem
- `TickResting`: moves walking NPCs toward shelter, promotes arrivals to `_sleeping` (rest→100, wake = elapsed+30 s), wakes expired sleepers back to Idle
- `TickFollow` and `TickJobs` skip NPCs in `_walkingToShelter` or `_sleeping`
- `FindNearestShelterPosition`: scans SettlementSystem children for node names starting with `"shelter"` (case-insensitive)

### Tests
- 230 tests, 0 failures
- New suite: `VillageGeneratorTests` (16 tests): 1 village placed, 6–10 villagers, stats 3–18, best-of-3 mean > 12, archetype = highest stat, archetype name key convention, no duplicate names, names from pool, determinism, different seeds diverge, unique IDs

---

## [M5] — 2026-07-09 — Class, stats, skills, and inventory panel (COMPLETE)

M5 demo gate passed. All gameplay code, editor tasks, and post-gate fixes complete.

### Fixed (2026-07-09 post-gate)
- **PlayerAnimator node paths** — `Knight/AnimationPlayer` → `CharacterRig/AnimationPlayer`; all animations were silently failing to initialize
- **Ranger T-pose** — `skeleton = NodePath("../..")` missing on all RangerMeshes children in Player.tscn; added in editor by Edu
- **Character facing** — `FaceMouseCursor()` added to `PlayerAnimator._Process`; character now rotates to face mouse cursor via horizontal-plane raycast, `Atan2(dir.X, dir.Z)` for +Z-forward KayKit model
- **WASD camera-relative** — `GetCameraRelativeInput()` in `PlayerController`; movement direction now relative to camera yaw (ADR-0025); world-space vector sent to server, no server changes needed
- **Camera zoom** — scroll-wheel zoom added to `CameraController` (range 5–30, step 2, default 14)
- **Build mode UX** — default is always combat mode; B opens build menu and enters build mode; closing menu, completing, or cancelling placement restores combat mode; C key toggle removed; `LocalState.SetCombatMode(bool)` replaces toggle-only API

### Added (2026-07-06, code-complete pass)

**Character creation**
- `CharacterCreateScreen` — roll 3d6 straight for Str/Dex/Con/Wis; pick race (Human/Dwarf/Elf/Halfling); pick class (Fighter/Ranger); Human race grants +1 to any chosen stat; Confirm disabled until all choices made; reroll clears Human bonus selection; effective stats shown with `raw → effective` notation when race changes a value
- Route: MainMenu → CharacterCreateScreen → GameWorld (was MainMenu → ClassSelectScreen → GameWorld)

**Stat foundation**
- `StatBlock` — record: Str, Dex, Con, Wis; `SkillCap(stat)` = `floor(99 × stat / 18)` (ADR-0019); `Clamped()` enforces 3–18
- `RaceData` / `RaceRegistry` — 4 races hardcoded: Human (+1 player choice), Dwarf (Con+1/Wis−1), Elf (Dex+1/Con−1), Halfling (Dex+1/Str−1); `Apply(rolled, chosenStat?)` applies modifiers and clamps
- `GameSession` — added `RolledStats (StatBlock?)`, `ChosenRaceId`, `HumanChosenStat`
- `CombatSystem._playerStats` upgraded from `(str, dex)` tuple to `StatBlock`; `RequestSetStats` RPC added; stats flow end-to-end from CharacterCreateScreen through CombatResolver

**Skill system**
- `SkillData` — record: Id, DisplayNameKey, GoverningStats[], XpPerAction, XpPerLevel, ToolTiers[]; `GetCap(StatBlock)` returns best cap across governing stats (Athletics uses max of Str/Con)
- `ToolTierData` — record: MinLevel, GrantedItemId
- `SkillRegistry` — 6 hardcoded skills: Melee (Str), Ranged (Dex), Athletics (max(Str/Con)), Woodcutting (Str), Foraging (Wis), Cooking (Wis); XpPerLevel=5, XpPerAction=1; Woodcutting level 15 → `item.tool.bronze_axe`
- `SkillSystem` — server Node; per-peer XP + class-bump tracking; `NotifyAction(peerId, skillId)` awards XP and levels up on threshold; `ApplyBump` applies class starting bonuses; `ClientApplySkillLevel` RPC pushes effective level to client
- Level formula: `effectiveLevel = min(statCap, rawXp / XpPerLevel + classBump)`. Demo gate: 75 chops = 75 XP / 5 = level 15
- Class skill bumps: Fighter Melee+5/Athletics+3, Ranger Ranged+5/Foraging+3 (replaces removed Str/Dex class fields)
- Skill XP wired into: melee hit (CombatSystem), player ranged hit (ProjectileSystem, monster shots excluded), bush harvest (BushSystem), cooking (BushSystem), tree fell (TreeSystem — placeholder `_playerXp` dict removed)
- `LocalState` — `SkillLevels` dict + `SetSkillLevel` + `SkillLevelChanged` event; `InventoryChanged` event added (fired on every server inventory snapshot)

**Inventory UI panel**
- `InventoryPanel` — CanvasLayer Layer 25; **I** key toggles, Escape closes; centred 380×480 modal; scrollable item list (`item.name × count`); item names via `Loc.T(itemId + ".name")`; refreshes on `LocalState.InventoryChanged`

**Character sheet**
- `CharacterSheet` — CanvasLayer Layer 26; **K** key toggles, Escape closes; shows race, class, effective stats (Str/Dex/Con/Wis post-race-modifiers), 6 skill rows with Level + Cap columns; cap cell turns orange at ceiling, green while room to grow; live refresh on `LocalState.SkillLevelChanged`

### Fixed (2026-07-06)
- Starting kit given twice on spawn — `PlayerInventory.ForceRemove` + `InventorySystem.ClearItem` added; `RequestSetClass` uses `ClearItem` in clear loop instead of `RemoveItems(999)` which silently failed
- Wrong `en.json` being edited — root duplicate deleted; all loc edits now go to `project/data/lang/en.json`

### Tests
- 214 tests, 0 failures
- New suites: `StatBlockTests` (7), `RaceRegistryTests` (19), `SkillRegistryTests` (10), `InventoryTests` regression (2 ForceRemove tests)

---

## [M5-code] — 2026-07-06 — Class, stats, skills, and inventory panel (code complete)

*(Superseded by M5 entry above — kept for session-log reference only.)*

All M5 gameplay code is implemented. Three editor tasks and the demo gate remain before M5 is formally closed.

### Added

**Character creation**
- `CharacterCreateScreen` — roll 3d6 straight for Str/Dex/Con/Wis; pick race (Human/Dwarf/Elf/Halfling); pick class (Fighter/Ranger); Human race grants +1 to any chosen stat; Confirm disabled until all choices made; reroll clears Human bonus selection; effective stats shown with `raw → effective` notation when race changes a value
- Route: MainMenu → CharacterCreateScreen → GameWorld (was MainMenu → ClassSelectScreen → GameWorld)

**Stat foundation**
- `StatBlock` — record: Str, Dex, Con, Wis; `SkillCap(stat)` = `floor(99 × stat / 18)` (ADR-0019); `Clamped()` enforces 3–18
- `RaceData` / `RaceRegistry` — 4 races hardcoded: Human (+1 player choice), Dwarf (Con+1/Wis−1), Elf (Dex+1/Con−1), Halfling (Dex+1/Str−1); `Apply(rolled, chosenStat?)` applies modifiers and clamps
- `GameSession` — added `RolledStats (StatBlock?)`, `ChosenRaceId`, `HumanChosenStat`
- `CombatSystem._playerStats` upgraded from `(str, dex)` tuple to `StatBlock`; `RequestSetStats` RPC added; stats flow end-to-end from CharacterCreateScreen through CombatResolver

**Skill system**
- `SkillData` — record: Id, DisplayNameKey, GoverningStats[], XpPerAction, XpPerLevel, ToolTiers[]; `GetCap(StatBlock)` returns best cap across governing stats (Athletics uses max of Str/Con)
- `ToolTierData` — record: MinLevel, GrantedItemId
- `SkillRegistry` — 6 hardcoded skills: Melee (Str), Ranged (Dex), Athletics (max(Str/Con)), Woodcutting (Str), Foraging (Wis), Cooking (Wis); XpPerLevel=5, XpPerAction=1; Woodcutting level 15 → `item.tool.bronze_axe`
- `SkillSystem` — server Node; per-peer XP + class-bump tracking; `NotifyAction(peerId, skillId)` awards XP and levels up on threshold; `ApplyBump` applies class starting bonuses; `ClientApplySkillLevel` RPC pushes effective level to client
- Level formula: `effectiveLevel = min(statCap, rawXp / XpPerLevel + classBump)`. Demo gate: 75 chops = 75 XP / 5 = level 15
- Class skill bumps: Fighter Melee+5/Athletics+3, Ranger Ranged+5/Foraging+3 (replaces removed Str/Dex class fields)
- Skill XP wired into: melee hit (CombatSystem), player ranged hit (ProjectileSystem, monster shots excluded), bush harvest (BushSystem), cooking (BushSystem), tree fell (TreeSystem — placeholder `_playerXp` dict removed)
- `LocalState` — `SkillLevels` dict + `SetSkillLevel` + `SkillLevelChanged` event; `InventoryChanged` event added (fired on every server inventory snapshot)

**Inventory UI panel**
- `InventoryPanel` — CanvasLayer Layer 25; **I** key toggles, Escape closes; centred 380×480 modal; scrollable item list (`item.name × count`); item names via `Loc.T(itemId + ".name")`; refreshes on `LocalState.InventoryChanged`

**Character sheet**
- `CharacterSheet` — CanvasLayer Layer 26; **K** key toggles, Escape closes; shows race, class, effective stats (Str/Dex/Con/Wis post-race-modifiers), 6 skill rows with Level + Cap columns; cap cell turns orange at ceiling, green while room to grow; live refresh on `LocalState.SkillLevelChanged`

### Fixed
- Starting kit given twice on spawn — `PlayerInventory.ForceRemove` + `InventorySystem.ClearItem` added; `RequestSetClass` uses `ClearItem` in clear loop instead of `RemoveItems(999)` which silently failed
- Wrong `en.json` being edited — root duplicate deleted; all loc edits now go to `project/data/lang/en.json`

### Tests
- 213 tests, 0 failures
- New suites: `StatBlockTests` (7), `RaceRegistryTests` (19), `SkillRegistryTests` (10), `InventoryTests` regression (2 ForceRemove tests)

---

## [M4] — 2026-07-05 — Combat and monsters

Two players (Fighter + Ranger) can cooperatively find and clear a bandit camp using melee and ranged combat, with full AD&D-flavored dice resolution, floating combat text, a minimap, and a death/respawn loop.

### Added

**Combat system**
- Dice-based hit resolution: 1d20 + Attack Bonus vs Target Number (combat.md §2.2). Natural 20 = critical hit (roll damage dice twice). Natural 1 = always miss.
- Stat modifier curve: `floor((stat − 10) / 4)` (combat.md §2.3) — gentler than classic AD&D to avoid double-counting stat significance alongside the skill-cap formula
- Melee: LMB swing, server-authoritative range + cooldown validation, `CombatSystem.RequestMeleeAttack`
- Ranged: shortbow with parabolic arrow physics (gravity applied each tick), mouse-aim via horizontal-plane raycast, arrow ghost orbs on clients, `ProjectileSystem`
- Shield blocking: RMB hold blocks both melee swings and incoming arrows (geometry gate — attack never reaches dice roll, combat.md §2.5)
- Block-and-attack penalty: swinging while blocking applies −3 to Attack Bonus for that roll only (combat.md §12.2)
- Damage formula: WeaponDice + StatModifier(GoverningStat). Melee → Strength; Ranged → Dexterity (combat.md §4)

**Weapons and armor catalog**
- 37 weapons and 13 armor pieces loaded from `data/base/items/` JSON (ADR-0009 stable IDs)
- `ArmorData`: ArmorValue, ArmorCategory (Light/Medium/Heavy), StrRequirement, StealthDisadvantage, ShieldBonus
- `WeaponData`: DamageDice, DamageType, IsRanged, ProjectileSpeed, AmmoItemId, SwingCooldown

**Monster AI**
- Four monster types: Wolf (beast, flat authored AB=3/TN=12/1d6), Goblin Scout, Bandit (melee humanoid), Bandit Archer (ranged, fires via `ProjectileSystem.FireFromMonster`)
- Three-state AI per monster: Idle (wander circle) → Aggro (move toward player) → Attack (melee or ranged)
- Monsters snap Y position to terrain height each tick (no sinking into hills)

**Faction allegiance system** (ADR-0024)
- `FactionService`: two-layer model — type-level defaults (MonsterNest × MonsterNest = Hostile; MonsterNest × PlayerSettlement = Hostile; etc.) and per-instance overrides
- §4 hard rule enforced in code: two PlayerSettlement factions can never be set Hostile (no PvP in v1)
- Each nest gets a unique `faction.nest.{id}` — different nests of the same species are different factions
- Projectile and aggro gates replaced ad-hoc `MONSTER_ID_THRESHOLD` patch with `FactionService.IsHostile()`

**Nests and respawn**
- Five seeded nests: 2× wolf pack (respawn 45 s), 2× goblin group (60 s), 1× bandit camp (90 s)
- Nest positions deterministic from WorldSeed; `NestGenerator` shared between server and client (same pattern as TreeGenerator)
- Respawn timer starts when the last monster in a nest dies

**Class kit selection**
- Class select screen before joining: Fighter (longsword + shield, Str=16) or Ranger (shortbow + 20 arrows, Dex=15)
- Stats flow end-to-end: `ClassKitData.Str/Dex` → `CombatSystem._playerStats` → `CombatResolver` helpers → attack/damage rolls
- `HealthSystem.RequestSetClass` RPC: remote clients announce their class on spawn, server re-distributes the correct kit and stats (fixes joining clients always receiving the host's class)

**HUD and feedback**
- `HealthHUD`: HP bar top-left
- `WeaponHUD`: bottom-centre label shows `[Build Mode]` or `[Combat · Melee · Longsword]`; C key toggles mode, Q key toggles melee/ranged
- `CombatFeedbackHUD`: floating Label3D over attacked entities — yellow number (hit), red enlarged number + `!` (crit), white "Miss", cyan "Block!"; float-up + fade Tween
- `MinimapHUD`: always-visible 150×150 top-right; terrain texture, nest dots (wolf=orange, goblin=green, bandit=red), player dot, Kingdom Marker ring, death drop red X
- `WorldMapScreen`: M key full-screen world map with legend, same data at larger scale

**Death loop**
- Combat death: inventory dropped as gold sphere pickup at death position; death drop red X on minimap/world map; player respawns at shelter with full HP, hunger, and rest
- Death drop recoverable with E key (priority pickup over all other interactions)

### Fixed
- Bandit archer arrows were invisible — shooter exclusion in `ProjectileSystem` only looked up player nodes; extended to also exclude the firing monster node
- Shield blocking had no effect against monster melee — `MonsterSystem.TickAttack` now checks `CombatSystem.IsBlocking` before the dice roll
- Floating combat text font sizes halved after in-game review (Block!=13, Miss=12, Crit=16, Normal=12)
- `BuildMenu.AddChild` during `_Ready()` caused "parent busy" error — fixed with `CallDeferred`
- Monsters sinking into terrain hills — Y position snapped to `CachedHeightmap` each AI tick

### Design decisions locked
- Combat resolution: hybrid d20 model — no persistent Armor Class stat for gear-bearing entities (combat.md §2.1)
- Beast defense: flat authored Attack Bonus + Target Number per monster definition; humanoids use live formula (combat.md §6.1, verified against original Monster Manual precedent)
- No ranged range penalty — arrow trajectory physics already makes long shots harder (combat.md §3)
- Simultaneous block-and-attack allowed with −3 Attack Bonus penalty, not a hard state-lock (combat.md §12.1)
- Faction system: per-nest not per-species; §4 no-PvP rule enforced in code (ADR-0024)

### Tests
- 170 tests, 0 failures
- New test suites: `CombatResolverTests`, `ArmorRegistryTests`, `WeaponRegistryTests`, `ClassKitRegistryTests`, `FactionServiceTests`, `HealthDataTests`, `ProjectileStateTests`

---

## [M0] — 2026-07-02 — Project scaffolded

### Added
- Godot 4.7 + C# project at `project/` (assembly: `MankersKingdoms`, target: `net8.0`)
- Client / server / shared script folder structure — architectural discipline enforced from day one
- `Loc.T(key)` localization stub in `shared/Loc.cs` — pure .NET, testable without Godot runtime (ADR-0012)
- `data/lang/en.json` — canonical English string file; `"splash.title": "Mankers Kingdoms"`
- `project/scenes/Main.tscn` — full-screen splash scene with embedded artwork
- `project/scripts/client/SplashScreen.cs` — loads Loc, inits Steam, pumps `runCallbacks()` every frame
- GodotSteam GDExtension 4.20 at `project/addons/godotsteam/` — all platforms included
- `project/tests/MankersKingdoms.Tests.csproj` — xUnit 2.9.0 skeleton; 3 Loc unit tests passing
- `.github/workflows/ci.yml` — `dotnet test` on `ubuntu-latest` + `windows-latest` on push / PR
- Git LFS patterns for binary assets: images, audio, fonts, 3D files, native DLLs

### Fixed
- Godot project was initialized in `project/Mankers Kingdoms/` (spaces in path) — moved to `project/`
- `.csproj` `RootNamespace` was `NewGameProject` — corrected to `MankersKingdoms`
- `.sln` referenced `New Game Project.csproj` — rewritten as `MankersKingdoms.sln`
- Rider run configs pointed `--path "./"` at repo root — corrected to `--path "./project/"`

### Notes
- GodotSteam `steamInitEx` returns `{"status": 0, "verbal": ""}` on success — `0` = `k_ESteamAPIInitResult_OK` (raw Steamworks SDK enum). Do not treat as failure.
- Editor play mode requires `steam_api64.dll` + `steam_appid.txt` (containing `480`) in the Godot editor executable directory.

---

## [Unreleased — pre-M0]

### Added
- PITCH.md, PRD.md, VERTICAL_SLICE.md, ARCHITECTURE.md
- docs/gdd/skills.md — skill system spec
- docs/decisions/ — 22 ADRs (ADR-0001 through ADR-0022)
- CLAUDE.md, HANDOVER.md, README.md, TODO.md, BUGS.md, IDEAS_BACKLOG.md, .gitignore

### Design decisions locked
- Working title: Mankers Kingdoms (ADR-0016)
- Engine: Godot 4 + C# + GodotSteam (ADR-0010)
- Multiplayer: authoritative host, dedicated server first-class (ADR-0002, ADR-0005)
- Skill framework: SkillSetRPG 4-group + Trades group (ADR-0011)
- Skill cap formula: `floor(99 × stat / 18)` (ADR-0019)
- Modding: Tier 1 (data) from day one (ADR-0009)
- Vertical slice: 2 players, 2 classes, 6 skills, 1 procedural village, 5 monster types
