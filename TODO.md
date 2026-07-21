# TODO — Active Task List

**One-line rule:** if it's in this file, it's actively being worked. If it's not, it's not.

New ideas go to `IDEAS_BACKLOG.md` first, get triaged, then land here if they're in scope.

---

## M0 — Project scaffolded ✅ COMPLETE (2026-07-02)

- [x] Initialize Godot 4.7 + C# project inside `/project/`
- [x] Configure `.csproj` for .NET 8+ (`RootNamespace=MankersKingdoms`)
- [x] Set up `.gitattributes` for LFS binary handling
- [x] Configure Rider workspace (run configs pointing to `./project/`)
- [x] Add GodotSteam GDExtension 4.20 via Godot AssetLib
- [x] Add xUnit test project skeleton in `/project/tests/`
- [x] Set up GitHub Actions CI (dotnet test on ubuntu + windows)
- [x] Create `data/lang/en.json` with `"splash.title": "Mankers Kingdoms"`
- [x] Create main scene with splash image + Loc system wired
- [x] Verify tests pass on Linux (CI ubuntu runner)
- [x] **M0 demo:** window opens with splash image; Steam ID confirmed on startup

---

## M1 — Main menu and two clients see each other (in progress)

**Goal:** From menu, one player hosts, other joins over LAN, both run around an empty plane and can see each other move smoothly.
**Transport:** ENet (LAN bring-up per ARCHITECTURE.md §4.2); GodotSteam SteamMultiplayerPeer replaces this in a later milestone.

### Onboarding
- [x] `tools/setup-steam-dev.ps1` — copy Steam DLL + write steam_appid.txt to Godot editor dir (Windows)
- [x] `tools/setup-steam-dev.sh` — same for Linux/macOS

### Foundation
- [x] Create `CURRENT_MILESTONE.md` for M1
- [x] Add M1 loc keys to `data/lang/en.json`
- [x] `project/scripts/shared/GameSession.cs` — session intent bridge between menu and game world
- [x] `project/scripts/shared/MsgPlayerInput.cs` — client→server movement DTO
- [x] `project/scripts/shared/MsgPlayerState.cs` — server→clients position snapshot DTO

### Networking
- [x] `project/scripts/server/NetworkManager.cs` — ENet host/join, peer lifecycle signals, reads GameSession.Intent on _Ready
- [x] `project/scripts/server/PlayerSystem.cs` — spawns/despawns Player.tscn nodes on peer connect/disconnect via RPC

### Player
- [x] `project/scripts/client/PlayerController.cs` — CharacterBody3D; WASD client prediction; sends input to server; reconciles corrections; interpolates remote players
- [x] `project/scenes/Player.tscn` — CharacterBody3D + CapsuleMesh + CapsuleShape3D + Camera3D (top-down, active only for local player)

### UI
- [x] `project/scripts/client/MainMenuController.cs` — Start Solo / Host / Join / Options / Exit; registers WASD input actions
- [x] `project/scripts/client/OptionsMenuController.cs` — master volume, graphics quality, language dropdown (EN only)
- [x] `project/scenes/MainMenu.tscn`
- [x] `project/scenes/OptionsMenu.tscn`
- [x] Update `project/scripts/client/SplashScreen.cs` — add 2-second timer → transition to MainMenu

### World
- [x] `project/scenes/GameWorld.tscn` — StaticBody3D plane + DirectionalLight3D + Players container + NetworkManager + PlayerSystem nodes

### Tests
- [x] `project/tests/Shared/MessageTests.cs` — MsgPlayerInput, MsgPlayerState, GameSession tests (8 tests, 11 total passing)

### Demo gate
- [x] M1 demo: host instance + join instance over LAN; both capsules visible; both move smoothly → mark M1 complete

---

## M1 ✅ COMPLETE (2026-07-03)

---

## M2 — World with things in it (in progress)

**Goal:** Two players explore a 64×64 procedural terrain and chop trees.
**Transport:** ENet (unchanged).

### Foundation
- [ ] `shared/WorldSeed.cs` — seed value + `CreateRandom()` factory
- [ ] `shared/TerrainConfig.cs` — generation params record with defaults
- [ ] `shared/TerrainGenerator.cs` — pure-C# value noise → `float[,]` heightmap
- [ ] `shared/TreeConfig.cs` — density, hp, yield, xp record with defaults
- [ ] `shared/TreeData.cs` — record: Id, GridX, GridZ, WorldX, WorldY, WorldZ
- [ ] `shared/TreeGenerator.cs` — heightmap + config + seed → `IReadOnlyList<TreeData>`
- [ ] `shared/MsgDayNight.cs` — DTO: WorldTimeSec, SunAngleDeg
- [ ] `GameSession.cs` — add `WorldSeed: uint` field
- [ ] `project/data/base/resources/wood.json` — item stub

### Terrain
- [ ] `server/TerrainSystem.cs` — Node: generates HeightMapShape3D collision at runtime
- [ ] `client/TerrainRenderer.cs` — Node: same heightmap → ArrayMesh visual
- [ ] `client/PlayerController.cs` — add gravity; E-key raycast → RequestChop

### Day/night
- [ ] `server/DayNightSystem.cs` — Node: advances world time, broadcasts SunAngleDeg RPC
- [ ] `client/DayNightClient.cs` — Node: rotates DirectionalLight3D, adjusts energy

### Trees
- [ ] `server/TreeSystem.cs` — Node: spawns Tree.tscn instances (all peers), manages HP,
      handles chop RPCs, broadcasts fell + wood drop events
- [ ] `client/TreeNode.cs` — StaticBody3D script: receives fell RPC, hides mesh, spawns WoodDrop
- [ ] `client/MainMenuController.cs` — register "interact" (E key) input action

### Tests
- [ ] `tests/Shared/WorldSeedTests.cs`
- [ ] `tests/Shared/TerrainGeneratorTests.cs`
- [ ] `tests/Shared/TreeGeneratorTests.cs`

### Demo gate
- [ ] M2 demo: two ENet players on procedural terrain, chop trees (E key),
      wood drops appear, Woodcutting XP logged in Output

---

## M1.5 — GodotSteam P2P transport swap ⏸ DEFERRED

**Why deferred:** GodotSteam has no official C# bindings. The only option (`LauraWebdev/GodotSteam_CSharpBindings`) is Open Beta, targets GodotSteam 4.6.1 (we're on 4.20), and has an open bug on the exact signal (`lobby_created`) the host/join flow depends on. See `docs/research/godotsteam/csharp-bindings-investigation-outcome.md`.

**Decision:** Continue M2–M4 gameplay work on ENet/LAN. Revisit once bindings mature or we evaluate a GDScript networking boundary as an ADR-level decision.

**Research preserved:** `docs/research/godotsteam/M1.5-implementation-guide.md` + class snapshots — reuse when we return to this.

---

## M3 — Settlement basics (in progress)

### Phase 2 follow-up
- [x] Show "Not enough materials" HUD message when player tries to place a building with insufficient resources. Server rejects silently now — needs a client-visible error (flash label or status bar). Wire via RPC rejection callback from SettlementSystem → client.

### Phase 3 — Needs system
- [x] `shared/LocalState.cs` — add `Hunger`, `Rest` float properties + `SetNeeds(float, float)`
- [x] `server/NeedsSystem.cs` — hunger drains in 5 min, rest in 10 min; sync every 2s; death clears inventory + respawns; `RequestSleep` RPC sets rest to 100
- [x] `client/NeedsHUD.cs` — hunger (orange) and rest (blue) ProgressBars anchored bottom-right
- [x] `client/PlayerController.cs` — `TryChopTree` → `TryInteract` (shelter check first → sleep, then tree chop); `ForceRespawn` RPC (AnyPeer)
- [x] `server/SettlementSystem.cs` — `GetRespawnPosition(peerId)` helper (marker pos or terrain origin)
- [x] **Editor:** Add `NeedsSystem` node (script: `server/NeedsSystem.cs`) to GameWorld.tscn after SettlementSystem
- [x] **Editor:** Add `NeedsHUD` CanvasLayer (script: `client/NeedsHUD.cs`) to GameWorld.tscn

### Phase 4 — Food loop
- [x] `shared/BushData.cs` — record: Index, WorldX, WorldZ, WorldY
- [x] `shared/BushGenerator.cs` — deterministic scatter placement, ~50 bushes per map
- [x] `server/BushSystem.cs` — spawns bush nodes in code (layer 16); ReceiveHarvest (+1 berry, 30s respawn); RequestCook (1 berry → 1 cooked berry); RequestEat (1 cooked berry → +40 hunger via NeedsSystem)
- [x] `server/NeedsSystem.cs` — add static Instance + RestoreHunger(peerId, amount)
- [x] `client/PlayerController.cs` — TryInteract priority: shelter > bush > cooking fire > tree; EatFood() on Tab
- [x] `client/MainMenuController.cs` — register "eat_food" → Tab
- [x] `data/lang/en.json` — berry and cooked_berry loc keys
- [x] **Editor:** Add `BushSystem` node (script: `server/BushSystem.cs`) to GameWorld.tscn after NeedsSystem

---

### Phase 5 — Food nutrition values
- [x] `shared/FoodData.cs` — record: RawItemId, CookedItemId (nullable), BaseHunger, CookMultiplier, IsToxicRaw, PoisonDuration
- [x] `shared/FoodRegistry.cs` — static registry; berry: base 10, multiplier 4× (cooked = 40)
- [x] `server/BushSystem.cs` — RequestEat: try cooked first (BaseHunger × CookMultiplier), fall back to raw (BaseHunger); use FoodRegistry
- [x] `client/PlayerController.cs` — EatFood: guard accepts cooked OR raw berry, not cooked-only
- [x] `tests/Shared/FoodRegistryTests.cs` — hunger values, cook multiplier math, fallback logic
- [x] Demo gate: Tab with raw berry → +10 hunger; Tab with cooked berry → +40 hunger

### Inventory (backend delivered; UI panel deferred)
- [x] `PlayerInventory` + `InventorySystem` + `InventoryHUD` — item→count dict backend,
      server-authoritative sync, minimal label display at top-center
- [ ] **Inventory UI panel** (slot list, `I` key to open, drag-drop) — deferred to M5
      per scope decision 2026-07-04; Phase B (shape-based grid, `docs/gdd/inventory.md`)
      deferred post-slice

## M3 ✅ COMPLETE (2026-07-04)

---

## M4 — Combat and monsters (in progress)

**Goal:** Two players (Fighter + Ranger) clear a bandit camp cooperatively.
**Transport:** ENet (unchanged).

### Phase 1 — Health & damage foundation ✅
- [x] `shared/HealthData.cs` — record: CurrentHp, MaxHp, IsDead
- [x] `shared/MsgEntityHealth.cs` — DTO: EntityId (long), CurrentHp, MaxHp
- [x] `server/HealthSystem.cs` — HP map for all entities (players + monsters); Damage/Heal/Kill; death → drop inventory + broadcast; player death → RespawnAt shelter
- [x] `client/HealthHUD.cs` — HP bar anchored top-left, fed by LocalState
- [x] `shared/LocalState.cs` — add CurrentHp, MaxHp + SetHealth(float, float)
- [x] **Editor:** Add HealthSystem node to GameWorld.tscn; add HealthHUD CanvasLayer

### Phase 2 — Weapons & melee ✅
- [x] `shared/WeaponData.cs` — record: Id, Damage, Range, SwingCooldown, IsRanged, ProjectileSpeed, AmmoItemId
- [x] `shared/WeaponRegistry.cs` — sword, shield, shortbow, hunting knife; hardcoded for now
- [x] `data/base/weapons/sword.json`, `shield.json`, `shortbow.json`, `hunting_knife.json`, `arrow.json`
- [x] `server/CombatSystem.cs` — RequestMeleeAttack RPC: validate weapon owned, cooldown, alive, distance; RequestSetBlocking; 50% flat block reduction
- [x] `client/MeleeController.cs` — LMB swing; RMB hold block; sphere query nearest target; client-side cooldown guard
- [x] `client/PlayerController.cs` — CollisionLayer |= 32u; AddChild(MeleeController)
- [x] **Editor:** Add CombatSystem node to GameWorld.tscn

### Phase 3 — Ranged combat ✅
- [x] `shared/ProjectileState.cs` — record: Id, OriginPeerId, WeaponId, flat PosX/Y/Z, VelX/Y/Z
- [x] `server/ProjectileSystem.cs` — tick projectiles (parabolic gravity), sphere collision vs entities, damage on hit, ClientSpawnArrow/ClientRemoveArrow RPC to clients
- [x] `client/BowController.cs` — mouse aim via horizontal plane raycast, LMB fire → RPC; arrow ghost sphere meshes via LocalState.ArrowSpawned/ArrowRemoved events
- [x] Arrow crafting: E key at Workbench → CombatSystem.RequestCraftArrows (3 wood → 5 arrows)
- [x] **Editor:** Add ProjectileSystem node to GameWorld.tscn; create `scenes/Arrow.tscn`

### Phase 4 — Monsters & AI ✅
- [x] `shared/MonsterData.cs` — record: Id, MaxHp, MeleeDamage, MoveSpeed, AggroRange, AttackRange, LootTable
- [x] `shared/MonsterRegistry.cs` — wolf, goblin, bandit (melee), bandit_archer (ranged)
- [x] `data/base/monsters/wolf.json`, `goblin.json`, `bandit.json`, `bandit_archer.json`
- [x] `server/MonsterSystem.cs` — AI state machine (Idle/Aggro/Attack); position broadcast RPC; death → loot drop to nearest player; bandit_archer uses ProjectileSystem.FireFromMonster
- [x] `client/MonsterNode.cs` — colour-coded CapsuleMesh; lerps toward server position; hit flash red; death hides mesh
- [x] **Editor:** Create `scenes/Monster.tscn`; add MonsterSystem node to GameWorld.tscn; add Monsters Node child to GameWorld

### Phase 4.5 — Combat mode state + weapon HUD ✅
- [x] `shared/LocalState.cs` — add `InCombatMode` bool (default false = build mode), `ToggleCombatMode()`, `CombatModeChanged` event
- [x] `client/BuildMenu.cs` — block B in combat mode, flash "You cannot build in combat mode"; subscribe to CombatModeChanged → close when switching to combat
- [x] `client/PlacementController.cs` — subscribe to CombatModeChanged → Cancel() when switching to combat
- [x] `client/BowController.cs` — only fire when `InCombatMode`
- [x] `client/MeleeController.cs` — only swing/block when `InCombatMode`
- [x] `client/WeaponHUD.cs` — CanvasLayer bottom-center; polls LocalState each frame; shows `[Build Mode]` or `[Combat · Melee · Sword]` or `[Combat · Ranged · Shortbow]`
- [x] `client/PlayerController.cs` — handle `toggle_combat` → `LocalState.ToggleCombatMode()`
- [x] `client/MainMenuController.cs` — register `toggle_combat` → key `C`
- [x] **Editor:** Add WeaponHUD CanvasLayer node (script: `client/WeaponHUD.cs`) to GameWorld.tscn

### Phase 4.6 — Minimap + world map ✅
- [x] `shared/NestGenerator.cs` — extract nest placement logic from NestSystem; Generate(uint seed) → IReadOnlyList<NestData>; both server and client call it independently (same pattern as TreeGenerator/BushGenerator)
- [x] `server/NestSystem.cs` — replace local GenerateNests() with NestGenerator.Generate()
- [x] `client/MinimapHUD.cs` — CanvasLayer (Layer 30, top-right 150×150); terrain texture baked from CachedHeightmap; player dot; nest dots; Kingdom Marker ring; death drop red X
- [x] `client/WorldMapScreen.cs` — CanvasLayer (Layer 31); M key / Escape toggles; 700×700 centred; legend; death drop red X + "DROP" label
- [x] `client/MainMenuController.cs` — register "open_map" → M key
- [x] **Editor:** MinimapHUD + WorldMapScreen CanvasLayer nodes added to GameWorld.tscn

### Phase 4.7 — Equipment catalog (schema + full data load) ✅
> Prerequisite for Phase 4.8 — establishes stable weapon IDs, DamageDice/DamageType schema, and
> ArmorData before CombatResolver is written. Full SRD 5.1 catalog loaded now per equipment.md §7
> to avoid re-authoring when post-slice classes arrive.

- [x] `shared/WeaponData.cs` — replace flat `Damage` with `DamageDice` (string), `DamageType` (string); update ID convention to `item.weapon.*`; remove `Damage`
- [x] `shared/WeaponRegistry.cs` — update to load full weapon catalog (~37 entries per equipment.md §3); update all IDs to `item.weapon.*`
- [x] `shared/ArmorData.cs` — record: Id, DisplayNameKey, ArmorValue, ArmorCategory (enum: Light/Medium/Heavy), StrRequirement (int), StealthDisadvantage (bool), ShieldBonus (int), Weight (float)
- [x] `shared/ArmorRegistry.cs` — static registry; loads all entries from `data/base/items/armor/*.json`
- [x] `data/base/items/weapons/*.json` — full catalog from equipment.md §3 (37 weapon files, stable `item.weapon.*` IDs)
- [x] `data/base/items/armor/*.json` — full catalog from equipment.md §2 (13 entries: 3 light, 5 medium, 4 heavy, 1 shield, stable `item.armor.*` IDs)
- [x] Migrate `data/base/weapons/*.json` → `data/base/items/weapons/` (old directory deleted; all 5 files superseded)
- [x] Update all code referencing old weapon IDs (`"sword"` → `"item.weapon.longsword"`, `"hunting_knife"` → `"item.weapon.dagger"`, `"shortbow"` → `"item.weapon.shortbow"`, `"arrow"` → `"item.weapon.arrow"`, `"shield"` → `"item.armor.shield"`)
- [x] `tests/Shared/ArmorRegistryTests.cs` — all 13 entries load; armor values match equipment.md §2; categories correct
- [x] `tests/Shared/WeaponRegistryTests.cs` — updated for new IDs and schema (DamageDice/DamageType)

### Phase 4.8 — Dice-based combat resolution ✅
> **Decision 2026-07-05:** Using Option A — placeholder Str=13, Dex=12 for all players in M4.
> Real stats will be wired when Phase 6 class kits land. See Phase 6 note for Option B.
> Builds on Phase 4.7's schema (DamageDice, DamageType, ArmorData already in place).

- [x] `shared/CombatResolver.cs` — `StatModifier(int stat)`, `RollDice(string notation)`, `ResolveAttack(int attackBonus, int targetNumber, string damageDice, int damageMod)` → `(bool hit, int damage)`, `PlayerAttackBonus`, `PlayerTargetNumber`, `PlayerDamageMod`; placeholder Str=13 / Dex=12 constants (Option A)
- [x] `shared/MonsterData.cs` — removed flat `MeleeDamage`; added `AttackBonus`, `TargetNumber`, `DamageDice`, `DamageType`
- [x] `shared/MonsterRegistry.cs` — authored combat stats for all 4 monsters; Wolf matches combat.md §6.2 example exactly
- [x] `data/base/monsters/*.json` — updated all 4 files (attackBonus, targetNumber, damageDice, damageType; removed meleeDamage; bandit_archer ranged_weapon_id corrected to item.weapon.shortbow)
- [x] `server/CombatSystem.cs` — block gate first (hard nullify per GDD §2.5), then ResolveAttack; GetMonsterData lookup for target TN; seeded _combatRng
- [x] `server/MonsterSystem.cs` — monster melee uses ResolveAttack vs PlayerTargetNumber(); GetMonsterData() public API; seeded _monsterRng
- [x] `server/ProjectileSystem.cs` — RollDice on confirmed physical hit; player adds Dex damageMod, monster adds 0; ParseFlatDamage removed; seeded _projectileRng
- [x] `tests/Shared/CombatResolverTests.cs` — StatModifier table (all 15 values), RollDice bounds (5 notations), ResolveAttack hit/miss/boundary, player helper values
- [x] `tests/Shared/MonsterRegistryTests.cs` — replaced MeleeDamage tests with AttackBonus/TargetNumber/DamageDice tests; Wolf stats verified against GDD example

### Phase 5 — Nests & death penalty ✅
- [x] `shared/NestData.cs` — record: Id, MonsterTypeIds[], WorldX, WorldZ, RespawnDelaySec
- [x] `server/NestSystem.cs` — seeded scatter (WorldSeed ^ 0xDEAD1234); 5 nests (2 wolf, 2 goblin, 1 bandit camp); respawn after all monsters killed
- [x] `server/MonsterSystem.cs` — SpawnMonster() public + returns ID; NestId field on MutableMonster; NestMonsterDied static event; monster loot → SpawnItemDrop instead of direct AddItem; removed hardcoded SPAWNS table
- [x] `server/HealthSystem.cs` — KillPlayer drops inventory as ItemDrop node; SpawnItemDrop() public API; RequestPickupDrop RPC; ClientSpawnItemDrop / ClientRemoveItemDrop RPCs; golden sphere visual (Layer 8 = 128u)
- [x] `client/PlayerController.cs` — E-interact mask adds 128u; Priority 0: ItemDrop pickup → RequestPickupDrop RPC
- [x] **Editor:** Add NestSystem node to GameWorld.tscn (after MonsterSystem); `scenes/MonsterNest.tscn` visual deferred

### Phase 6 — Class kit selection ✅ (code complete; editor task pending)
> **Option B note (2026-07-05):** Phase 6 could precede Phase 4.8 to give dice resolution real player stats from the start instead of placeholders. Chosen not to block on this — revisit when wiring real stats into `CombatResolver`.

- [x] `shared/ClassKitData.cs` — record: ClassId, DisplayNameKey, ClassKitItem[] StartingItems (ClassKitItem = top-level record, not nested — avoids C# primary-constructor scope issue)
- [x] `shared/ClassKitRegistry.cs` — Fighter (longsword + shield) and Ranger (shortbow + 20 arrows) kits
- [x] `shared/GameSession.cs` — ChosenClassId field added; defaults to "class.fighter"; Reset() clears it
- [x] `client/ClassSelectScreen.cs` — two-button picker; TooltipText shows class description; sets ChosenClassId → transitions to GameWorld
- [x] `client/MainMenuController.cs` — all three paths (Solo/Host/Join) now route through ClassSelectScreen.tscn
- [x] `server/HealthSystem.cs` — debug kit removed; replaced with ClassKitRegistry.Find(ChosenClassId) loop; falls back to Fighter if classId unrecognised
- [x] `data/lang/en.json` — class.select.title/subtitle, class.fighter/ranger .name/.desc
- [x] `tests/Shared/ClassKitRegistryTests.cs` — 14 tests: catalog count, item IDs, Fighter has longsword+shield not bow, Ranger has bow+arrows not sword, default classId recognised
- [x] **Editor:** Create `scenes/ClassSelectScreen.tscn` — Control + VBoxContainer + %TitleLabel + %SubtitleLabel + HBoxContainer with %FighterButton + %RangerButton; attach script client/ClassSelectScreen.cs

### Floating combat text ✅ (code complete; editor task pending)
- [x] `shared/CombatResolver.cs` — ResolveAttack return extended to `(bool hit, int damage, bool isCrit)`; isCrit = natural 20
- [x] `client/CombatFeedbackHUD.cs` — spawns Label3D above attacked entity; float+fade Tween; miss=white "Miss", hit=yellow number, crit=red "N!" larger; ShowCombatResult RPC (Authority, CallLocal=true)
- [x] `server/CombatSystem.cs`, `MonsterSystem.cs`, `ProjectileSystem.cs` — each calls `GetNodeOrNull<Node>(COMBAT_FEEDBACK_PATH)?.Rpc("ShowCombatResult", pos, hit, dmg, isCrit)` after resolving attack; string-based Rpc avoids Client import in server scripts
- [x] **Editor:** Add a plain `Node` to GameWorld.tscn, rename it exactly `CombatFeedbackHUD`, attach script `client/CombatFeedbackHUD.cs`; no special position needed

### Faction allegiance system (ADR-0024) ✅
- [x] `shared/FactionType.cs`, `shared/FactionRelationship.cs`, `shared/FactionService.cs` — two-layer model, §4 hard rule, Reset() for test isolation
- [x] `shared/NestData.cs` — FactionId field added
- [x] `server/NestSystem.cs` — registers factions at startup, passes FactionId to SpawnMonster
- [x] `server/MonsterSystem.cs` — FactionId on MutableMonster; aggro gate; GetMonsterFactionId() API
- [x] `server/ProjectileSystem.cs` — faction gate replaces MONSTER_ID_THRESHOLD patch
- [x] `tests/Shared/FactionServiceTests.cs` — 16 tests
- [x] `docs/decisions/ADR-0024-faction-allegiance-system.md`

### GDD §12 — Block-and-attack penalty + shield vs projectiles ✅
- [x] `docs/gdd/combat.md` §12 appended (locked design)
- [x] `server/CombatSystem.cs` — `if (IsBlocking(sender)) attackBonus -= 3` before ResolveAttack
- [x] `server/ProjectileSystem.cs` — IsBlocking gate; blocked arrows show "Block!" and are removed
- [x] `tests/Shared/CombatResolverTests.cs` — ResolveAttack_BlockPenalty_ReducesHitRate

### Tests
- [x] `tests/Shared/WeaponRegistryTests.cs`
- [x] `tests/Shared/MonsterRegistryTests.cs`
- [x] `tests/Shared/HealthDataTests.cs`
- [x] `tests/Shared/ProjectileStateTests.cs`
- [x] `tests/Shared/FactionServiceTests.cs`

### Demo gate ✅ PASSED (2026-07-05)
- [x] M4 demo: two ENet players, one Fighter (sword + shield), one Ranger (shortbow + arrows); find bandit camp from nest placement; clear it cooperatively; both combat styles functional; death drops inventory, player respawns at shelter
- [x] Verify: block reduces hit rate when swinging while guarding (−3 AB penalty)
- [x] Verify: RMB shield intercepts both melee and arrow attacks

---

## M4 ✅ COMPLETE (2026-07-05)

---

## M5 — Class, stats, skills, and inventory panel (in progress)

**Goal:** Ranger player rolls stats, picks race, enters world, chops wood, watches Woodcutting level up, unlocks bronze axe at level 15, hits stat ceiling and stops.

### Phase 1 — Stat foundation + race system ✅
- [x] `shared/StatBlock.cs` — record: Str, Dex, Con, Wis; `SkillCap(int stat)` = floor(99×stat/18)
- [x] `shared/RaceData.cs` — record: Id, DisplayNameKey, stat modifier dict, PlayerChoiceModifier; Apply() clamps to 3–18
- [x] `shared/RaceRegistry.cs` — 4 races hardcoded (Human/Dwarf/Elf/Halfling); Find(id)
- [x] `data/base/races/` — not needed (registry is hardcoded for v1, same as ClassKitRegistry)
- [x] `shared/GameSession.cs` — add RolledStats (StatBlock?), ChosenRaceId, HumanChosenStat
- [x] `server/CombatSystem.cs` — upgrade `_playerStats` from `(str,dex)` to StatBlock; `RequestSetStats` RPC
- [x] `shared/ClassKitData.cs` — remove Str/Dex (now rolled); add ClassSkillBump record + SkillBumps[]
- [x] `shared/ClassKitRegistry.cs` — Fighter: Melee+5, Athletics+3; Ranger: Ranged+5, Foraging+3
- [x] `client/PlayerController.cs` — AnnounceStats() deferred call alongside AnnounceClass()
- [x] `data/lang/en.json` — race loc keys + charCreate loc keys
- [x] `tests/Shared/StatBlockTests.cs` — 7 tests
- [x] `tests/Shared/RaceRegistryTests.cs` — 19 tests (incl. ClassKitRegistry SkillBumps regression)

### Phase 2 — Skill system ✅
- [x] `shared/ToolTierData.cs` — record: MinLevel, GrantedItemId
- [x] `shared/SkillData.cs` — record: Id, DisplayNameKey, GoverningStats[], XpPerAction, XpPerLevel, ToolTiers[]
- [x] `shared/SkillRegistry.cs` — 6 hardcoded skills (melee/ranged/athletics/woodcutting/foraging/cooking)
- [x] `shared/LocalState.cs` — added SkillLevels dict + SetSkillLevel + SkillLevelChanged event
- [x] `server/SkillSystem.cs` — per-peer XP+bump tracking; NotifyAction; level-up with stat cap; tool tier grants; broadcasts level to client
- [x] Wire triggers: CombatSystem melee hit → skill.melee; ProjectileSystem ranged hit → skill.ranged; BushSystem harvest → skill.foraging; BushSystem cook → skill.cooking; TreeSystem fell → skill.woodcutting
- [x] `server/HealthSystem.cs` — ApplyBump called in RequestSetClass after kit distribution
- [x] `data/lang/en.json` — skill loc keys + item.tool.bronze_axe
- [x] `tests/Shared/SkillRegistryTests.cs` — 10 tests (213 total, 0 failures)

### Phase 3 — Inventory UI panel ✅
- [x] `client/InventoryPanel.cs` — CanvasLayer Layer=25; I key toggle, Escape closes; centred modal panel; scrollable item list from LocalState.Inventory; refreshes on LocalState.InventoryChanged event
- [x] `shared/LocalState.cs` — InventoryChanged event added (fired in SetInventory)
- [x] `client/MainMenuController.cs` — registered "open_inventory" → Key.I
- [x] `data/lang/en.json` — inventory.title/empty/close_hint; resource.wood.name
- [x] **Editor:** Add InventoryPanel CanvasLayer node to GameWorld.tscn; attach script `res://scripts/client/InventoryPanel.cs`

### Phase 4 — Character sheet UI ✅
- [x] `client/CharacterSheet.cs` — CanvasLayer Layer=26; K key toggle, Escape closes; race/class line; Str/Dex/Con/Wis (race-modified); 6 skill rows with Level + Cap columns; cap cell turns orange when at cap; live refresh on LocalState.SkillLevelChanged
- [x] `client/MainMenuController.cs` — registered "char_sheet" → Key.K
- [x] `data/lang/en.json` — charSheet.* loc keys
- [x] **Editor:** Add CharacterSheet CanvasLayer to GameWorld.tscn; attach script `res://scripts/client/CharacterSheet.cs`

### Phase 5 — Character creation screen
- [x] `client/CharacterCreateScreen.cs` — rolls 3d6×4; race picker (Human/Dwarf/Elf/Halfling applies modifier); class picker; reroll button; confirm → GameWorld
- [x] `scenes/CharacterCreateScreen.tscn` — **editor task** (see editor instructions below)
- [x] `client/MainMenuController.cs` — route Solo/Host/Join through CharacterCreateScreen instead of ClassSelectScreen
- [x] `data/lang/en.json` — all character creation loc keys present from Phase 1

### GDD §13 — Ranged resolution asymmetry ✅
- [x] `docs/gdd/combat.md` §13 appended (locked): physical contact = automatic hit; d20+AB → normal/crit only
- [x] `shared/CombatResolver.cs` — `PlayerAttackBonus` gains `skillLevel` param; formula: `skillLevel/10 + StatModifier`
- [x] `server/SkillSystem.cs` — `GetSkillLevel(long peerId, string skillId)` public method
- [x] `server/CombatSystem.cs` — melee AB uses real `skill.melee` level via `GetSkillLevel`
- [x] `server/ProjectileSystem.cs` — ranged hit: inline d20+AB; nat20 = crit; double dice; `isCrit` to `ShowCombatResult`; no hit/miss gate
- [x] `tests/Shared/CombatResolverTests.cs` — updated callers + `PlayerAttackBonus_SkillLevel_ContributesFloorDiv10`

### KayKit animation system ✅
- [x] `shared/LocalState.cs` — `DamageTaken`, `PlayerDied`, `PlayerRevived`, `LocalArrowFired` events; `SetHealth` updated
- [x] `client/BowController.cs` — `NotifyLocalArrowFired()` after fire RPC dispatch
- [x] `client/PlayerController.cs` — `AddChild(new PlayerAnimator())` in local-player `_Ready()`
- [x] `client/PlayerAnimator.cs` — state machine: Idle_A, Walking_A/B/C, Running_A/B, Jump sequence, Hit_A/B, Death_A/B, Throw; `ApplyCharacterMesh()` for Knight/Ranger visibility
- [x] **Editor (Edu):** deleted old capsule MeshInstance3D; wrapped Knight meshes into `KnightMeshes`; added `RangerMeshes` (Visible=false)
- [x] **Editor:** confirm `Death_A` and `Death_B` loop mode = `None` in AnimationPlayer

### Demo gate
- [x] Player creates Ranger (any race), enters world, chops 75 trees, watches Woodcutting reach level 15, receives bronze axe, stat ceiling stops further progress

### Post-gate fixes (2026-07-09)
- [x] `client/PlayerAnimator.cs` — node paths corrected (`Knight/` → `CharacterRig/`); animations were not playing at all
- [x] `client/PlayerAnimator.cs` — `FaceMouseCursor()` added; character now rotates to face mouse cursor each frame
- [x] `client/PlayerController.cs` — `GetCameraRelativeInput()` added; WASD now moves relative to camera yaw (ADR-0025)
- [x] `client/CameraController.cs` — scroll-wheel zoom added (5–30 units, step 2, default 14)
- [x] `client/BuildMenu.cs`, `client/PlacementController.cs`, `shared/LocalState.cs` — build mode redesign: default always combat; B opens build menu (enters build mode); close/place/cancel restores combat; C key toggle removed
- [x] **Editor (Edu):** Ranger mesh `skeleton = NodePath("../..")` set on all RangerMeshes children in Player.tscn

---

## M5 ✅ COMPLETE (2026-07-09)

---

## M6 — Village and recruitment

**Goal:** Player travels to a procedural village, recruits a high-Str villager, brings them
home, assigns to Woodcutter's Post — NPC chops trees, wood lands in settlement stockpile,
player takes it via Kingdom Marker.

### Phase 1 — Village generation and NPC spawn ✅
- [x] `shared/VillagerData.cs` — record: Id, Name, Stats (StatBlock); ArchetypeTag computed from highest stat
- [x] `shared/VillageData.cs` — record: Id, WorldX, WorldZ, VillagerIds (string[])
- [x] `shared/VillageGenerator.cs` — seeded; 1 village; 6–10 villagers; each stat = best-of-three 3d6; archetype derived from highest stat
- [x] `data/base/villages/names.json` — 30 name pool; drawn by seeded index
- [x] `server/VillageSystem.cs` — Node; spawns VillagerNode instances; holds mutable NPC state (SortedDictionary)
- [x] `client/VillagerNode.cs` — teal capsule + name Label3D above head; collision layer 256u
- [x] `data/lang/en.json` — archetype.*.name + village.title loc keys
- [x] `tests/Shared/VillageGeneratorTests.cs` — 16 tests: 1 village placed; 6–10 villagers; stats in 3–18; archetype = highest stat; no duplicate names; determinism (230 total, 0 failures)
- [x] **Editor:** Add `VillageSystem` node to `GameWorld.tscn`; create `scenes/VillagerNode.tscn` (CharacterBody3D + CapsuleMesh + CapsuleShape3D + Label3D)

### Phase 2 — Recruitment dialogue and follow state ✅
- [x] `client/RecruitmentDialogue.cs` — CanvasLayer Layer=28; E key near villager; name + archetype + stats (highest stat gold ★); Recruit / Leave buttons; Escape closes
- [x] `shared/LocalState.cs` — `FollowerNpcId` + `SetFollower` + `ClearFollower` + `FollowerChanged` event
- [x] `server/VillageSystem.cs` — `RequestRecruit` / `RequestLeave` RPCs; proximity check (3m); _followTargets + _followerByPeer dicts; _PhysicsProcess follow movement (3 m/s, stops 2m); `ClientMoveVillager` position broadcast
- [x] `client/PlayerController.cs` — 256u added to interact mask; villager Priority 1 (above shelter); dialogue guard on E key
- [x] `data/lang/en.json` — `recruit.*` loc keys (230 tests, 0 failures)
- [x] **Editor:** Add `RecruitmentDialogue` CanvasLayer to `GameWorld.tscn`

### Phase 3 — Woodcutter's Post, settlement stockpile, NPC job loop ✅
- [x] `shared/BuildingRegistry.cs` — added `WoodcuttersPost` (id `"building.woodcutters_post"`, 15 wood, in All list)
- [x] `server/SettlementSystem.cs` — `_stockpile` SortedDictionary; `AddToStockpile`; `RequestTakeFromStockpile` RPC; `ClientUpdateStockpile` (JSON) → LocalState; `SpawnMarker` now calls `LocalState.SetMarkerWorldPos`
- [x] `shared/LocalState.cs` — `StockpileSnapshot` + `SetStockpile(json)` + `StockpileChanged`; `MarkerWorldPos` + `SetMarkerWorldPos`
- [x] `client/StockpilePanel.cs` — CanvasLayer Layer=29; E key near Kingdom Marker (3m); item list; "Take All"; subscribes to `StockpileChanged`
- [x] `server/TreeSystem.cs` — `ServerChopTree(treeId)` routes wood to stockpile (no SkillSystem); `GetAvailableTreeIds()` public API
- [x] `server/VillageSystem.cs` — `RequestAssignToStation` RPC; Following→Working state transition; job tick: `FindNearestTree` (20m), move to tree, `ServerChopTree` (1s cooldown via elapsed time); `MoveNpcToward` helper shared by follow + job ticks
- [x] `client/PlayerController.cs` — Kingdom Marker proximity check (pre-sphere); Woodcutter's Post + has follower → assign (Priority 2); `TryAssignFollowerToStation` helper; `StockpilePanel` path constant
- [x] `data/lang/en.json` — `building.woodcutters_post.name` + `stockpile.*` loc keys (230 tests, 0 failures)
- [x] **Editor:** Create `scenes/WoodcuttersPost.tscn`; Add `StockpilePanel` CanvasLayer to `GameWorld.tscn`

### Phase 4 — NPC needs ✅
- [x] `server/VillageSystem.cs` — per-NPC hunger + rest (SortedDictionary<string,float>); hunger drains 0.5/min, rest drains 1/min; rest < 20 → suspend job/follow, walk to nearest Shelter, sleep 30s (rest → 100); hunger restores passively 0.5/s

### Demo gate ✅
- [x] Player travels to village → recruits high-Str villager → NPC follows home → build Woodcutter's Post (15 wood) → E key assigns NPC → NPC chops trees autonomously → wood appears in stockpile → player takes it via Kingdom Marker

---

## M6 ✅ COMPLETE (2026-07-10)

---

## M6.5 — NPC haul loop + Stockpile building

**Goal:** Woodcutter NPC carries wood to a physical Stockpile Drop building instead of
teleporting it to the abstract stockpile. More satisfying and visible.

### Tasks
- [x] `server/TreeSystem.cs` — `ServerChopTree` returns `int` wood yielded (0 until tree felled); `FellTreeForNpc` no longer calls `AddToStockpile` directly
- [x] `server/VillageSystem.cs` — `_npcCarried` dict; `_walkingToDeposit` dict; `NPC_CARRY_CAPACITY = 6`; `TickDeposit`; `FindNearestStockpile`; updated `TickJobs`; carry check on wake
- [x] `shared/BuildingRegistry.cs` — add `StockpileDrop` (`building.stockpile`, 8 wood, `Stockpile.tscn`)
- [x] `server/SettlementSystem.cs` — null guard on `GD.Load<PackedScene>` in `SpawnBuilding`
- [x] `data/lang/en.json` — `building.stockpile.name`
- [x] **Editor (Edu):** Create `scenes/Stockpile.tscn` — Node3D root + BoxMesh (3×1.5×3) + StaticBody3D + BoxShape3D on collision layer 8 (buildings)

### Demo gate ✅ PASSED (2026-07-13)
- [x] Build Stockpile Drop near Woodcutter's Post → assign NPC → NPC chops 2 trees, walks to Stockpile Drop, deposits wood → stockpile count increments → NPC returns to chop

---

## M7.5a — Hotbar (9 quick-access slots)

**Goal:** Player opens inventory, hovers an item, presses 1–9 to assign it to a hotbar slot. Hotbar is always visible at screen bottom. Active slot highlighted with 1–9 keys. Prerequisite for M7 bandage use-from-hotbar.

### Code (all complete)
- [x] `shared/PlayerInventory.cs` — `HotbarSlots string?[9]`; `SetHotbarSlot(int, string?)`; `GetHotbarSlot(int)`
- [x] `shared/LocalState.cs` — `ActiveHotbarSlot`; `HotbarSlotChanged`; `ActiveHotbarSlotChanged`; `HotbarKeyPressed`; `SetHotbarSlot`; `SetActiveHotbarSlot`; `NotifyHotbarKeyPressed`
- [x] `server/InventorySystem.cs` — `RequestAssignHotbar(int slot, string itemId)` AnyPeer RPC; `ApplyHotbarSlot` Authority RPC; `SyncHotbarTo` helper
- [x] `client/HotbarHUD.cs` — CanvasLayer Layer=5; 9-slot bar bottom-centre; number keys 1–9 select active slot; slot label shows item name; yellow highlight on active slot
- [x] `client/InventoryPanel.cs` — hover tracking per item row; `HotbarKeyPressed` handler assigns hovered item; hotbar badge `[N]` on assigned rows; hotbar hint in footer
- [x] `data/lang/en.json` — `inventory.hotbar_hint`

### Editor tasks
- [x] **Editor (Edu):** Add `HotbarHUD` CanvasLayer node to `GameWorld.tscn`; attach script `res://scripts/client/HotbarHUD.cs`

### Demo gate
- [x] Open inventory (I), hover Wood row, press 3 → `[3]` badge appears on Wood row, slot 3 in HotbarHUD shows "Wood"; close inventory; press 3 → slot 3 highlighted
- [x] Hover a different item and press 3 again → slot 3 overwrites to new item (verified 2026-07-10)

---

## M7 — Class-gated building

**Goal:** Fighter alone can't build Herbalist's Hut → recruits Ranger villager → hut becomes buildable → Ranger leaves → hut becomes non-functional (dormant).

### Phase 1 — Herbalist's Hut building ✅
- [x] `shared/BuildingRegistry.cs` — `HerbalistsHut` added (id `"building.herbalists_hut"`, 20 wood, 4×3×4); `RequiresPresence` removed from all buildings
- [x] `server/SettlementSystem.cs` — `RequestCraftBandage` RPC (2 herbs → 1 bandage; no gate)
- [x] `client/BuildMenu.cs` — gate label/button system removed; panel simplified

### Phase 2 — Forager NPC job loop ✅
- [x] `server/VillageSystem.cs` — `_settlementNpcs` SortedSet; `TickForagerJob` (30s, herb → stockpile); `RequestAssignNpcToStation` (shelter-based, no proximity); `RequestUnassignNpc`; `BroadcastVillageRoster` + `ClientSetVillageRoster`
- [x] `shared/LocalState.cs` — `VillageRosterJson` + `VillageRosterChanged` event
- [x] `data/lang/en.json` — `item.herb.*` keys

### Phase 3 — Bandage crafting + use ✅
- [x] `server/HealthSystem.cs` — `RequestUseBandage` RPC (heal 20 + floor(foraging/5), cap 40)
- [x] `client/PlayerController.cs` — Tab with bandage in active hotbar → `RequestUseBandage`; E at Herbalist's Hut → `RequestCraftBandage`; E at Shelter (founder) → `BuildingAssignmentPanel.Open()`
- [x] `client/BuildingAssignmentPanel.cs` — new CanvasLayer Layer=31; NPC list + station list; Assign/Unassign; subscribes to `VillageRosterChanged`
- [x] `data/lang/en.json` — `item.bandage.*` keys
- [x] **Editor (Edu):** Add `BuildingAssignmentPanel` CanvasLayer to `GameWorld.tscn`; attach `res://scripts/client/BuildingAssignmentPanel.cs`

### Demo gate ✅ PASSED (2026-07-11)
- [x] Recruit a Wis-high villager (forager archetype) → assign via BuildingAssignmentPanel → build Herbalist's Hut → assign forager NPC to it → herbs appear in stockpile → E at Hut → craft bandage → assign bandage to hotbar → Tab to use → HP restored

---

## M7 ✅ COMPLETE (2026-07-11)

---

## M8 ✅ COMPLETE (2026-07-15)

## M8 — Save/load and polish

**Goal:** Play for 30 minutes, quit, restart, resume exactly where you left off.

### Phase 1 — Save schema + core systems ✅
- [x] `shared/SaveData.cs` — v1 schema (WorldSeed, Markers, Buildings, Stockpile, NpcAssignments, Players); version field for future migrations
- [x] `server/SaveSystem.cs` — `Save()` + `TryLoad()` + 5-minute autosave + exit save + reconnect replay (`SendFullStateToClient`)
- [x] `server/SettlementSystem.cs` — `_placedBuildings` tracking in `SpawnBuilding`; `GetSaveState()` + `RestoreFromSave()`
- [x] `server/SkillSystem.cs` — `GetXpForSave()` + `GetBumpsForSave()` + `RestoreSkillsFromSave()` + `BroadcastLevelsTo()`
- [x] `server/HealthSystem.cs` — `GetPlayerIds()` + `RestoreHpFromSave()` + `SyncHealthTo()`
- [x] `server/NeedsSystem.cs` — `GetNeeds()` + `RestoreNeedsFromSave()`
- [x] `server/InventorySystem.cs` — `RestoreInventoryFromSave()` + `SyncInventoryAndHotbarTo()`
- [x] `server/VillageSystem.cs` — `GetAssignmentsForSave()` + `RestoreAssignmentsFromSave()` + `BroadcastRosterToAll()`

### Phase 2 — Wire-up + editor task ✅
- [x] `tests/Shared/SaveDataTests.cs` — 13 round-trip + schema tests (270 total, 0 failures)
- [x] **Editor (Edu):** Add `SaveSystem` node to `GameWorld.tscn` — last child of root after VillageSystem
- [x] Smoke-test save→quit→restart: kingdom marker, 2+ buildings, stockpile items, skill level, bandage in hotbar all survive, felled trees stay felled ✅

### Phase 2b — Named saves + Load Game UI ✅ (code only; editor task pending)
- [x] `shared/SaveUtil.cs` — `ListSaves()` + `PeekSession()` read-only helpers (client-safe; no Server import needed)
- [x] `shared/GameSession.cs` — `SaveName` field; `SaveRequested` event + `RequestSave()` (bridges PauseMenu → SaveSystem without cross-namespace import)
- [x] `shared/SaveData.cs` — `SessionSave` class (class/race/stats) embedded in save; `Session` field on `SaveData`
- [x] `server/SaveSystem.cs` — `SavePath` property uses `GameSession.SaveName`; `EnsureSaveDir()`; saves and restores `data.Session`; subscribes `GameSession.SaveRequested`
- [x] `client/CharacterCreateScreen.cs` — `OnConfirm()` stamps `GameSession.SaveName = "save_{yyyyMMdd_HHmmss}"`
- [x] `client/LoadGamePanel.cs` — programmatic Control (no .tscn); lists saves via `SaveUtil.ListSaves()`; shows class/race from `PeekSession()`; sets `GameSession` on load; auto-selects newest save on open; double-click to load
- [x] `client/PauseMenu.cs` — CanvasLayer Layer=50; Escape toggle; Resume/Save/Load/Quit buttons; embeds `LoadGamePanel`; uses `GameSession.RequestSave()` (no Server import)
- [x] `client/MainMenuController.cs` — injects "Load Game" button after StartSolo; creates `LoadGamePanel` overlay
- [x] `data/lang/en.json` — `menu.load_game`, `pause.*`, `load_panel.*` loc keys
- [x] **Editor (Edu):** Add `PauseMenu` CanvasLayer to `GameWorld.tscn` — done 2026-07-15
- [x] Smoke-test Load Game: new game → play → save (Escape → Save) → quit to menu → Load Game → select slot → resumes in same world with same state

### Phase 2c — Equipment slots (inventory.md §10) ✅
> Wires armor and shield values into combat TN per combat.md §2.2; auto-equips class kit gear; adds equip UI to CharacterSheet.
- [x] `shared/EquipSlot.cs` — enum: MainHand=0, OffHand=1, BodyArmor=2
- [x] `shared/PlayerInventory.cs` — three nullable equipped fields + `GetEquipped`/`SetEquipped`/`ClearEquippedSlotsFor`; `Clear()` resets equipped slots
- [x] `shared/SaveData.cs` — `PlayerSave.EquippedMainHand/OffHand/BodyArmor` (nullable, additive — no version bump)
- [x] `shared/LocalState.cs` — `EquippedMainHand/OffHand/BodyArmor`; `GetEquipped`/`SetEquipped`; `EquippedSlotChanged` event
- [x] `shared/CombatResolver.cs` — `PlayerTargetNumber` gains optional `armorValue`, `shieldBonus`, `armorCategory` params; ArmorCategory.Medium caps Dex mod at +1, Heavy zeroes it
- [x] `server/InventorySystem.cs` — `RemoveItems`/`ClearItem`/`TakeAll` evict equipped slots; `RestoreInventoryFromSave` restores equipped slots; `SyncInventoryAndHotbarTo` now syncs equipped too; `EquipItem` server API; `RequestEquipItem` AnyPeer RPC; `SyncEquippedSlotsTo` + `ApplyEquippedSlot` Authority RPC
- [x] `server/CombatSystem.cs` — `GetPlayerTargetNumber` reads armor+shield from equipped slots; `RequestSetBlocking` validates shield in OffHand; block gate in `RequestMeleeAttack` uses equipped slot check
- [x] `server/HealthSystem.cs` — `AutoEquipKitItems` infers slot from WeaponRegistry/ArmorRegistry; called on connect + class change
- [x] `server/SaveSystem.cs` — saves and restores equipped slots per player
- [x] `client/CharacterSheet.cs` — Equipment section (3 slot rows with buttons) between Stats and Skills; `BuildPicker()` creates persistent floating picker; `OpenPicker(slot)` populates with compatible items + Unequip; `OnPickerItemSelected` sends `RequestEquipItem` RPC; `OnEquippedSlotChanged` refreshes button labels; Escape closes picker before closing sheet
- [x] `data/lang/en.json` — `charSheet.equipment`, `charSheet.slot.*` loc keys
- [x] `tests/Shared/InventoryTests.cs` — 6 new equipment slot tests
- [x] `tests/Shared/CombatResolverTests.cs` — 6 new `PlayerTargetNumber` armor/shield/category tests

### Phase 3 — Remaining M8 scope
- [x] **ESC as game menu fallback (partial)** — `BuildMenu` handles `ui_cancel`; all other panels have `if (!Visible) return;` guards; PauseMenu (Layer 50) catches Escape when nothing else is open
- [x] **ESC overlap fix** — `AnyGamePanelVisible()` in PauseMenu; checks 7 panel paths; returns WITHOUT `SetInputAsHandled()` when a panel is open so the panel's own handler fires
- [x] **CharacterSheet StyleBoxFlat polish** — dark parchment panel, antique gold border/separators, inset slot buttons with hover/pressed states, gold section headers; palette in `COL_*` constants at class top
- [x] **Buff/debuff system** — `shared/BuffStat, BuffAmountType, ActiveBuff, BuffCalculator, CritEffect, FumbleEffect`; `server/BuffSystem`; hooked into CombatSystem (stun/disarm gates, AB debuff, armor debuff, crit/fumble effect application), HealthSystem (vulnerability multiplier, clear on death), MonsterSystem (stun gate, monster crit/fumble symmetry §5.2), ProjectileSystem (stun/disarm gates, AB debuff, crit effect on hit)
- [x] **Delete save button** — `LoadGamePanel`: Delete button alongside Load/Cancel; `DirAccess` removes `user://saves/{name}.json`; `load_panel.delete` loc key
- [x] **Fog of war** — `shared/FogOfWarData` (64×64 byte grid, UNSEEN/SEEN/VISIBLE, 40m vision radius); `server/FogSystem` (1.5s update, shared reveal, Base64 RPC broadcast); `LocalState.FogSnapshot + FogChanged`; `SaveData.FogBase64`; `WorldMapScreen` fog overlay (solid black/dark grey/transparent per state); `SaveSystem` save+restore+late-connect push
- [x] **Localization audit** — 19 new loc keys; fixed `BuildingAssignmentPanel` (6 strings), `BuildMenu` (4 strings), `CombatFeedbackHUD` (2 strings), `WorldMapScreen` (9 strings); 0 hardcoded player-facing strings remain
- [x] Demo gate: play 30 min solo → quit → restart → resume exactly where left off ✅ PASSED (2026-07-15)

### Captured (scope TBD)
- [ ] Building construction progress — visual indicator while a building is being placed/constructed
- [ ] NPC collision with buildings — NPCs currently walk through placed buildings

---

## M9 — Vertical slice playtest

**Goal:** Play a real 30–60 min session with a friend. Log what breaks, confuses, or feels bad. Fix P0/P1 bugs only.

### Pre-playtest content fixes (2026-07-15) ✅ code complete
- [x] **WorldSeed randomisation** — `CharacterCreateScreen.OnConfirm()` sets `GameSession.WorldSeed = (uint)GD.Randi()`. Every new game now produces a unique world.
- [x] **Sickle at Foraging 15** — `SkillRegistry` Foraging ToolTiers grants `item.tool.sickle`; `en.json` name/desc added.
- [x] **Stew Pot at Cooking 10** — `SkillRegistry` Cooking ToolTiers grants `item.tool.stew_pot`; `en.json` name/desc added.
- [x] **Wooden Wall + Gate** — `BuildingRegistry` adds `WoodenWall` (5 wood) and `WoodenGate` (10 wood); `en.json` keys added. Scenes pending (see editor tasks below).
- [x] **Herb patches (HerbGenerator + HerbSystem)** — 30 deterministic herb patch nodes (purple spheres) with 60s harvest cooldown; server NPC API exposed.
- [x] **Forager NPC movement loop** — replaced static 30s timer with woodcutter-style movement: NPC walks to nearest herb patch or berry bush, harvests, carries (cap 6), walks to stockpile, deposits herbs + berries separately. BushSystem gained NPC harvest API.

### Block system redesign (2026-07-18) ✅ code complete
- [x] **Mutual exclusivity (block XOR attack)** — `MeleeController` LMB guard; `BowController.TryFireBow()` guard; `LocalState.IsBlocking` bridge property; server gates in `RequestMeleeAttack` (replaces §2.5 hard-negation) and `RequestFireProjectile` (new gate after disarm check)
- [x] **Active block TN bonus +4** — `CombatSystem.GetPlayerTargetNumber`: adds +4 when `IsBlocking(peerId) && shieldBonus > 0`; shield re-verified at hit time. Orc vs Fighter: 70% → 50% hit chance while blocking.
- [x] **Remove §12 penalty** — `if (IsBlocking(sender)) attackBonus -= 3` removed from `RequestMeleeAttack`
- [x] **Ranged crit threshold** — `BLOCKING_CRIT_THRESHOLD = 24` in ProjectileSystem; `isCrit = tgtBlocking ? (roll==20 && rollTotal>=24) : (roll==20)`; Bandit Archer crit vs blocking: 5% → 0%
- [x] **combat.md §15** — full rule text; §2.5 and §12 marked superseded; intentional melee/ranged asymmetry documented in §15.3
- [x] **IDEAS_BACKLOG** — [post-slice] entry: BLOCKING_CRIT_THRESHOLD coincidental to Bandit Archer AB 3; revisit for any AB ≥ 4 ranged enemy

### Pre-playtest HP + encounter fixes (2026-07-17) ✅ code complete
- [x] **Fog of war on minimap** — `MinimapHUD` full fog overlay; `FogOfWarData.IsDiscovered` gates nest visibility on both maps. BUGS.md [P1] closed.
- [x] **Orc elite monster** — `MonsterRegistry` new entry (18 HD d8, Con 16, 99 HP, AB 5, TN 14, 1d10 slashing); `en.json` name/desc.
- [x] **Nest tiering** — `NestTier` enum (Minor/Major); `NestData.Tier` field; `NestGenerator` Option B: 3 nests (Wolf Minor, Goblin Minor, Raider Major with Orc). Major dot larger on mini+world maps.
- [x] **Humanoid HP from HD formula** — Goblin 31.5, Bandit 60.5, BanditArcher 40.5 (all via `ComputeHp` in MonsterRegistry).
- [x] **Rolled player HP** — `ClassKitData.HitDiceCount/HitDieSize`; `HealthSystem.RollPlayerHp`; `ApplyConstitution` called from `CombatSystem.RequestSetStats`; result stored in `_playerBaseHp`.
- [x] **Athletics HP growth** — `floor(level/2)` bonus HP; `HealthSystem.OnAthleticsLevelUp` called from `SkillSystem.NotifyAction`; `RecomputeMaxHp` recomputes and broadcasts.
- [x] **HP persistence** — `SaveData.PlayerSave.BaseHp`; saved in `SaveSystem.Save()`; restored in `RestoreHpFromSave(peerId, hp, baseHp)`.
- [x] **Test rebuild** — fixed `SaveUtil.cs` test exclusion (ADR-0010), updated stale `SkillRegistryTests`, 9 new MonsterRegistryTests, 9 new NestGeneratorTests, 14 new HpFormulaTests. **350 tests, 0 failures**.

### Editor tasks (Edu — required before playtest)
- [x] Add `HerbSystem` node (script: `res://scripts/server/HerbSystem.cs`) to `GameWorld.tscn` after `BushSystem`. Plain `Node`.
- [x] Create `scenes/WoodenWall.tscn` — Node3D + StaticBody3D + BoxMesh 2×3×0.4 + BoxShape3D, collision layer 8.
- [x] Create `scenes/WoodenGate.tscn` — same spec as WoodenWall (no interaction yet).

### Playtest session
- [ ] Schedule and run 30–60 min session with a friend (host + join over LAN/ENet)
- [ ] Log: crashes, confusing UI, blocking bugs, missing feedback
- [ ] Fix P0 (crash) and P1 (major blocker) bugs found during session

### Success criteria (all three must be yes)
- [ ] **Fun** — both players want to keep playing after the session
- [ ] **Legible** — both players understand what to do next without developer help
- [ ] **Stable** — no crashes or state corruption during the session

### Post-playtest-prep fixes (2026-07-18, session 2) ✅ complete
- [x] **Death drop compile fix** — `0xHP1234u` → `0xD1CE1234u` in `HealthSystem.cs`. H/P not valid hex; unblocked the death-drop system.
- [x] **Fog not restored on load** — `FogSystem.BroadcastFog()` made public; `SaveSystem.TryLoad()` calls it after `RestoreFogFromBase64`. Clients now receive explored fog state on load.
- [x] **NPC workers/settlers gone after load** — `GetAssignmentsForSave()` iterates `_settlementNpcs` (was `_workAssignments` only); sleeping + idle settlers now persisted.
- [x] **Weapon slot reads (Tier 1)** — MeleeController + BowController read `LocalState.EquippedMainHand/OffHand` first; inventory scan fallback for legacy saves.
- [x] **MoveSpeed buff client sync (Tier 1)** — `BuffSystem.NotifyMoveSpeedClient` RPC; `LocalState.SetMoveSpeedBuff`; `PlayerController` applies multiplier.
- [x] **Code cleanup (Tier 2)** — dead `RequiresPresence` field; VillageSystem float→double; bare `Random` ambiguity; ProjectileSystem docstring.
- [x] **SaveUtil Godot dependency removed (Tier 3)** — provider delegate pattern; implementation moved to SaveSystem; test .csproj exclusion removed.
- [x] **Docs updated (Tier 3)** — ARCHITECTURE.md §6 (entity model) + §7 (determinism/RNG) rewritten; CLAUDE.md rule 6 + rule 8 tightened.
- [x] **ClassSelectScreen removed** — dead code, superseded by CharacterCreateScreen.

### Pre-playtest verification (required before playtest session)
- [ ] **Godot build** — open Godot, Build → Build Solution, confirm 0 errors. Critical: HealthSystem 0xD1CE1234u change and FogSystem/BuffSystem additions must compile.
- [ ] **Smoke-test bug fixes** — die → respawn → pick up death drop ✓; load game → fog explored state preserved ✓; load game → NPC workers still in roster and working ✓.

### Pre-playtest: shelter capacity recruitment gate
- [x] **Shelter capacity gate** — `VillageSystem.RequestRecruit` rejects if `_settlementNpcs.Count >= shelterCount * 2`. `reject.no_shelter_capacity` loc key added to `en.json`.

---

## Character trait system ✅ COMPLETE (2026-07-20)

GDD §11 (alignment), §12 (racial/class traits), and combat.md §16-17 (saving throws, block/crit class traits).

- [x] **Alignment** — `Alignment` enum + `AlignmentExtensions`; `GameSession.ChosenAlignment`; `CharacterCreateScreen` alignment button group + handler; `SaveData` v1→v2 (`SessionSave.Alignment`); `SaveSystem` migrate+save+restore; `en.json` loc keys
- [x] **Racial traits** — `RaceData.SavingThrowBonuses/CombatBonusVs`; `RaceRegistry` populated (Elf +4 sleep/charm, Dwarf +2 poison/magic + +2 AB vs goblin/orc, Halfling +2 magic/poison); Dwarf Wis penalty corrected to dormant Cha; `CombatSystem.RequestSetRace` RPC + Dwarf AB bonus; `PlayerController.AnnounceRace()`
- [x] **Class traits** — `ClassKitData.ActiveBlockBonus/RangedCritThreshold`; Fighter=6/24, Ranger=4/22; `CombatSystem.RequestSetClass` + per-class block bonus in `GetPlayerTargetNumber`; `ProjectileSystem` per-shooter crit threshold via `CombatSystem.GetRangedCritThreshold()`; `PlayerController.AnnounceClass()` also calls CombatSystem
- [x] **SavingThrowResolver** — `shared/SavingThrowResolver.cs`: 1d20 + floor(skillLevel/10) + racialBonus ≥ difficulty
- [x] **Tests** — `AlignmentTests.cs` (12), `SavingThrowResolverTests.cs` (17); RaceRegistry + ClassKitRegistry extended; stale tests fixed. 417 total, 0 failures.
- [x] **Editor task** — Add `AlignLawfulButton`, `AlignNeutralButton`, `AlignChaoticButton` (Button nodes under `Alignment` Label) to `scenes/CharacterCreateScreen.tscn`

---

## M10 — World gen quality + river

**Goal:** River flows through the world; forest clusters feel natural; terrain looks carved rather than flat.

### River generation + terrain carving ✅ COMPLETE (2026-07-20)
- [x] **RiverSegment.cs + RiverData.cs** (shared) — record types; `ChannelMask bool[,]`; `IsInChannel()` helper
- [x] **RiverGenerator.cs** (shared) — D8 downhill-biased walk; source = highest border cell of 8 tries; MIN_RIVER_STEPS=20 guarantees non-trivial paths; monotonic height smoothing; cosine taper carving (CHANNEL_DEPTH=3, WATER_SURFACE_OFFSET=1.5); 1D width noise sub-salt
- [x] **TerrainSystem.cs** — pipeline: `GenerateHeightmap()` → `RiverGenerator.Generate()` in-place carve → `River` static property → `HeightMapShape3D` built from carved heightmap; `IsInRiverChannel()` helper
- [x] **TreeGenerator.cs** — optional `riverMask` param; skips channel cells
- [x] **TreeSystem.cs** — passes `TerrainSystem.River?.ChannelMask` to generator
- [x] **WaterSystem.cs** (server, both peers) — `ArrayMesh` ribbon (UV.y = arc-length for downstream scroll, tangent data for NORMAL_MAP); loads `res://shaders/water_river.gdshader` at runtime; flat-blue fallback; `Area3D` WaterTrigger per-segment
- [x] **water_river.gdshader** — scrolling normal map, semi-transparent blue, specular highlights
- [x] **docs/gdd/water.md** — algorithm, carving math, ribbon spec, shader spec

### Forest clustering ✅ COMPLETE (2026-07-20)
- [x] **BushGenerator.cs** — clustering: NEAR_TREE_CHANCE=0.70 within CLUSTER_RADIUS=3 tiles of any tree; ISOLATED_CHANCE=0.30 elsewhere; `riverMask` exclusion; `trees` + `riverMask` optional params
- [x] **BushSystem.cs** — uses carved `TerrainSystem.Heightmap`; re-generates tree list for clustering seed; passes both masks

### Tests ✅ COMPLETE (2026-07-20)
- [x] **RiverGeneratorTests.cs** — 11 tests: determinism, different seeds, bounds, min length (seeds 1–10), tangent normalisation, carving depth, channel mask, IsInChannel, monotonic WaterY, TreeGenerator exclusion integration
- [x] **TreeGeneratorTests.cs** — +2 tests: river mask exclusion, no-mask still deterministic
- [x] **BushGeneratorTests.cs** — 6 tests: determinism with/without trees, height floor, river mask exclusion, clustering concentration, target count preserved
- **370 tests, 0 failures**

### River shore polish (2026-07-21) ✅ COMPLETE
- [x] `WaterSystem.cs` — ribbon upsampled at 1 m intervals via `UpsampleSegments`; tangents re-derived post-upsample
- [x] `TerrainRenderer.cs` — bank-region cells subdivided into 4×4 sub-quads (1 m visual resolution) via `AddBankSurface`
- [x] `WaterSystem.cs` — `NoiseTexture2D` normal map constructed and assigned in code; no editor action needed
- [x] `docs/gdd/water.md §5` — locked constraint: map-size increase must scale cell count, not TileSize

### Editor tasks pending (Edu)
- [x] Add `WaterSystem` node (script `res://scripts/server/WaterSystem.cs`) to `GameWorld.tscn` after `TerrainSystem`. Plain `Node`. ✅ confirmed done
- [x] Assign `normal_texture` uniform — done in code (`WaterSystem._Ready`); no editor action needed.

---

## Blocked

Nothing.

---

## Post-playtest backlog (post-M9, milestone TBD)

These features are scoped after the M9 playtest signal is evaluated.

### Survival pressure — HP decay and thirst

#### HP decay when starving/exhausted ✅ COMPLETE (2026-07-20)
- [x] `server/NeedsSystem.cs` — Hunger=0 drains HP at 2f/60f per second via `HealthSystem.ApplyDamage`; Rest=0 triggers three-phase exhaustion: immediate MoveSpeed×0.5, +60s AttackBonus−2 + stumble pulse, +300s HP drain. Private `KillPlayer` removed — death goes through `HealthSystem.KillPlayer`. `ClearExhaustionState` resets all phases when Rest rises above 0.
- [ ] `data/lang/en.json` — HUD warning strings (`hud.starving`, `hud.exhausted`) — deferred post-playtest
- [ ] Regression test: HP drains at correct rate when need = 0; HP stops draining when need refills — deferred (NeedsSystem has Godot dependency, not unit-testable)

#### Thirst system
- [ ] `shared/ThirstData.cs` — record: MaxThirst (100), DrainPerMin (10), DrinkRestoreAmount (40); integrate into NeedsData or add Thirst field to PlayerSave
- [ ] `server/NeedsSystem.cs` — add Thirst float per peer; drain 10/min; Thirst = 0 → HP drains same rate as hunger/rest; `RequestDrink(peerId, amount)` restores thirst
- [ ] `shared/LocalState.cs` — add `Thirst` float + `SetThirst`; `ThirstChanged` event
- [ ] `client/NeedsHUD.cs` — add blue-teal thirst bar alongside hunger + rest
- [ ] `shared/BuildingRegistry.cs` — add `Well` (`building.well`, 20 stone, spawns water source node); add `WaterBarrel` (`building.water_barrel`, 8 wood, stores 200 water units)
- [ ] `server/SettlementSystem.cs` — track water storage in stockpile (`resource.water`); `RequestFillBarrel` pulls from Well; `RequestDrinkFromBarrel` at barrel/well removes resource.water + calls NeedsSystem.RestoreThirst
- [ ] Village water deposit: water automatically deposited to stockpile from wells on a 60-second tick (no NPC required)
- [ ] `client/PlayerController.cs` — E key near Well/WaterBarrel → drink if thirst < 100
- [ ] `data/base/resources/water.json` — item stub (`resource.water`)
- [ ] `data/lang/en.json` — thirst HUD, well/barrel names, water item, drink feedback strings
- [ ] Save round-trip: Thirst persisted in PlayerSave; bump SaveData.Version + migration stub
- [ ] Tests: thirst drain rate, drink restore, HP decay when all three needs = 0

