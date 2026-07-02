# TODO — Active Task List

**One-line rule:** if it's in this file, it's actively being worked. If it's not, it's not.

New ideas go to `IDEAS_BACKLOG.md` first, get triaged, then land here if they're in scope.

---

## Current milestone: M0 — Project scaffolded ✅

All M0 tasks complete. Ready to begin M1.

---

## Blocked

Nothing currently.

---

## Done (M0)

- [x] Initialize Godot 4.7 + C# project inside `/project/`
- [x] Configure `.csproj` for .NET 8+ (`RootNamespace=MankersKingdoms`)
- [x] Set up `.gitattributes` for LFS binary handling (png, jpg, audio, 3D, native DLLs)
- [x] Configure Rider workspace (run configs pointing to `./project/`)
- [x] Add GodotSteam GDExtension 4.20 via Godot AssetLib (not module build)
- [x] Add xUnit test project skeleton in `/project/tests/`
- [x] Set up GitHub Actions CI — tests on ubuntu-latest + windows-latest
- [x] Create `data/lang/en.json` with `"splash.title": "Mankers Kingdoms"`
- [x] Create main scene with splash image + Loc.T() wired
- [x] Verify build runs on both Windows (local) and Linux (CI ubuntu runner)
- [x] **M0 demo:** window opens with splash image; Steam initialized (status 0 = OK), Steam ID printed

