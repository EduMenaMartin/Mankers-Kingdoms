# ADR-0024: Faction allegiance system

**Status:** Accepted
**Date:** 2026-07-05
**Deciders:** Edu + Claude

## Context

During M4 testing, monsters from different nests were receiving damage from projectiles fired by other monsters — a bandit archer's arrow could hit a wolf or goblin that wandered into the path. The immediate cause was that `ProjectileSystem` used `CollisionMask = 32u | 64u` (players + monsters) for sphere hit detection, with no concept of which side a given entity is on.

An ad-hoc patch (`if originIsMonster && targetIsMonster: skip damage`) was applied first, but it was a knowledge-of-internals hack: it used the `MONSTER_ID_THRESHOLD` (10001) sentinel rather than any real allegiance model, and it would incorrectly skip damage even between monsters that *should* fight each other (e.g., wolves vs bandits in the same area).

The correct fix is to give every spawned group its own faction and gate all combat on faction relationships — matching the GDD design in `docs/gdd/factions.md`.

## Decision

**Implement a two-layer faction allegiance system**, as specified in `docs/gdd/factions.md`.

**Layer 1 — type-level defaults** (`FactionService.TypeDefault`): relationships are defined once per `FactionType` pairing (`monster_nest`, `village`, `player_settlement`). The full table:

| Type A | Type B | Default |
|---|---|---|
| monster_nest | monster_nest (different instance) | **Hostile** |
| monster_nest | village | **Hostile** |
| monster_nest | player_settlement | **Hostile** |
| village | village | Neutral |
| village | player_settlement | Neutral |
| player_settlement | player_settlement | **Allied** |

**Layer 2 — instance overrides** (`FactionService.TrySetOverride`): specific faction-id pairs can be given an explicit relationship that takes precedence over the type default. Not used in v1 content but the mechanism is in place for authored alliances/rivalries.

**§4 hard rule**: `TrySetOverride` rejects — in code, not just as a data default — any attempt to set two `PlayerSettlement` factions to `Hostile`. This enforces `PRD.md §4.2` (no PvP in v1) even against mod data.

**Faction assignment**: each nest spawned by `NestSystem` gets a unique `faction_id` (`"faction.nest.{nestId}"`), registered with `FactionService` as `MonsterNest`. All players share `FactionService.PLAYER_FACTION_ID = "faction.player.settlement"`, registered as `PlayerSettlement`. The `FactionId` field is carried on `NestData` and `MutableMonster`.

**Combat gates**:
- `ProjectileSystem`: replaces the MONSTER_ID_THRESHOLD patch with `FactionService.IsHostile(originFactionId, targetFactionId)`. Non-hostile hits are discarded (arrow disappears, no damage).
- `MonsterSystem.TickIdle`: aggro scan is wrapped in an `IsHostile` check against the player faction. Currently always true for monster nests, but the gate is live for future global overrides (world hostility slider, PRD.md §4.10).
- `CombatSystem` (player melee): not gated — whether players can attack neutral-faction villagers is an open design question (`docs/gdd/factions.md §10.1`). Resolved after M6 playtesting.

## Consequences

**Positive:**
- Monster friendly fire is fixed correctly: a wolf and a bandit from different nests are in different `monster_nest` factions, which are Hostile to each other per Layer 1. The projectile gate stops friendly fire for *same-nest* pairs (same faction → Allied) and would allow monster-vs-monster damage for *different-nest* pairs if the AI ever targets them.
- The no-PvP rule is enforced in code, not just in data — a future mod cannot accidentally or intentionally enable player-vs-player combat in v1.
- The system is forward-compatible: world hostility slider (PRD.md §4.10) becomes a global Layer 1 override; conquest (ADR-0015) becomes a faction transfer; recruitment (§5) is already a faction transfer.
- `FactionService` is pure C#, no Godot dependency, fully tested in xUnit.

**Negative:**
- Monster-vs-monster melee AI combat is not yet implemented (TickIdle only scans player nodes). The faction gate exists at target selection but won't trigger monster-vs-monster fights until the AI scan is expanded. This is intentional — expanding AI target pools is a gameplay change that needs its own milestone entry.
- `NestData` gains a `FactionId` field. Existing callers that construct `NestData` directly (tests, `NestGenerator`) are unaffected because the field defaults to `""`.

## Alternatives considered

1. **Keep the MONSTER_ID_THRESHOLD patch** — simple, but incorrect for monster-vs-monster interactions and a violation of the content-is-data rule (hardcoding entity ID ranges as a proxy for allegiance). Rejected.
2. **Per-species factions** (all wolves = one faction, all goblins = one faction) — contradicts the GDD's explicit decision that two Goblin nests are different factions. Rejected.
3. **Faction as a field on `MonsterData`** (species-level) — same problem as option 2, plus species and faction are orthogonal. Rejected.
4. **Gate combat in a single central method** rather than at each call site — cleaner in theory, but `ProjectileSystem` and `MonsterSystem` call their own combat paths. Adding a shared gate would require a new abstraction layer that isn't justified at current scale. Deferred.

## References

- `docs/gdd/factions.md` — full design specification
- `PRD.md` §4.2 — no PvP in v1 (locked)
- `PRD.md` §4.10 — world hostility slider (roadmap)
- ADR-0009 — stable string IDs (faction IDs use the same convention)
- ADR-0015 — conquest (faction transfer mechanism)
- `VERTICAL_SLICE.md` §3.6 — hostile/non-hostile bestiary categorization
- `docs/gdd/combat.md` §2.5 — faction check composition with existing geometry gate
