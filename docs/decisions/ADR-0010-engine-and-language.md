# ADR-0010: Engine and language — Godot 4 + C# + GodotSteam

**Status:** Accepted
**Date:** 2026-07-02
**Deciders:** Edu + Claude

## Context

Engine choice for a game like this is a high-cost decision. Migrating engines mid-project is essentially starting over.

Requirements:
- Strong 2D / top-down support (this is a 2D-adjacent game)
- Multiplayer-capable
- C# support (Edu's language of comfort, better for heavy simulation than GDScript)
- Free of onerous licensing (Unity's 2023 runtime fee saga made publisher-friendliness a real concern)
- Steam integration path (Steamworks for networking)
- Reasonable tooling

Candidates evaluated:
- **Godot 4** — first-class 2D pipeline, MIT license, C# supported via .NET, MultiplayerAPI + GodotSteam for networking
- **Unity 2023+** — huge asset store, mature C#, Mirror/Fish-Net for networking, but runtime-fee overhang
- **Unreal 5** — networking excellent, but optimized for 3D shooters; top-down 2D fights the engine
- **Bevy (Rust)** — ECS-first, elegant, but immature ecosystem and steep learning curve

## Decision

**Godot 4 + C# / .NET + GodotSteam** for networking.

IDE recommendation: JetBrains Rider (best Godot C# support, official Claude Code plugin, free for non-commercial). Fallback: VS Code with C# Dev Kit.

## Consequences

**Positive:**
- Godot's 2D-native pipeline suits this game
- MIT license removes commercial risk
- C# gives us .NET's ecosystem for tooling, testing (xUnit), and libraries
- GodotSteam handles Steam networking (P2P/relay/dedicated) via well-maintained bindings
- Free and open source — no vendor lock-in

**Negative:**
- Godot's C# support is less mature than Unity's (though rapidly improving in Godot 4.x)
- GodotSteam is a community project, not first-party — must verify version compatibility carefully
- Smaller talent pool if we ever hire (Godot devs less common than Unity/Unreal)
- Godot's default systems (Physics, Navigation, Dictionary iteration) are not cross-machine deterministic — matters for our determinism-where-cheap discipline

## Alternatives considered

- **Unity + Fish-Net.** Rejected primarily for licensing overhang and 2D-as-afterthought design.
- **Unreal 5.** Rejected — wrong tool for top-down 2D-adjacent simulation.
- **Bevy.** Rejected — Rust learning curve + immature ecosystem for a first game project.
- **Custom engine.** Considered briefly, rejected — Wube spent years on Factorio's engine.

## References

- PRD.md §8
- ARCHITECTURE.md §2
- Design conversation session 1
