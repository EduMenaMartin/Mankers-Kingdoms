# Handover — Rolling Session Context

**Update this file at the end of every substantial session.**

---

## Current status

**Milestone:** M5 — in progress (Phases 1–5 code complete; editor tasks + demo gate pending)
**Last session:** 2026-07-08 — GDD §13 (ranged asymmetry) locked + implemented; KayKit animation system (PlayerAnimator) implemented; Player.tscn editor tasks complete. 214 tests, 0 failures.
**Blockers:** None.
**Awaiting:** Edu to add CharacterSheet + InventoryPanel CanvasLayer nodes to GameWorld.tscn; build CharacterCreateScreen.tscn scene; then run M5 demo gate.

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

## What's next (top 3)

1. **Editor tasks (nodes to add to GameWorld.tscn):**
   - `InventoryPanel` CanvasLayer → script `res://scripts/client/InventoryPanel.cs`
   - `CharacterSheet` CanvasLayer → script `res://scripts/client/CharacterSheet.cs`
   - `SkillSystem` Node → script `res://scripts/server/SkillSystem.cs` (if not already done)

2. **Editor task: `scenes/CharacterCreateScreen.tscn`** — build the scene (instructions in session log below). Once done, the full M5 char-creation flow is playable end-to-end.

3. **M5 demo gate:** Player creates Ranger, chops 75 trees → Woodcutting 15 → bronze axe granted → K key shows cap reached in orange. Verify CharacterSheet shows race/class/stats; verify animations play (idle, walk, hit, death).

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
