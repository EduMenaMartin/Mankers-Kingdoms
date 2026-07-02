# ADR-0012: Localization architecture

**Status:** Accepted
**Date:** 2026-07-02
**Deciders:** Edu + Claude

## Context

Games without localization from day one struggle to add it later — string literals accumulate throughout the codebase, and each one is a bug waiting to be found. Retrofitting a localization system means auditing every UI file and every game event message.

We want:
- Multiple language support from launch (English canonical; German and Spanish target given team language coverage)
- Community-added languages via mods
- No string literals in gameplay code
- Graceful fallback when a translation is missing

## Decision

**Externalize all player-facing strings to per-language JSON files** at `/data/lang/en.json`, `/data/lang/de.json`, etc.

All UI and game-event code accesses strings via `Loc.T("key.path")` — never a hardcoded literal. Format substitutions via `Loc.T("combat.damage_dealt", damage, targetName)`.

**Enforcement:** grep-based CI check fails builds that contain untranslated user-facing string literals in `/scripts/`.

**Modding:** mods drop `.json` files into their `data/lang/` folder. Keys are merged into the global namespace. Modders can override base game strings by defining the same key.

**Fallback chain:** missing key in current language → English → key name in brackets (`[menu.start_solo]`) so translators immediately see gaps.

**Language file addition** requires no code change — dropping a `fr.json` into any mod's lang folder makes French appear in the language dropdown.

## Consequences

**Positive:**
- Localization is a solved problem from day one
- Community can add new languages without touching code
- Modders can localize their content in their own mod
- No accumulated string-literal debt

**Negative:**
- Every UI addition takes an extra step (add key + English text before wiring UI)
- CI enforcement adds a small friction

## Alternatives considered

- **Godot's built-in `.po` gettext system.** Considered — more powerful (plurals, context), less approachable for editing. May migrate later if community requests.
- **CSV translation files.** Rejected — awkward for programmers, no nesting.
- **No structured localization; ship English only.** Rejected — retrofitting is a known failure mode.

## References

- PRD.md §8
- ARCHITECTURE.md §9
