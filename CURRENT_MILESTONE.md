# Current Milestone: M4 — Combat and monsters

**Started:** 2026-07-04
**Target demo:** Two players — one Fighter (sword + shield), one Ranger (shortbow + arrows) — clear a bandit camp cooperatively using both combat styles.

## Scope (from VERTICAL_SLICE.md §3.4 + §5)

- Player health/damage system with HP persistence
- Melee combat: directional swing (LMB), block (hold RMB), hit detection by range + facing angle
- Ranged combat: aim with mouse, click to fire arrow, projectile with arc trajectory and travel time, can miss
- Arrow crafting at Workbench (wood → arrows)
- Enemies: Wolf, Goblin, Bandit (melee), Bandit Archer (ranged AI)
- Monster nests: Goblin nest, Bandit camp — deterministic placement, respawn after a delay
- Death penalty: drop all carried inventory at death position (recoverable), respawn at shelter
- Minimal class kit distribution on world join (Fighter: sword + shield; Ranger: shortbow + hunting knife + arrows)
- Health HUD: HP bar for local player

## Key decisions

- **Class selection UI is M5, not M4.** M4 distributes a class kit via a pre-world selection (simple two-button screen, no stats rolled yet). Full character creation (stat rolling, skill initialization, class sheet) is M5.
- **Stamina deferred to M5.** VERTICAL_SLICE.md mentions "simple stamina cost" for melee — implementing stats first in M5 makes stamina meaningful. M4 melee has no stamina cost.
- **Skill XP from combat deferred to M5.** Melee and Ranged skills grow through use (ADR-0020), but the skill system itself is M5. M4 logs XP to Output as a stub (same pattern as M2 Woodcutting).
- **Death penalty skill loss deferred to M5.** VERTICAL_SLICE.md says "lose 1 level in highest skill" — requires the skill system. M4 penalty is inventory drop only; skill loss added in M5.
- **Projectile arc:** simple parabolic trajectory — initial velocity vector with downward gravity applied each tick. Not ballistic simulation.
- **Monster AI (v1 simple):** state machine — Idle (wander) → Aggro (move toward target) → Attack (swing/shoot in range). No pathfinding around obstacles in M4; straight-line movement.
- **Monster HP is server-authoritative.** Clients see monster HP updates via RPC; visual feedback only.

## Architecture decisions in play

- `WeaponData` + `MonsterData` in shared/ (pure C#, testable)
- `HealthSystem` (server/) owns all HP state — players and monsters
- `CombatSystem` (server/) validates melee: sender position, target position, range, angle
- `ProjectileSystem` (server/) ticks projectiles per frame, collision by sphere overlap
- `MonsterSystem` (server/) owns AI tick — runs only on server, no Godot rendering assumptions
- `MonsterNode` (client/) is a visual-only receiver — position/health pushed via RPC
- Server/client separation: server never touches Godot rendering; client never runs HP logic

## Out of scope for M4

- Class stat rolling (M5)
- Skill leveling from combat (M5)
- Death penalty skill loss (M5)
- Character sheet UI (M5)
- Stamina (M5)
- Stealth (scaffold only, no mechanics)
- Orcs (M4 adds wolf/goblin/bandit/bandit_archer — orc deferred)
- Recruited NPC combat (M6)
- Save/load of world state (M8)

## Files being created

### Shared
- `shared/HealthData.cs` — record: CurrentHp, MaxHp, IsDead
- `shared/MsgEntityHealth.cs` — DTO: EntityId, CurrentHp, MaxHp
- `shared/WeaponData.cs` — record: Id, Damage, Range, SwingCooldown, IsRanged, ProjectileSpeed
- `shared/WeaponRegistry.cs` — sword, shield (block value), shortbow, hunting knife, arrow
- `shared/MonsterData.cs` — record: Id, MaxHp, MeleeDamage, MoveSpeed, AggroRange, AttackRange, LootTable
- `shared/MonsterRegistry.cs` — wolf, goblin, bandit, bandit_archer
- `shared/ProjectileState.cs` — record: Id, OriginPeerId, WeaponId, Position, Velocity
- `shared/ClassKitData.cs` — record: ClassId, StartingItemIds

### Server
- `server/HealthSystem.cs` — HP tracking for all entities, Damage/Heal/Kill RPCs, death handling
- `server/CombatSystem.cs` — RequestMeleeAttack RPC: validate range + angle, call HealthSystem
- `server/ProjectileSystem.cs` — tick projectiles, sphere collision vs players/monsters, damage on hit
- `server/MonsterSystem.cs` — AI state machine (Idle/Aggro/Attack), loot drop on death, position broadcast
- `server/NestSystem.cs` — deterministic nest positions, spawn timing, respawn after clear

### Client
- `client/MeleeController.cs` — LMB swing RPC, RMB block state, swing cooldown
- `client/BowController.cs` — mouse aim, LMB fire RPC, local arrow ghost
- `client/MonsterNode.cs` — receives position/health push from server, hit flash on damage
- `client/HealthHUD.cs` — HP bar anchored top-left
- `client/ClassSelectScreen.cs` — two-button pre-world screen (Fighter / Ranger), sets kit

### Data
- `data/base/weapons/sword.json`
- `data/base/weapons/shield.json`
- `data/base/weapons/shortbow.json`
- `data/base/weapons/hunting_knife.json`
- `data/base/weapons/arrow.json`
- `data/base/monsters/wolf.json`
- `data/base/monsters/goblin.json`
- `data/base/monsters/bandit.json`
- `data/base/monsters/bandit_archer.json`

### Tests
- `tests/Shared/WeaponRegistryTests.cs`
- `tests/Shared/MonsterRegistryTests.cs`
- `tests/Shared/HealthDataTests.cs`
- `tests/Shared/ProjectileStateTests.cs`

### Editor work (Edu)
- `scenes/Monster.tscn` — CharacterBody3D + CapsuleMesh + CapsuleShape3D (reused for all monster types via script)
- `scenes/Arrow.tscn` — Node3D + small BoxMesh
- `scenes/MonsterNest.tscn` — StaticBody3D + marker mesh (simple cylinder or flag)
- `scenes/ClassSelectScreen.tscn` — simple CanvasLayer with two buttons
- Add to `GameWorld.tscn`: HealthSystem, CombatSystem, ProjectileSystem, MonsterSystem, NestSystem nodes
- Add to `GameWorld.tscn`: HealthHUD CanvasLayer
