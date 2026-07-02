# Changelog

Notable changes to the project. Not a git log — a human-facing history of what shipped in each milestone or version.

Follows [Keep a Changelog](https://keepachangelog.com/) conventions loosely.

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
