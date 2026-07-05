# Current Milestone: M4 — Combat and monsters

**Started:** 2026-07-04
**Target demo:** Two players — one Fighter (sword + shield), one Ranger (shortbow + arrows) — find a bandit camp from nest placement, clear it cooperatively, both combat styles functional, death drops inventory, respawn at shelter.

## Scope (from VERTICAL_SLICE.md §3.4 + §5)

- Player health/damage system with HP persistence ✅
- Melee combat: LMB swing, RMB block, range check ✅
- Ranged combat: mouse aim, projectile arc, arrow crafting ✅
- Enemies: Wolf, Goblin, Bandit (melee), Bandit Archer (ranged AI) ✅
- Combat/build mode state (C key); melee/ranged toggle (Q key) ✅
- WeaponHUD — mode + equipped weapon display ✅
- Monster nests: deterministic placement, respawn after clear ✅
- Death penalty: drop inventory at death position (recoverable), respawn at shelter ✅
- Minimap (top-right, always visible) + World map (M key) with nest/player/marker/death-drop overlays ✅
- Dice-based combat resolution per combat.md GDD ⬜ Phase 4.7 (plan ready, awaiting go)
- Class kit distribution on world join (Fighter / Ranger two-button screen) ⬜ Phase 6
- Health HUD: HP bar for local player ✅

## Key decisions (locked)

- **Stamina deferred to M5.** M4 melee has no stamina cost.
- **Skill XP from combat deferred to M5.** M4 logs XP to Output only.
- **Death penalty skill loss deferred to M5.** M4 penalty is inventory drop only.
- **Projectile arc:** parabolic — initial velocity + downward gravity per tick.
- **Monster AI (v1):** Idle → Aggro → Attack state machine; straight-line movement, no pathfinding.
- **Monster HP is server-authoritative.** Clients receive HP updates via RPC.
- **Monster IDs start at 10001L** to avoid collision with player peer IDs.
- **Combat/build mode is client-only state** — server doesn't track it; all server validation is unchanged.
- **Debug kit (temp):** HealthSystem.OnPlayerConnected gives sword + shield + shortbow + 10 arrows — remove when Phase 6 class selection lands.
- **Combat resolution (GDD locked):** hybrid d20 roll; gentler stat modifier curve `floor((stat-10)/4)`; flat authored attack_bonus/target_number for true beasts; live Dex+armor formula for gear-bearing humanoids. See `docs/gdd/combat.md`.

## Remaining phases

### Phase 4.7 — Dice-based combat resolution (plan ready, awaiting go)
- [ ] `shared/CombatResolver.cs` — StatModifier, RollDice, ResolveAttack, PlayerAttackBonus, PlayerTargetNumber
- [ ] `shared/WeaponData.cs` — add DamageDice (string), DamageType (string)
- [ ] `shared/MonsterData.cs` — add AttackBonus (int), TargetNumber (int), DamageDice (string), DamageType (string)
- [ ] `shared/WeaponRegistry.cs`, `shared/MonsterRegistry.cs` — add authored values
- [ ] `data/base/weapons/*.json`, `data/base/monsters/*.json` — add dice/type fields
- [ ] `server/CombatSystem.cs` — wire dice resolution; nat-20 = double damage
- [ ] `server/MonsterSystem.cs` — replace flat MeleeDamage with dice roll
- [ ] `server/ProjectileSystem.cs` — replace flat damage with RollDice on hit
- [ ] `tests/Shared/CombatResolverTests.cs` — stat modifier table, formula examples, nat-20/nat-1 rules
- **Open:** player stats placeholder (Str=13, Dex=12) vs Phase 6 class kits first — Edu to decide

### Phase 6 — Class kit selection
- [ ] `shared/ClassKitData.cs` — record: ClassId, DisplayNameKey, StartingItemIds
- [ ] `client/ClassSelectScreen.cs` — two-button screen (Fighter / Ranger) shown before world join
- [ ] `shared/GameSession.cs` — add ChosenClassId field
- [ ] `server/HealthSystem.cs` or `PlayerSystem.cs` — distribute starting kit on peer connect based on ChosenClassId; remove debug kit
- [ ] **Editor:** Create `scenes/ClassSelectScreen.tscn`; wire into scene flow after MainMenu host/join

### Tests still needed
- [ ] `tests/Shared/HealthDataTests.cs`

## Out of scope for M4

- Class stat rolling (M5)
- Skill leveling from combat (M5)
- Death penalty skill loss (M5)
- Character sheet UI (M5)
- Stamina (M5)
- Orc enemy type (deferred)
- Recruited NPC combat (M6)
- Save/load of world state (M8)
- Crit/fumble Phase B — per-damage-type tables (roadmap, post-slice)
