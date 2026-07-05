# Handover — Rolling Session Context

**Update this file at the end of every substantial session.**

---

## Current status

**Milestone:** M4 — Combat and monsters (in progress)
**Last session:** 2026-07-05 — Phase 6 (ClassSelectScreen, ClassKitRegistry) complete; floating combat text (CombatFeedbackHUD) complete. 146 tests passing.
**Blockers:** Two editor tasks pending (see below).
**Awaiting:** Edu to complete editor tasks, then run game and test combat feel + floating numbers.

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

## What's next (top 3)

1. **Editor task A** — Create `scenes/ClassSelectScreen.tscn`:
   `Control` (script: `client/ClassSelectScreen.cs`) → `VBoxContainer` → `%TitleLabel` (Label), `%SubtitleLabel` (Label), `HBoxContainer` → `%FighterButton` (Button), `%RangerButton` (Button). Enable "Access as Unique Name" on all four or just ensure child names match exactly (`FindChild` is used, so unique names are optional).
   Wire `MainMenuController` → `ClassSelectScreen.tscn` (already done in code).

2. **Editor task B** — Add `CombatFeedbackHUD` node to `GameWorld.tscn`:
   Add a plain `Node` as a child of GameWorld, rename it exactly `CombatFeedbackHUD`, attach script `client/CombatFeedbackHUD.cs`. No special position needed — it has no visual of its own. Server systems look it up via path `/root/GameWorld/CombatFeedbackHUD`.

3. **M4 demo gate** — two players, Fighter + Ranger, find bandit camp from minimap, clear cooperatively. Floating combat numbers should be visible during the demo.

---

---

## Blocked

Nothing.

## Decisions needed from Edu

None outstanding.

---

## Session log

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
