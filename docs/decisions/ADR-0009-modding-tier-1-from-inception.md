# ADR-0009: Modding Tier 1 from inception

**Status:** Accepted
**Date:** 2026-07-02
**Deciders:** Edu + Claude

## Context

Modding turns a shipped game into a platform. Community mods extend content, fix balance, add languages, and keep games alive years past publisher support. But mod support falls on a spectrum:

- **Tier 1 — Data mods:** modders add/replace items, monsters, buildings, recipes, translations via data files. No code execution.
- **Tier 2 — Scripting mods:** modders write code hooking into game events (Lua sandbox, C# plugins, etc.).
- **Tier 3 — Total conversions:** modders can replace entire systems. Requires very clean architecture and often a modding SDK.
- **Tier 4 — Workshop integration:** Steam Workshop, dependency resolution, signed manifests, MP compatibility.

Retrofitting modding is famously hard. Games designed without it (Cities: Skylines base pre-mods, many indie games) require major refactors to add it later. Games designed with it from day one (Minecraft, RimWorld, Factorio) got community ecosystems that dwarfed publisher content.

Costs and benefits at each tier:
- Tier 1 is nearly free if we're disciplined: everything content-shaped lives in data files, IDs are stable strings.
- Tier 2 requires an event bus and either a sandboxed API or a plugin loader. Non-trivial.
- Tier 3 requires a full modding SDK.
- Tier 4 requires Workshop integration and a real community process.

## Decision

Support **Tier 1 (data mods) from day one of implementation**. Do not attempt Tier 2 or higher during the vertical slice.

Enforced by:
- All content defined in `data/base/**` as JSON or Godot resources — no C# constants for content
- Stable string IDs for all content (`monster.goblin.scout`, not integer indices)
- Mod loader boots by scanning `/data/base/` first, then `/mods/`
- Base game loads through the mod loader (not a special path)
- Server-authoritative mod validation on client join
- Missing content in saves degrades gracefully

Tiers 2, 3, 4 are on the roadmap (PRD §6.3, §6.4) but explicitly out of scope until v1 slice succeeds.

## Consequences

**Positive:**
- The community can extend the game from launch
- Content iteration during dev is faster (edit JSON, reload, no rebuild)
- Base game code stays clean because it can't cheat and hardcode content
- Save-format robustness improves (missing content becomes a solved problem, not an emergent bug)

**Negative:**
- 10–15% extra design overhead for every content system to keep it data-driven
- No exposed scripting means some plugin-shaped mods have to wait

## Alternatives considered

- **Skip Tier 1, add all modding later.** Rejected — well-documented failure mode.
- **Start with Tier 2 (scripting) directly.** Rejected — much larger commitment; wait until data-mod ecosystem proves demand.

## References

- PRD.md §4.11, §6
- ARCHITECTURE.md §10
- SKILLS.md §5 (modding surface example)
