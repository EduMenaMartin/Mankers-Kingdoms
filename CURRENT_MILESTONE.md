# Current Milestone: M2 — World with things in it

**Started:** 2026-07-03
**Target demo:** Two ENet players on procedural 64×64 terrain; chop trees with E key; wood drops appear; Woodcutting XP logged.

## Scope (from VERTICAL_SLICE.md)

- Procedural terrain generation: heightmap → 64×64 tile map, 4 units/tile
- Day/night cycle: 2-minute real-time days; visual only (light rotation + energy)
- Player gravity: CharacterBody3D now falls and walks on uneven terrain
- Trees: placed from seed + config, 5 HP each, E key to chop, 3 wood on fell
- Woodcutting XP: logged to Output (no UI yet, that's M5)

## Key decisions

- 64×64 tiles (not full 500×500 — deferred to later milestone)
- E key for chop interaction
- Terrain and tree positions are deterministic from seed — both server and client generate identically, no sync RPC needed
- Day/night: 2-minute days for testing; tunable from config later

## Architecture decisions in play

- ARCHITECTURE.md §7: seeded RNG only (`WorldSeed.CreateRandom()`)
- ARCHITECTURE.md §3: `TerrainGenerator` + `TreeGenerator` in shared/ (pure C#, testable)
- `TerrainSystem` (server/) + `TerrainRenderer` (client/) both generate the same heightmap and create their own Godot children at runtime — no .tscn edits needed for terrain
- `TreeSystem` (server/) creates tree instances on ALL peers (deterministic); HP logic guarded by `Multiplayer.IsServer()`

## Out of scope for M2

- Inventory system (M3)
- Woodcutting XP UI (M5)
- Tree regrowth
- Rocks (visible in world but not interactive — M4+)
- Save/load (M8)
- Steam transport (deferred indefinitely pending C# bindings)

## Files being created

### Shared
- `shared/WorldSeed.cs`
- `shared/TerrainConfig.cs`, `TerrainGenerator.cs`
- `shared/TreeConfig.cs`, `TreeData.cs`, `TreeGenerator.cs`
- `shared/MsgDayNight.cs`

### Server
- `server/TerrainSystem.cs`
- `server/DayNightSystem.cs`
- `server/TreeSystem.cs`

### Client
- `client/TerrainRenderer.cs`
- `client/DayNightClient.cs`
- `client/TreeNode.cs`

### Updated
- `shared/GameSession.cs` (add WorldSeed field)
- `client/PlayerController.cs` (gravity + E-key interact)
- `client/MainMenuController.cs` (register interact action)
- `server/PlayerSystem.cs` (raise spawn Y above terrain)

### Data
- `project/data/base/resources/wood.json`

### Tests
- `tests/Shared/WorldSeedTests.cs`
- `tests/Shared/TerrainGeneratorTests.cs`
- `tests/Shared/TreeGeneratorTests.cs`

### Editor work (Edu)
- `scenes/Tree.tscn` — StaticBody3D + CylinderMesh + CollisionShape3D
- `scenes/WoodDrop.tscn` — Node3D + BoxMesh
- `scenes/GameWorld.tscn` — add TerrainSystem, TerrainRenderer, DayNightSystem, DayNightClient, TreeSystem nodes; remove old flat Ground node
