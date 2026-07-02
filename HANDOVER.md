# Handover — Rolling Session Context

**Update this file at the end of every substantial session.**

---

## Current status

**Milestone:** M0 complete — ready to begin M1
**Last session:** 2026-07-02 — M0 fully closed out
**Blockers:** None
**Awaiting:** Edu to run final commit + push, then confirm CI green

---

## What was done this session

- **Structural coherency pass:** Godot project was in `project/Mankers Kingdoms/` (spaces in name, wrong path) — merged into `project/`; stray `project.godot`, `icon.svg`, `icon.svg.import` at repo root deleted; `.csproj` namespace fixed (`NewGameProject` → `MankersKingdoms`); `.sln` rewritten with correct references; Rider run configs fixed to `--path "./project/"`
- **Localization stub:** `project/scripts/shared/Loc.cs` — `Loc.T(key)` backed by `System.Text.Json`, `Reset()` for test isolation, no Godot dependency; `data/lang/en.json` + `project/data/lang/en.json` created
- **Splash scene:** `project/scenes/Main.tscn` — full-screen TextureRect with `Mankers Kingdoms.png` (stretch=cover); Label removed (title embedded in artwork)
- **GodotSteam GDExtension 4.20:** installed via Godot AssetLib; confirmed Steam initializes (`status: 0` = `k_ESteamAPIInitResult_OK` — raw Steamworks SDK enum, NOT failure); Steam ID printed on startup; `runCallbacks()` pumped every frame
- **xUnit test skeleton:** `project/tests/MankersKingdoms.Tests.csproj` — 3 Loc tests pass; compiles `shared/` directly, no Godot runtime needed on CI
- **GitHub Actions CI:** `.github/workflows/ci.yml` — `dotnet test` on `ubuntu-latest` + `windows-latest` on push/PR
- **Config hygiene:** `.gitignore` extended (build output, `steam_appid.txt`); `.gitattributes` extended (LFS for images, audio, native binaries)

## What's next (top 3)

1. **Push to main** — triggers first CI run; confirm both ubuntu + windows jobs green
2. **Start M1** — create `CURRENT_MILESTONE.md`, confirm M1 scope from `VERTICAL_SLICE.md` before any code
3. **M1 first task** — player avatar spawning: WASD movement, top-down camera, single scene

## Blocked

Nothing.

## Decisions needed from Edu before next session

- **M1 scope confirmation:** VERTICAL_SLICE.md lists M1 as "playable world + avatar." Confirm exact deliverable before I start writing systems.
- **steam_appid.txt editor setup:** currently requires manually copying `steam_api64.dll` + `steam_appid.txt` to the Godot editor dir on each dev machine. Want a setup script for onboarding, or just document it in README?

---

## Session log

### 2026-07-02 — Foundation
- Design complete, 22 ADRs, repo scaffolding

### 2026-07-02 — M0 complete
- Full coherency pass on Godot project structure
- Loc system, splash scene with artwork, GodotSteam GDExtension wired
- xUnit skeleton with 3 passing tests
- GitHub Actions CI on Linux + Windows
- M0 demo verified: splash image on screen, Steam ID printed on startup
