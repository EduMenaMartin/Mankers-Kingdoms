# Handover — Rolling Session Context

**Update this file at the end of every substantial session.**

---

## Current status

**Milestone:** M4 — Combat and monsters (started 2026-07-04)
**Last session:** 2026-07-04 — M3 closed; M4 planned and scoped
**Blockers:** None
**Awaiting:** Implementation — start with Phase 1 (HealthSystem)

---

## What was done this session (2026-07-04 — M3 close-out)

- **Settlement permissions GDD (`docs/gdd/settlements.md`):** §1 locked permission table wired into code. `SettlementSystem.RequestPlaceBuilding` now calls `IsFounder(sender)` explicitly with §1.3 reference comment.
- **"Not enough materials" HUD feedback:** server sends `ClientNotifyRejection` RPC to the requesting peer on resource check failure. Routes through `LocalState.NotifyRejection()` (shared/ event) to avoid server/ → client/ import. `PlacementController` subscribes to the event and flashes a label for 2.5 s at top-center of screen — no editor task required (label created programmatically).
- **Loc key added:** `hud.build.no_materials` → "Not enough materials."
- **TODO.md + HANDOVER.md updated:** M3 marked complete.

## What was done previously (2026-07-03 — M2 + M3)

- **M2:** procedural terrain, day/night, trees, tree chopping, wood drops
- **M3:** Kingdom Marker, settlement buildings (Shelter, Storage, Workbench, Cooking Fire), inventory system, needs (hunger/rest), food loop (bushes → berries → cook → eat), class-building permission table in GDD

## What was done this session (2026-07-03 — M1 implementation)

- **Onboarding scripts:** `tools/setup-steam-dev.ps1` + `tools/setup-steam-dev.sh` — locate Godot editor, copy `steam_api64.dll` / `libsteam_api.so`, write `steam_appid.txt` (appid 480 / Spacewar placeholder)
- **Shared DTOs:** `scripts/shared/MsgPlayerInput.cs`, `MsgPlayerState.cs` (no Godot deps, testable), `GameSession.cs` (scene-to-scene intent bridge)
- **Server layer:** `scripts/server/NetworkManager.cs` (ENet host/join, reads `GameSession.Intent` on `_Ready`), `scripts/server/PlayerSystem.cs` (spawns/despawns `Player.tscn` via reliable RPC on peer connect/disconnect, `SortedDictionary` for ordered iteration)
- **Client layer:** `scripts/client/PlayerController.cs` (WASD input, client-side prediction, `ReceiveInput` RPC → server, `UpdateState` RPC → all clients, remote lerp, server reconciliation), `scripts/client/MainMenuController.cs` (menu logic + runtime WASD action registration), `scripts/client/OptionsMenuController.cs` (audio + graphics stubs)
- **Scenes:** `MainMenu.tscn`, `OptionsMenu.tscn`, `GameWorld.tscn` (50×50 green plane + directional light + Players container + NetworkManager + PlayerSystem nodes), `Player.tscn` (CharacterBody3D + CapsuleMesh + CapsuleShape3D + Camera3D at -60° pitch)
- **Splash update:** `SplashScreen.cs` gets a 2-second auto-transition to `MainMenu.tscn`
- **Tests:** `tests/Shared/MessageTests.cs` — 8 new tests; 11/11 passing total
- **Loc keys:** 17 entries in `data/lang/en.json`

## What was done previously



- **Structural coherency pass:** Godot project was in `project/Mankers Kingdoms/` (spaces in name, wrong path) — merged into `project/`; stray `project.godot`, `icon.svg`, `icon.svg.import` at repo root deleted; `.csproj` namespace fixed (`NewGameProject` → `MankersKingdoms`); `.sln` rewritten with correct references; Rider run configs fixed to `--path "./project/"`
- **Localization stub:** `project/scripts/shared/Loc.cs` — `Loc.T(key)` backed by `System.Text.Json`, `Reset()` for test isolation, no Godot dependency; `data/lang/en.json` + `project/data/lang/en.json` created
- **Splash scene:** `project/scenes/Main.tscn` — full-screen TextureRect with `Mankers Kingdoms.png` (stretch=cover); Label removed (title embedded in artwork)
- **GodotSteam GDExtension 4.20:** installed via Godot AssetLib; confirmed Steam initializes (`status: 0` = `k_ESteamAPIInitResult_OK` — raw Steamworks SDK enum, NOT failure); Steam ID printed on startup; `runCallbacks()` pumped every frame
- **xUnit test skeleton:** `project/tests/MankersKingdoms.Tests.csproj` — 3 Loc tests pass; compiles `shared/` directly, no Godot runtime needed on CI
- **GitHub Actions CI:** `.github/workflows/ci.yml` — `dotnet test` on `ubuntu-latest` + `windows-latest` on push/PR
- **Config hygiene:** `.gitignore` extended (build output, `steam_appid.txt`); `.gitattributes` extended (LFS for images, audio, native binaries)

## What's next (top 3)

1. **M4 Phase 1** — `HealthData`, `MsgEntityHealth`, `HealthSystem` (server), `HealthHUD` (client), `LocalState` HP fields. Tests for HealthData. Editor: add nodes to GameWorld.tscn.
2. **M4 Phase 2** — `WeaponData`, `WeaponRegistry`, `CombatSystem`, `MeleeController`. Tests for WeaponRegistry.
3. **M4 Phase 3** — `ProjectileState`, `ProjectileSystem`, `BowController`. Arrow crafting at Workbench.

## Blocked

Nothing.

## Decisions needed from Edu before next session

- **Next milestone:** M2 (terrain + tree chopping, first gameplay loop) or M1.5 (GodotSteam P2P to replace ENet, Steam friend invite flow)?

---

## Session log

### 2026-07-02 — Foundation
- Design complete, 22 ADRs, repo scaffolding

### 2026-07-03 — M1 complete
- Debugged camera (wrong transform row-major vs column-major), player spawning, material rendering
- Locked `docs/scene_workflow.md` rule: Claude Code never edits .tscn files; editor owns scenes
- M1 demo verified: two instances, 127.0.0.1, both capsules visible, both move smoothly

### 2026-07-02 — M0 complete
- Full coherency pass on Godot project structure
- Loc system, splash scene with artwork, GodotSteam GDExtension wired
- xUnit skeleton with 3 passing tests
- GitHub Actions CI on Linux + Windows
- M0 demo verified: splash image on screen, Steam ID printed on startup
