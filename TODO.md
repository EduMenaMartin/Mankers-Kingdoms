# TODO — Active Task List

**One-line rule:** if it's in this file, it's actively being worked. If it's not, it's not.

New ideas go to `IDEAS_BACKLOG.md` first, get triaged, then land here if they're in scope.

---

## Current milestone: M0 — Project scaffolded

- [x] Initialize Godot 4 + C# project inside `/project/`
- [x] Configure `.csproj` for .NET 8+ (`RootNamespace=MankersKingdoms`)
- [x] Set up `.gitattributes` for LFS binary handling
- [ ] Initialize git repo, initial commit with docs — **Edu runs this**
- [ ] Configure Rider workspace — open `project/MankersKingdoms.sln` in Rider
- [ ] Add GodotSteam plugin (verify current version compatibility)
- [x] Add xUnit test project skeleton in `/project/tests/`
- [ ] Set up basic GitHub Actions CI (build + test on push) — optional for M0
- [x] Create `data/lang/en.json` with `"splash.title": "Mankers Kingdoms"`
- [x] Create main scene that opens window, reads splash title from localization, displays it
- [ ] Verify build runs on both Windows and Linux
- [ ] **M0 demo:** window opens with "Mankers Kingdoms" text on both dev PCs

---

## Blocked

Nothing currently.

---

## Done (this milestone)

- Project structure coherency fixed: moved Godot project from `project/Mankers Kingdoms/` → `project/`
- `.csproj` namespace fixed (`NewGameProject` → `MankersKingdoms`)
- `.sln` rewritten with correct references, includes both game + tests projects
- `project/project.godot`: assembly name fixed, main scene wired, C# feature tag present
- Rider run configs updated to `--path "./project/"` (were pointing at repo root)
- `.gitignore` extended with build output patterns
- `.gitattributes` extended with Git LFS binary patterns
- `project/scripts/shared/Loc.cs` — localization stub, pure .NET, testable
- `project/scripts/client/SplashScreen.cs` — reads en.json, sets Label text via Loc.T()
- `project/scenes/Main.tscn` — full-screen Control + centered Label, wired to SplashScreen
- `project/tests/MankersKingdoms.Tests.csproj` — xUnit skeleton, compiles shared/ directly
- `project/tests/Shared/LocTests.cs` — 3 tests covering Loc fallback, load, and missing-key cases
- `data/lang/en.json` — canonical repo-level lang file created

