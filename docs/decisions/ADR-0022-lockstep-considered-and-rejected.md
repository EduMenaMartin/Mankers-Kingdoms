# ADR-0022: Deterministic lockstep considered and rejected

**Status:** Accepted
**Date:** 2026-07-02
**Deciders:** Edu + Claude (with input from Edu's IT contact)

## Context

An external technical contact suggested Factorio's deterministic lockstep networking model for our multiplayer. Lockstep is objectively excellent for a certain class of game, and dismissing it without proper analysis would be an error.

**How Factorio's lockstep works:**
- All players simulate the entire game identically each tick
- Only inputs are sent over the network, never game state
- Requires bit-exact determinism across all clients
- Any inconsistency causes a "desync" — client disconnects and re-downloads world
- Speed is limited by slowest player (buffer/latency setting compensates)
- Extremely low bandwidth (~few hundred bytes/second even for massive worlds)
- Cheat-resistant by design
- Enables input-log-as-replay for free

**Why it works for Factorio specifically:**
- Discrete inputs (click to place a belt, not continuous WASD + mouse aim)
- Massive world state (10,000+ entities) — state replication would need gigabits
- Slow simulation objects (biters don't even update until attacking)
- Wube built the C++ engine specifically for determinism, over many years
- Cooperative game with no time-critical actions
- Movement lag hidden with a "fake avatar" trick that works because movement doesn't cause immediate combat interaction

## Decision

**Deterministic lockstep is rejected for Mankers Kingdoms.** Authoritative host model (ADR-0005) is retained.

Four hard reasons:

1. **Real-time directional combat kills lockstep's input-lag tolerance.** Every action routes through the deterministic sim with mandatory buffer delay (~100–200ms). Factorio hides this for movement with a fake-avatar trick; it cannot be hidden for combat. Swing/block combat needs sub-100ms responsiveness.

2. **Godot 4 + C# is fundamentally hostile to determinism.** Godot Physics is not cross-machine bit-exact. `Dictionary<T>` and `HashSet<T>` iteration order varies across .NET runtimes and platforms. C# float ops are not identical Windows vs Linux vs Mac. Godot's NavigationAgent uses non-deterministic pathfinding. Making Godot deterministic would mean rewriting or wrapping half of what the engine gives us for free — losing the productivity advantage that made us pick Godot.

3. **Modding fragility.** Factorio's forums are full of mod-caused desyncs. Any mod that doesn't perfectly respect determinism rules breaks MP for everyone. For a game where modding is a design pillar (ADR-0009), this conflicts directly.

4. **The bandwidth argument doesn't apply at our scale.** Factorio's win is huge because of 10,000+ entities. Mankers Kingdoms at v1 has ~30 entities per scene; at 1.0 maybe a few hundred. Authoritative-server state replication is trivially bandwidth-efficient at our scale (target < 30 KB/s per client).

## Decision, part 2: keep determinism as a discipline

Even though we reject lockstep as a networking model, we retain **determinism where cheap** as a server discipline:

- Seeded RNG (never `System.Random`)
- Ordered iteration (never `Dictionary<T>` iteration in sim code)
- Fixed-timestep server ticks
- No wall-clock in simulation

This gives us:
- Server-side replay for debugging (record inputs + seed → replay same world)
- Save-file reproducibility (same seed + same mods = same world generation)
- Mod behavior consistency across sessions

Without paying lockstep's costs.

## Consequences

**Positive:**
- Real-time combat feels correct (no input lag)
- Godot 4 development is efficient (we use engine features as designed)
- Modding compatibility is straightforward
- Determinism discipline preserved for its debugging and content benefits

**Negative:**
- Slightly higher bandwidth than lockstep (~30 KB/s vs ~1 KB/s)
- Save format is a state snapshot (larger) not an input log (tiny)
- Cheating in dedicated servers requires other mitigations (server validation, not "you can't fake determinism")

**Accepted:** the bandwidth and save-size trade-off is trivially manageable at our scale.

## When would we revisit this?

If Mankers Kingdoms pivots toward "less real-time combat, more automation" and world state grows to thousands of entities, lockstep becomes viable again. Unlikely given current design pillars, but worth noting.

## Alternatives considered

Only lockstep vs. authoritative-server was the real question. Peer-to-peer is worse than both.

## References

- ADR-0005 (authoritative host)
- PRD.md §8
- ARCHITECTURE.md §4, §7
- Factorio Friday Facts #76 (Wube's lockstep architecture explanation)
- Factorio wiki: Desynchronization
- Egregoria project (Rust ECS game): documented determinism requirements
