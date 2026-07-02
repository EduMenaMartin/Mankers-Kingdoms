# ADR-0005: Authoritative host multiplayer

**Status:** Accepted
**Date:** 2026-07-02
**Deciders:** Edu + Claude

## Context

Coop game networking has three main models:
- **Deterministic lockstep** (Factorio, StarCraft): all clients simulate identically; only inputs are sent.
- **Authoritative server** (Valheim, most modern coop): server maintains state; clients predict/interpolate.
- **Peer-to-peer** (older games): each client trusts others' state updates.

Lockstep is analyzed and rejected in ADR-0022. Peer-to-peer has cheating and consistency issues.

## Decision

Authoritative host / server model. The server (whether player-hosted or dedicated) is the single source of truth for game state. Clients send inputs and commands, receive state snapshots and events, and predict/interpolate for smooth visual output.

Combined with ADR-0002, the host and dedicated server share code — the host is a dedicated server that also runs a local client.

Target scale: 2–6 concurrent players, cooperative only (no PvP in v1).

## Consequences

**Positive:**
- Well-understood, well-supported architecture
- Compatible with Godot's built-in `MultiplayerAPI` and GodotSteam
- Cheating protection is straightforward (server validates client actions)
- Enables dedicated server support (ADR-0002)
- Modding compatibility is straightforward — mods declare their content and the server validates client mod lists on join

**Negative:**
- Bandwidth scales with world state visible per client (~30 KB/s target)
- Server load scales linearly with entities being simulated
- Some client-side prediction complexity for local player movement

## Alternatives considered

- **Deterministic lockstep** — see ADR-0022 for full analysis and rejection reasoning
- **P2P with rotating host** — rejected: no dedicated server path, inconsistent authority

## References

- PRD.md §4.2, §8
- ARCHITECTURE.md §4
- ADR-0002, ADR-0022
