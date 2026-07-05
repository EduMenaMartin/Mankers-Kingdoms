# Changelog

Notable changes to the project. Not a git log — a human-facing history of what shipped in each milestone or version.

Follows [Keep a Changelog](https://keepachangelog.com/) conventions loosely.

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
