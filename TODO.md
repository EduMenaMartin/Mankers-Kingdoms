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
- [ ] **Editor:** Add HealthSystem node to GameWorld.tscn; add HealthHUD CanvasLayer

### Phase 2 — Weapons & melee ✅
- [x] `shared/WeaponData.cs` — record: Id, Damage, Range, SwingCooldown, IsRanged, ProjectileSpeed, AmmoItemId
- [x] `shared/WeaponRegistry.cs` — sword, shield, shortbow, hunting knife; hardcoded for now
- [x] `data/base/weapons/sword.json`, `shield.json`, `shortbow.json`, `hunting_knife.json`, `arrow.json`
- [x] `server/CombatSystem.cs` — RequestMeleeAttack RPC: validate weapon owned, cooldown, alive, distance; RequestSetBlocking; 50% flat block reduction
- [x] `client/MeleeController.cs` — LMB swing; RMB hold block; sphere query nearest target; client-side cooldown guard
- [x] `client/PlayerController.cs` — CollisionLayer |= 32u; AddChild(MeleeController)
- [ ] **Editor:** Add CombatSystem node to GameWorld.tscn

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
- [ ] **Editor:** Create `scenes/Monster.tscn`; add MonsterSystem node to GameWorld.tscn; add Monsters Node child to GameWorld

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

### Phase 5 — Nests & death penalty
- [ ] `shared/NestData.cs` — record: Id, MonsterTypeId, WorldX, WorldZ, MaxSpawned, RespawnDelaySec
- [ ] `server/NestSystem.cs` — deterministic nest positions (seeded), spawn timing, respawn after all monsters killed
- [ ] Death penalty in HealthSystem: drop full inventory at death position (ItemDrop nodes via RPC), respawn at shelter
- [ ] `client/PlayerController.cs` — pickup dropped items on E-interact with ItemDrop node
- [ ] **Editor:** Create `scenes/MonsterNest.tscn`; add NestSystem node to GameWorld.tscn

### Phase 6 — Class kit selection
- [ ] `shared/ClassKitData.cs` — record: ClassId, DisplayNameKey, StartingItemIds
- [ ] `client/ClassSelectScreen.cs` — two-button screen (Fighter / Ranger) shown before world join; sends chosen class ID in GameSession
- [ ] `shared/GameSession.cs` — add ChosenClassId field
- [ ] `server/HealthSystem.cs` or `PlayerSystem.cs` — distribute starting kit on peer connect based on ChosenClassId
- [ ] **Editor:** Create `scenes/ClassSelectScreen.tscn`; wire into scene flow after MainMenu host/join

### Tests
- [x] `tests/Shared/WeaponRegistryTests.cs`
- [x] `tests/Shared/MonsterRegistryTests.cs`
- [ ] `tests/Shared/HealthDataTests.cs`
- [x] `tests/Shared/ProjectileStateTests.cs`

### Demo gate
- [ ] M4 demo: two ENet players, one Fighter (sword + shield), one Ranger (shortbow + arrows); find bandit camp from nest placement; clear it cooperatively; both combat styles functional; death drops inventory, player respawns at shelter

---

## Blocked

Nothing.

