# Handover — Rolling Session Context

**Update this file at the end of every substantial session.**

---

## Current status

**Milestone:** M0 complete — ready to begin M1
**Last session:** 2026-07-02 — M0 fully closed out
**Blockers:** None
**Awaiting:** Edu to run final M0 commit and push to trigger CI

---

## What was done last

### M0 fully completed

**Project structure:**
- Godot 4.7 + C# project at `project/` (assembly: MankersKingdoms, .NET 8)
- Client/server/shared folder discipline in place
- Rider run configs correct (`--path "./project/"`, Godot at `F:\Godot_v4.7-stable_mono_win64\`)

**Localization:**
- `Loc.T(key)` in `project/scripts/shared/Loc.cs` — pure .NET, no Godot dependency
- `data/lang/en.json` (canonical) + `project/data/lang/en.json` (res:// accessible)
- ADR-0012 compliant from day one

**Scene:**
- `project/scenes/Main.tscn` — full-screen splash image (TextureRect, stretch=cover)
- `SplashScreen.cs` in client/ — loads Loc, inits Steam, pumps runCallbacks every frame

**GodotSteam:**
- GDExtension 4.20 at `project/addons/godotsteam/`
- `steam_appid.txt` (480) in `project/` for exported builds
- `steam_api64.dll` + `steam_appid.txt` must also be in Godot editor dir for editor play mode
- `status: 0` from steamInitEx = `k_ESteamAPIInitResult_OK` (success — raw Steamworks SDK enum)
- Steam ID confirmed printing on startup
- `runCallbacks()` pumped every frame — ready for M1 Steam features

**Tests:**
- `project/tests/MankersKingdoms.Tests.csproj` — 3 passing Loc tests
- Compiles `shared/` directly, no Godot runtime dependency

**CI:**
- `.github/workflows/ci.yml` — runs `dotnet test` on ubuntu-latest + windows-latest on push/PR
- No Godot install needed on runner (test project is pure .NET)
- Full Godot build CI deferred to M1

---

## What's next (M1)

Start `CURRENT_MILESTONE.md` for M1 when kicking off. Key M1 scope from VERTICAL_SLICE.md:
- Player avatar spawning (WASD movement, top-down camera)
- Basic world chunk loading
- First NPC settler
- Steam lobby creation (host) and join flow

## Open questions / decisions needed from Edu

- Does `steam_appid.txt` need to live permanently in the Godot editor dir, or should we document a setup script?
- M1 scope confirmation: confirm which VERTICAL_SLICE items are in M1 before starting

---

## Session log

### 2026-07-02 — Foundation
- Design complete, 22 ADRs, repo scaffolding

### 2026-07-02 — M0 complete
- Project structure, Loc, splash scene, GodotSteam, xUnit, CI
- M0 demo verified: splash + Steam ID on screen
