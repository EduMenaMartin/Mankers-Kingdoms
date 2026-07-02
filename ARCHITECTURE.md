# Mankers Kingdoms — Technical Architecture

**Status:** v0.1 — living document. Architectural principles are locked (require ADR to change). Specific implementations may evolve.

**Related:** `PRD.md` §8, all ADRs in `docs/decisions/`

**Last updated:** 2026-07-02

---

## 1. Guiding principles

These principles inform every technical decision. When two implementations conflict, the principle wins.

1. **Correctness over cleverness.** A boring, well-understood approach beats an elegant one we don't fully understand.
2. **Dedicated server is not an afterthought.** The host is a special case of a dedicated server that happens to also run a local client.
3. **Data is not code.** All content lives in data files. Code operates on data. Enables modding, hot-reload, and clean testing.
4. **Server is authoritative.** Clients are windows onto server state. They predict for local player only, interpolate for others.
5. **Determinism where cheap.** Seeded RNG, ordered iteration, reproducible tick outputs. Not required for MP sync, valuable for debugging and replay.
6. **Strings are not code.** All player-facing text loaded from localization files. No literals in gameplay code.
7. **The base game is a mod.** The mod loader loads the base game the same way it loads community mods.
8. **Test what will hurt to break.** Save format, netcode, simulation ticks, mod loading. Everything else optional.

---

## 2. Stack

| Layer | Choice | Rationale (ADR) |
|---|---|---|
| Engine | Godot 4 (latest stable) | ADR-0010 |
| Language | C# / .NET | ADR-0010 |
| Networking | GodotSteam (Steam relay + P2P + dedicated) | ADR-0002, ADR-0005 |
| Sync model | Authoritative host + client prediction/interpolation | ADR-0005, ADR-0022 |
| IDE (primary) | JetBrains Rider + Claude Code plugin | — |
| IDE (secondary) | VS Code + C# Dev Kit + Claude Code extension | — |
| Version control | Git + Git LFS for binary assets | — |
| Test framework | xUnit for C# libraries + GdUnit4 for Godot integration | — |
| Package management | NuGet for C#; git submodules for GodotSteam if not on registry | — |

---

## 3. Client/server split

The single most important architectural discipline. Enforced by folder structure at the source level.

### 3.1 Folder rule

```
/project/scripts/
├── server/     # runs on server (including host, including headless dedicated)
├── client/     # runs on client (never on headless dedicated server)
└── shared/     # runs on both; must be side-effect-free w.r.t. platform
```

**Enforcement:**
- Any `.cs` file in `/client/` importing from `/server/` is a build-time warning.
- Any `.cs` file in `/server/` importing from `/client/` is a build error.
- Both may import from `/shared/`.
- CI runs a static check pass to enforce these rules.

### 3.2 What lives where

**`/server/`:**
- World simulation (tick loop, entity updates)
- NPC AI and pathfinding
- Combat resolution, hit detection
- Save/load serialization
- Mod content loading (data phase)
- Persistence and world state
- Player action validation
- Random number generation (seeded)

**`/client/`:**
- Rendering
- Input capture and prediction
- UI scenes and controllers
- Audio playback
- Client-side prediction for local player movement
- Interpolation of remote entities
- Camera control
- Visual effects, particles

**`/shared/`:**
- Data models (Entity, NpcDefinition, ItemDefinition, etc.)
- Constants and enums
- Math utilities
- Serialization schemas
- Networking message types (DTOs)
- Localization key definitions
- Shared validation logic (client can predict, server validates authoritatively)

### 3.3 The host is a dedicated server + local client

When a player "hosts a game," we start a single process running:
- The `/server/` scene tree (headless-capable)
- The `/client/` scene tree (rendered locally for the host player)
- Both connected via internal RPC (same as network RPC, but zero-latency)

Running dedicated is the same process with the `/client/` scene tree never loaded:

```
godot --headless -- --mode=dedicated --world=<save_id> --port=<port>
```

This means dedicated-server support is not a separate build. It's a runtime flag. Testing the dedicated code path = running the game with `--headless`.

---

## 4. Networking

### 4.1 Model

**Authoritative host / server:** the server maintains the sole authoritative game state. Clients predict for the local player, interpolate for everyone else, and accept the server's word on all state.

**Not lockstep.** See ADR-0022 for the full rejection reasoning.

### 4.2 Transport

GodotSteam provides:
- Steam relay networking (NAT-traversal-free)
- Direct P2P with Steam identity
- Dedicated-server TCP/UDP endpoints
- Steam ID as player identity

Fallback: raw ENet via Godot's built-in `MultiplayerAPI` for LAN testing without Steam. Never shipped, only used in M0–M1 for early bring-up if GodotSteam integration is slow.

### 4.3 Message categories

| Category | Direction | Frequency | Reliability | Example |
|---|---|---|---|---|
| Input | Client → Server | Every tick (20 Hz) | Unreliable ordered | WASD, mouse aim, swing |
| State snapshot | Server → Client | Every 3 ticks (~7 Hz) | Unreliable ordered | Entity positions, health |
| Event | Server → Client | On event | Reliable ordered | Item picked up, damage dealt |
| Command | Client → Server | On action | Reliable ordered | Build structure, assign NPC |
| Sync | Bidirectional | On connect | Reliable | Full world state on join |
| Chat | Bidirectional | On send | Reliable | Player messages |

### 4.4 Prediction and interpolation

**Local player movement:** Client predicts based on its own input immediately (no perceived lag). Server validates; if predicted position diverges beyond threshold, server sends correction and client smoothly rubber-bands.

**Remote entities:** Client receives snapshots at ~7 Hz. Between snapshots, interpolates smoothly toward the latest received position. Small buffer (100–200ms) to hide jitter.

**Combat:** Server-authoritative hit detection. Client shows immediate swing animation on input, but damage is only applied when the server confirms the hit. No client-side hit prediction — this is the correct trade-off for a co-op (non-competitive) game where visual honesty beats snappy fake hits.

### 4.5 Bandwidth budget

Target: **< 30 KB/s per client** at typical play (2–6 players, 30 entities visible).

If we blow past this, first optimization is snapshot delta compression (send only what changed since last ack). Second is area-of-interest culling (don't send entities outside player's view radius).

---

## 5. Tick model

### 5.1 Server tick

**Fixed timestep, 20 Hz** (50ms per tick). All simulation runs in tick order:

```
For each tick:
  1. Read pending client inputs
  2. Advance entity systems (movement, physics, combat, needs)
  3. Advance NPC AI decisions
  4. Advance slow systems (day/night, weather, resource regen) — see §5.3
  5. Emit state deltas and events to clients
  6. Advance tick counter, save if autosave threshold hit
```

Server tick is deterministic given the same inputs and RNG seed. This is not required for MP correctness (state replicates directly) but is used for:
- Server-side replays for debugging
- Save-file soul (world snapshot + tick counter = resumable exactly)
- Modding: same seed + same mods = same world

### 5.2 Client tick

Clients run at framerate (60+ Hz for rendering) but process game logic aligned to server ticks. Between server snapshots, they:
- Interpolate remote entities
- Extrapolate local player if inputs sent but no server response yet
- Play animations, sounds, particles

Client "game logic" is thin — most is display-only.

### 5.3 Fast/slow tick split

Not everything needs 20 Hz. To keep server load manageable:

| System | Tick rate | Notes |
|---|---|---|
| Player movement, physics | 20 Hz | Full fidelity |
| Combat resolution | 20 Hz | Full fidelity |
| Projectile updates | 20 Hz | Arrows in flight |
| NPC pathfinding update | 5 Hz | Recompute path every 4 ticks unless interrupted |
| NPC needs (hunger, rest) | 1 Hz | 20× cheaper |
| Building production (Woodcutter's Post tick) | 1 Hz | Aggregate work per second |
| Day/night cycle | 1 Hz | Slow enough |
| Weather changes | 0.1 Hz | Every 10 seconds |
| Save autosave | 0.003 Hz | Every 5 minutes |

Slow systems tick from a "wallclock counter" that increments on the main tick loop.

---

## 6. Entity model

### 6.1 ECS-lean, not full ECS

We use a component-heavy composition pattern with C# classes. Not a full framework like Arch or Entitas — those are optimizations for millions of entities, and we won't have millions.

An entity is:
- A unique `EntityId` (stable string, e.g. `"player.6b7f...", "npc.village_0.3", "monster.goblin.42"`)
- A bag of components (`PositionComponent`, `HealthComponent`, `NeedsComponent`, `SkillsComponent`, etc.)
- Registered with the server's entity manager

Components are data-heavy, behavior-light. Systems operate on components:

```csharp
// Data
class NeedsComponent {
    public float Hunger;    // 0-100
    public float Rest;      // 0-100
    public float HungerRate; // per-tick decay
}

// Behavior
class NeedsSystem : IServerSystem {
    public void Tick(World world) {
        foreach (var e in world.EntitiesWith<NeedsComponent>()) {
            var n = e.Get<NeedsComponent>();
            n.Hunger = Math.Max(0, n.Hunger - n.HungerRate);
            // ...
        }
    }
}
```

### 6.2 Player vs NPC unification

Players and NPCs share components. The only difference is:
- Player entity has a `ClientOwnedComponent` (which client controls it)
- NPC entity has an `AiControlledComponent` (which archetype, current station)

This means the skill/stat/needs/combat systems don't care whether they're operating on a player or an NPC. Single code path for progression, damage, hunger, etc.

### 6.3 Serialization

Every component has a `Serialize()` and `Deserialize()` method returning JSON. The world's save state is a list of `{EntityId, components: [...]}` records. Migrations happen at load time based on the schema version in the save.

---

## 7. Determinism policy

Determinism is a **discipline for the server simulation**, not a networking model.

We enforce:
- **Seeded RNG.** Every random call goes through `world.Random.Next()`, seeded per-world at generation time. Never `System.Random` or `Random.Shared`.
- **Ordered iteration.** Never iterate over `Dictionary<T>` or `HashSet<T>` in gameplay logic; use `SortedDictionary<T>` or explicit ordering by `EntityId`.
- **No wall-clock in sim.** Sim reads tick counter, not `DateTime.Now`.
- **Explicit float precision policy.** Sim uses `double` internally, rounded to fixed precision on serialization. No accumulator drift.

This gives us:
- Server-side replay for debugging (record inputs + seed → replay same world)
- Save-file reproducibility (same seed + same mods = same world generation)
- Mod behavior consistency (a mod that adds a monster produces the same monster on every player's screen)

This does NOT give us:
- Cross-machine bit-exact reproducibility (Godot Physics, .NET runtime differences)
- Lockstep networking (we don't want it — see ADR-0022)

---

## 8. Persistence

### 8.1 Save format

**JSON, with schema version field.** Human-readable, moddable, easy to debug, easy to hand-edit for testing.

```json
{
  "version": 1,
  "world_seed": 8472635,
  "tick": 84200,
  "entities": [ ... ],
  "settlements": [ ... ],
  "mods_loaded": [
    { "id": "mankers.base", "version": "0.1.0" },
    { "id": "mymod.extramonsters", "version": "1.2.0" }
  ]
}
```

### 8.2 Schema versioning

`version` field from day one. Every schema change increments it and adds a migration function:

```csharp
class SaveMigrations {
    public static SaveState Migrate(SaveState save) {
        while (save.Version < CurrentVersion) {
            save = save.Version switch {
                1 => MigrateV1toV2(save),
                2 => MigrateV2toV3(save),
                _ => throw new UnsupportedSaveException()
            };
        }
        return save;
    }
}
```

### 8.3 Autosave and manual save

- Autosave every 5 minutes on the server
- Autosave on host quit / dedicated server shutdown (graceful)
- Manual save via chat command `/save` or menu button
- Save files rotated (keep last 5 autosaves + last 3 manual saves)

### 8.4 Missing mod resilience

If a save references a mod that isn't currently loaded, entities and content from that mod are preserved as **inert placeholders** — they don't crash the load, they just don't tick or respond. Player is warned on load. If the mod is later re-enabled, placeholders resurrect.

---

## 9. Localization architecture

### 9.1 File format

Per-language JSON files. Nested by feature area.

```
/data/lang/
├── en.json    (base language, canonical keys)
├── de.json
├── es.json
```

```json
{
  "menu": {
    "start_solo": "Start Solo",
    "host_multiplayer": "Host Multiplayer",
    "join_multiplayer": "Join Multiplayer"
  },
  "combat": {
    "damage_dealt": "You dealt {0} damage to {1}"
  }
}
```

### 9.2 Access pattern

All UI code reads through `Loc.T("menu.start_solo")` — never a hardcoded string. Format substitutions via `Loc.T("combat.damage_dealt", damage, targetName)`.

**No string literals in gameplay code.** Enforced by grep-based CI check.

### 9.3 Fallback

Missing key in current language → fall back to English → fall back to key name displayed in `[brackets]` so translators immediately see what's missing.

### 9.4 Modding

Mods drop `.json` files into `/mods/mymod/lang/` and their keys are merged into the global keyspace. Modders can also **override** base game strings by defining the same key.

New language files added by community: player drops `fr.json` into any mod's lang folder → French appears in the language dropdown. No code changes needed.

### 9.5 Choice: JSON now, Godot's `.po` gettext later

Godot 4 has native gettext support. It's more powerful (plurals, contexts) but heavier. For v1 the JSON approach is simpler; migration to `.po` is possible later if the community requests it. Not a blocking decision.

---

## 10. Mod loader

### 10.1 Base game as mod

The base game is a content pack at `/data/base/`. It ships with the game and loads first, always.

The mod loader treats it identically to any user mod. This is the guarantee that ensures the mod system is real.

### 10.2 Load order

1. Scan `/data/base/` — always loaded, always first
2. Scan `/mods/` for user mods
3. Read each mod's `manifest.json`
4. Resolve dependencies (fail hard on missing / version conflict)
5. Load in dependency order
6. Each mod's content merges into global content registries by stable string ID
7. Mods with the same content ID override earlier loads (mod list order → priority)

### 10.3 Manifest format

```json
{
  "id": "mymod.extramonsters",
  "name": "Extra Monsters Pack",
  "version": "1.2.0",
  "author": "SomeModder",
  "description": "Adds 10 new monsters from D&D SRD",
  "requires": [
    { "id": "mankers.base", "version_min": "0.1.0" }
  ],
  "conflicts": [ ],
  "mp_safe": true
}
```

`mp_safe: true` means the mod modifies only data (Tier 1). Tier 2+ (scripting) mods will have additional flags.

### 10.4 Server-authoritative mod validation

On client join:
1. Server sends its loaded mod list (IDs + versions)
2. Client compares against its local mod list
3. Mismatch → server rejects join with a clear message ("Server has mymod.extramonsters 1.2.0, you have 1.1.0")
4. Match → proceed with world state sync

No auto-download of mods in v1. Steam Workshop integration is Tier 4 modding.

### 10.5 Content pipeline

Content types loaded from data files (`.tres` Godot resources or `.json`):

| Type | Location in mod | Loaded into |
|---|---|---|
| Items | `data/items/*.json` | `ItemRegistry` |
| Monsters | `data/monsters/*.json` | `MonsterRegistry` |
| Buildings | `data/buildings/*.json` | `BuildingRegistry` |
| Recipes | `data/recipes/*.json` | `RecipeRegistry` |
| Skills | `data/skills/*.json` | `SkillRegistry` |
| Classes | `data/classes/*.json` | `ClassRegistry` |
| NPC archetypes | `data/archetypes/*.json` | `ArchetypeRegistry` |
| Localization | `data/lang/*.json` | Localization system |

All entries are keyed by stable string IDs.

---

## 11. Testing

### 11.1 Test types

- **Unit tests (xUnit):** Test simulation logic in isolation. `WoodcuttingTest`, `SkillCapTest`, `NeedsDecayTest`. Fast, hundreds of them.
- **Integration tests (GdUnit4):** Test scenes and systems together in the Godot runtime. Fewer, slower, cover cross-system flows.
- **Headless server tests:** Run the server in headless mode with scripted inputs, assert world state after N ticks. This is how we test the netcode without needing two humans.
- **Determinism tests:** Run the same seed + inputs twice, assert identical world state.
- **Save round-trip tests:** Save → load → save → compare byte-for-byte. Enforces schema stability.

### 11.2 CI

Every push to `main` runs:
1. Build (`dotnet build`)
2. Unit tests (`dotnet test`)
3. Headless integration smoke test (`godot --headless -- --test`)
4. Localization key coverage check (fails if English key exists but German doesn't — warning, not error)
5. Client/server split enforcement check (grep-based)

CI is GitHub Actions in a private repo. Configuration deferred to when we have a repo.

### 11.3 Test coverage goals

- **Simulation logic:** high coverage. This is where bugs bite silently.
- **Netcode:** dedicated tests for prediction correctness and rollback.
- **UI code:** low coverage. Fast-changing, tested manually.
- **Save/load:** every schema version gets a golden-file test.

---

## 12. Build targets

### 12.1 Platforms

| Target | Priority | Notes |
|---|---|---|
| Windows 64-bit client | P0 | Primary dev platform |
| Linux 64-bit client | P1 | Supported via Godot's Linux export |
| Linux 64-bit dedicated server (headless) | P0 | Same binary, `--headless` flag |
| macOS client | P2 | Post-1.0 consideration |
| Steam Deck | P2 | Should Just Work via Linux target |

### 12.2 Distribution

- Steam for player-facing builds (client)
- Dockerized dedicated server for public server hosts (post-slice)
- Community: GitHub releases with Linux server tarballs

---

## 13. Performance targets

Loose targets for the v1 slice. Refined later.

- **Server tick time:** ≤ 25ms per tick at 20 Hz (leaves 25ms headroom). For 30 entities. Scales linearly to ~200 entities before needing optimization work.
- **Client frame time:** ≤ 16ms per frame at 60 FPS on a 5-year-old midrange PC.
- **Memory:** ≤ 2 GB for client, ≤ 500 MB for dedicated server.
- **Save file size:** ≤ 10 MB uncompressed for a full v1 slice world. Compression optional later.
- **Load time:** ≤ 5 seconds from menu click to playable world for the slice size.
- **Network bandwidth:** ≤ 30 KB/s per client at typical play.

---

## 14. Deferred technical decisions

Things that will be decided when we have the data to decide. Placeholder here so we don't forget.

1. Whether to migrate JSON save to binary format (perf optimization; only if load times exceed targets)
2. Whether to adopt a full ECS framework (Arch/Entitas) — only if profiling shows the naive approach is a bottleneck
3. Whether to move to `.po` gettext localization (community feature request-driven)
4. Whether to add Rust or C++ modules via GDExtension for sim hot paths
5. Whether to use Steam Datagram Relay vs plain Steam Networking Sockets — depends on GodotSteam API maturity
6. Anti-cheat approach for dedicated servers with public hosts (post-slice, if we ever ship publicly)
7. Whether to expose scripting mods (Tier 2) via Lua, C# assemblies, or a custom sandbox

---

## 15. Reference

- ADR-0001 through ADR-0022 in `docs/decisions/`
- `PRD.md` §8 for the summary version
- `docs/gdd/skills.md` for the concrete skill data model
- SkillSetRPG.com (design reference, not a technical dependency)
- Factorio Friday Facts #76 (lockstep architecture reference for the ADR-0022 rejection reasoning)
