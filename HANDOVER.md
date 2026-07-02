# Handover — Rolling Session Context

**Update this file at the end of every substantial session.**

The point: give the next session (whether it's you tomorrow or Claude Code opening cold) enough context to pick up without re-reading everything.

---

## Current status

**Milestone:** M0 (project scaffolding — in progress)
**Last session:** 2026-07-02 — coherency pass + Steps 2 & 3 complete
**Blockers:** None
**Awaiting:** Edu to open project in Godot editor and run the demo

---

## What was done last

### Structural coherency pass
- Godot project was in `project/Mankers Kingdoms/` (spaces in name, wrong path) — merged into `project/`
- Stray `project.godot`, `icon.svg`, `icon.svg.import` at repo root — deleted
- `.csproj` had `RootNamespace=NewGameProject` — fixed to `MankersKingdoms`
- `.sln` referenced `"New Game Project.csproj"` — rewritten as `MankersKingdoms.sln`
- Rider run configs pointed `--path "./"` (repo root) — fixed to `--path "./project/"`
- `.gitignore` and `.gitattributes` extended (build output patterns, Git LFS patterns)

### Step 2 — Localization + main scene
- `project/scripts/shared/Loc.cs` — static `Loc.T(key)` backed by `System.Text.Json`, no Godot dependency, `Reset()` for test isolation
- `project/scripts/client/SplashScreen.cs` — loads `res://data/lang/en.json`, sets Label text on `_Ready()`
- `project/scenes/Main.tscn` — full-screen Control + centered Label, wired to SplashScreen
- `project/data/lang/en.json` — `"splash.title": "Mankers Kingdoms"` (Godot-accessible via res://)
- `data/lang/en.json` — canonical repo-level copy per ADR-0012 architecture

### Step 3 — xUnit test skeleton
- `project/tests/MankersKingdoms.Tests.csproj` — Microsoft.NET.Sdk (not Godot SDK), compiles shared/ directly to avoid Godot runtime dependency
- `project/tests/Shared/LocTests.cs` — 3 tests: fallback brackets, load+retrieve, missing-key-after-load

## What's next

1. **Edu opens `project/project.godot` in Godot editor** — this regenerates `.godot/` cache, re-imports `icon.svg`, and validates the scene
2. **Edu runs `dotnet test` from `project/`** — should pass all 3 Loc tests
3. **Run the game** — window should open with "Mankers Kingdoms" centered on screen
4. **GodotSteam plugin** — next main task in M0
5. **CI setup** (optional for M0)

## Open questions

- `data/lang/en.json` at repo root vs `project/data/lang/en.json` inside the Godot project: for M0 both exist and the game loads from `res://`. The full content-loading architecture (loading from repo-root `data/`) is M1+ work. No ADR needed yet, but track in IDEAS_BACKLOG.
- Rider workspace: `.idea/` is at repo root. After Rider opens `project/MankersKingdoms.sln`, it may create `project/.idea/`. Leave both until they cause a conflict.

## Decisions needed from Edu

None currently.

---

## Session log

### 2026-07-02 — Foundation
- Design conversation from initial concept to locked decisions
- 22 ADRs written
- Repo scaffolding complete
- Ready to initialize Godot project

### 2026-07-02 — M0 coherency + Steps 2 & 3
- Godot project structure corrected
- Loc system + splash scene created
- xUnit test skeleton in place
- M0 demo ready for first run

