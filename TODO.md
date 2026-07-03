# TODO — Active Task List

**One-line rule:** if it's in this file, it's actively being worked. If it's not, it's not.

New ideas go to `IDEAS_BACKLOG.md` first, get triaged, then land here if they're in scope.

---

## M0 — Project scaffolded ✅ COMPLETE (2026-07-02)

- [x] Initialize Godot 4.7 + C# project inside `/project/`
- [x] Configure `.csproj` for .NET 8+ (`RootNamespace=MankersKingdoms`)
- [x] Set up `.gitattributes` for LFS binary handling
- [x] Configure Rider workspace (run configs pointing to `./project/`)
- [x] Add GodotSteam GDExtension 4.20 via Godot AssetLib
- [x] Add xUnit test project skeleton in `/project/tests/`
- [x] Set up GitHub Actions CI (dotnet test on ubuntu + windows)
- [x] Create `data/lang/en.json` with `"splash.title": "Mankers Kingdoms"`
- [x] Create main scene with splash image + Loc system wired
- [x] Verify tests pass on Linux (CI ubuntu runner)
- [x] **M0 demo:** window opens with splash image; Steam ID confirmed on startup

---

## M1 — Main menu and two clients see each other (in progress)

**Goal:** From menu, one player hosts, other joins over LAN, both run around an empty plane and can see each other move smoothly.
**Transport:** ENet (LAN bring-up per ARCHITECTURE.md §4.2); GodotSteam SteamMultiplayerPeer replaces this in a later milestone.

### Onboarding
- [x] `tools/setup-steam-dev.ps1` — copy Steam DLL + write steam_appid.txt to Godot editor dir (Windows)
- [x] `tools/setup-steam-dev.sh` — same for Linux/macOS

### Foundation
- [x] Create `CURRENT_MILESTONE.md` for M1
- [x] Add M1 loc keys to `data/lang/en.json`
- [x] `project/scripts/shared/GameSession.cs` — session intent bridge between menu and game world
- [x] `project/scripts/shared/MsgPlayerInput.cs` — client→server movement DTO
- [x] `project/scripts/shared/MsgPlayerState.cs` — server→clients position snapshot DTO

### Networking
- [x] `project/scripts/server/NetworkManager.cs` — ENet host/join, peer lifecycle signals, reads GameSession.Intent on _Ready
- [x] `project/scripts/server/PlayerSystem.cs` — spawns/despawns Player.tscn nodes on peer connect/disconnect via RPC

### Player
- [x] `project/scripts/client/PlayerController.cs` — CharacterBody3D; WASD client prediction; sends input to server; reconciles corrections; interpolates remote players
- [x] `project/scenes/Player.tscn` — CharacterBody3D + CapsuleMesh + CapsuleShape3D + Camera3D (top-down, active only for local player)

### UI
- [x] `project/scripts/client/MainMenuController.cs` — Start Solo / Host / Join / Options / Exit; registers WASD input actions
- [x] `project/scripts/client/OptionsMenuController.cs` — master volume, graphics quality, language dropdown (EN only)
- [x] `project/scenes/MainMenu.tscn`
- [x] `project/scenes/OptionsMenu.tscn`
- [x] Update `project/scripts/client/SplashScreen.cs` — add 2-second timer → transition to MainMenu

### World
- [x] `project/scenes/GameWorld.tscn` — StaticBody3D plane + DirectionalLight3D + Players container + NetworkManager + PlayerSystem nodes

### Tests
- [x] `project/tests/Shared/MessageTests.cs` — MsgPlayerInput, MsgPlayerState, GameSession tests (8 tests, 11 total passing)

### Demo gate
- [x] M1 demo: host instance + join instance over LAN; both capsules visible; both move smoothly → mark M1 complete

---

## M1 ✅ COMPLETE (2026-07-03)

---

## Blocked

Nothing.

