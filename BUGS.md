# BUGS — Known Bugs

**Format:** one entry per bug. Newest at the top.

## [P1] Death drop missing — no inventory drop on death, no map marker (2026-07-18)

**Milestone found:** M9 (playtest)
**Reproduce:** Die in combat → respawn → check ground at death position → no dropped items. Open minimap and world map → no red X marker at death location.
**Expected:** On death, inventory items drop as a pickup at death position; a red X appears on minimap and world map. Player can retrieve items by walking to the marker.
**Actual:** Neither the physical drop nor the map marker appears. "Gone again" suggests a regression — was functional at some earlier point.
**Suspected causes (needs investigation):**
1. `HealthSystem.ClientShowDeathMarker` RPC may not be firing, or the marker position is wrong (e.g. Vector3.Zero passed as death position).
2. The physical item drop node may not be spawning (Phase 5 deferred? or `ServerSpawnItemDrop` RPC not called / not wired to a scene).
3. A recent code change may have broken the death→drop→marker sequence in `HealthSystem`.
**Next step:** Read `HealthSystem.cs` death handling path and trace whether `ClientShowDeathMarker` is called, what position it receives, and whether item drop nodes are being spawned.

Status: OPEN

---

## [P1] Fog of war not visible on minimap or world map (2026-07-17)

**Milestone found:** M8, discovered M9
**Reproduce:** Start a new game, walk around for 30+ seconds, open world map (M) — terrain is fully visible with no fog overlay; minimap also shows full terrain without fog.
**Expected:** Unexplored areas blacked out; explored-but-not-currently-visible areas shown in dark grey; visible area (within ~40m) fully revealed.
**Two root causes:**

1. **FogSystem node missing from GameWorld.tscn (editor task never confirmed done).**
   The M8 session logged an editor task: "Add `FogSystem` node (script: `res://scripts/server/FogSystem.cs`) to `GameWorld.tscn` after VillageSystem and before SaveSystem." This was never marked complete. Without the node, `FogSystem._Process` never runs → no fog data is ever generated or broadcast → `LocalState.FogSnapshot` stays null → `WorldMapScreen._fogTex` stays null → no overlay drawn.
   **Fix (editor):** Add `FogSystem` node to `GameWorld.tscn` (same as BushSystem / HerbSystem — plain `Node`, script `res://scripts/server/FogSystem.cs`).

2. **MinimapHUD has no fog support at all.**
   `WorldMapScreen` has full fog overlay code (`_fogTex` field, `BakeFogTexture`, `FogChanged` subscription, draws fog layer between terrain and entities in `_Draw`). `MinimapHUD` has none of this — no subscription, no texture field, no draw call. Even after fixing #1, the minimap will never show fog.
   **Fix (code):** Mirror the WorldMapScreen fog pattern into MinimapHUD: add `ImageTexture? FogTex` to `_DrawControl`, subscribe `LocalState.FogChanged` in `_Ready`, unsubscribe in `_ExitTree`, call `BakeFogTexture` on change, pass `_fogTex` to `_draw.FogTex` each `_Process` frame, draw it in `_DrawControl._Draw` between terrain and entities.

Status: FIXED (2026-07-17) — FogSystem node confirmed present (Edu). MinimapHUD fog overlay + fog-gated nest visibility added. WorldMapScreen nest visibility fog-gated. `FogOfWarData.IsDiscovered()` added as shared helper.

---

## [P2] Every new game generates the same world — WorldSeed never randomised (2026-07-15)

**Milestone found:** M2 (terrain), discovered M9
**Reproduce:** Start a new game → play → quit → start another new game → identical world layout.
**Expected:** Each new game produces a different procedural world.
**Actual:** `GameSession.WorldSeed` is initialised to `42u` and never changed. `CharacterCreateScreen.OnConfirm()` stamps a unique `SaveName` but never sets `WorldSeed`. All generators (Terrain, Tree, Bush, Nest, HerbPatch) read `GameSession.WorldSeed` and always produce identical output.
**Fix:** Added `GameSession.WorldSeed = (uint)GD.Randi();` in `CharacterCreateScreen.OnConfirm()` before `ChangeSceneToFile`. Debug log removed.

Status: FIXED (2026-07-15)

---

## [P1] Grey screen + player at top-left of minimap after Load Game (2026-07-14)

**Milestone found:** M8
**Reproduce:** Start solo game → play → quit to main menu → Load Game → grey 3D viewport, player dot at minimap top-left corner.
**Expected:** World renders normally with camera following the player.
**Actual:** `NetworkManager.Close()` (called in `_ExitTree`) set `Multiplayer.MultiplayerPeer = null`. On the next GameWorld load in Solo mode, no peer is created, so the peer stays null. In Godot 4, `Multiplayer.GetUniqueId()` returns **0** when the peer is null (not 1). This causes `IsMultiplayerAuthority()` to return false (authority=1 ≠ unique=0), so `_isLocalPlayer = false`, `_camera.MakeCurrent()` is never called → grey screen. `MinimapHUD` also looks for `Player_0` which doesn't exist, leaving `_playerMapPos = (0,0)` = map top-left.
**Fix:** `NetworkManager.Close()` now resets to `new OfflineMultiplayerPeer()` instead of `null`, keeping `GetUniqueId() = 1` across scene reloads.

Status: FIXED (2026-07-14)

---

## [P2] Starting kit re-given after load, overwriting restored inventory (2026-07-14)

**Milestone found:** M8
**Reproduce:** Save game with inventory accumulated beyond starting kit → quit to main menu → Load → inventory shows only starting kit items.
**Expected:** Restored inventory from save persists.
**Actual:** `PlayerController.AnnounceClass()` fires deferred (after `SaveSystem.TryLoad()` restores inventory) and calls `HealthSystem.RequestSetClass`, which clears and re-distributes the starting kit. Deferred slot order: `TryLoad` (slot 2) → `AnnounceClass` (slot 4).
**Fix:** `SaveSystem.SaveWasLoaded` static bool, reset to false in `_Ready()` and set to true just before the restore block in `TryLoad()`. `HealthSystem.RequestSetClass` returns early when `SaveSystem.SaveWasLoaded` is true. M8 solo-safe; future multiplayer reconnects need a per-peer "was in save" check.

Status: FIXED (2026-07-14)

---

## [P2] Load Game panel "Load" button disabled on open — no visible way to load (2026-07-14)

**Milestone found:** M8
**Reproduce:** Main Menu → Load Game → save slot visible and selectable but Load button appears greyed out.
**Expected:** Load button enabled as soon as the panel opens with a save present.
**Actual:** `_loadButton.Disabled = true` on open; only enabled after clicking a list item via `ItemList.ItemSelected` signal. First-time user sees no clickable affordance.
**Fix:** `Refresh()` auto-selects the first (newest) save via `_saveList.Select(0)` and sets `_loadButton.Disabled = false` when saves are present. Added `ItemActivated` handler so double-clicking a save also loads immediately.

Status: FIXED (2026-07-14)

---

## [P2] Felled trees respawn on save→quit→reload (2026-07-13)

**Milestone found:** M8
**Reproduce:** Chop several trees, quit, restart.
**Expected:** Felled trees remain absent; only un-cut trees are visible.
**Actual:** All trees respawn — `TreeSystem._Ready()` re-instantiates every seeded tree and `_treeHp` is reset; felled state is not persisted.
**Fix:** Add `_felledTreeIds SortedSet<string>` to TreeSystem; populate on `FellTree`/`FellTreeForNpc`. Add `List<string> FelledTreeIds` to `SaveData`. `RestoreFelledTreesFromSave()` removes felled IDs from `_treeHp` and calls new `ClientRemoveTreeNode` RPC to hide nodes on all peers. Note: partially-chopped trees (non-zero HP) reset to full on reload — acceptable for M8.

Status: FIXED (2026-07-13)

## [P2] Founder cannot sleep in Shelter — E opens assignment panel instead (2026-07-13)

**Milestone found:** M7
**Reproduce:** Plant Kingdom Marker (become founder), build a Shelter, press E while standing inside it.
**Expected:** Rest bar fills; player sleeps.
**Actual:** `BuildingAssignmentPanel` opens. Sleep never triggers. Priority 1 in `PlayerController.TryInteract` matches the Shelter and returns before Priority 3 (sleep) is reached. Priority 3 also has an explicit `!LocalState.IsFounder` guard, so founders are doubly blocked.
**Fix:** `N` key registered as `open_assignment` in `MainMenuController`. `PlayerController._UnhandledInput` handles it — opens `BuildingAssignmentPanel` if founder. Priority 1 (shelter→panel) removed from `TryInteract`; `!LocalState.IsFounder` guard removed from Priority 3 (sleep). E-at-Shelter now sleeps for everyone. 257 tests, 0 failures.

Status: FIXED (2026-07-13)

---

## [P2] Hotbar slot not cleared when item fully consumed via Tab key (2026-07-10)

**Milestone found:** M7.5a
**Reproduce:** Assign berries (or any consumable) to a hotbar slot. Consume the item to 0 via the default Tab action (`eat_food`). The hotbar slot still shows the item badge.
**Expected:** When an item's stack reaches 0, any hotbar slot referencing that item is automatically cleared.
**Actual:** `InventorySystem.RemoveItems` reduces the stack count but does not walk the hotbar array and null out matching slots. `SyncHotbarTo` is not called after removal, so the client's `LocalState` hotbar is stale.
**Fix:** Added `PlayerInventory.ClearHotbarSlotsFor(itemId)` (nulls matching slots). Called from `InventorySystem.RemoveItems` when `!inv.Has(itemId)` after removal, from `ClearItem` always, and `TakeAll` via `Clear()` (which now also zeros all hotbar slots). `SyncHotbarTo` called in each path. 3 regression tests added to `InventoryTests.cs`. 257 tests, 0 failures.

Status: FIXED (2026-07-13)

---

## [P1] PlayerAnimator node paths wrong — animations not playing (2026-07-09)

**Milestone found:** M5
**Reproduce:** Enter GameWorld as any class; character stands in T-pose, no idle/walk/death animations.
**Expected:** Idle animation plays immediately; walk/run/hit/death animate correctly.
**Actual:** `GetNode<AnimationPlayer>("Knight/AnimationPlayer")` threw at `_Ready()` — node is named `CharacterRig` in Player.tscn, not `Knight`. All animation setup aborted silently.
**Notes:** Fixed by correcting the three path constants in `PlayerAnimator.cs`: `Knight/AnimationPlayer` → `CharacterRig/AnimationPlayer`, and similarly for KnightMeshes/RangerMeshes paths.

Status: FIXED (2026-07-09)

---

## [P1] Ranger meshes T-pose — `skeleton` NodePath missing in Player.tscn (2026-07-09)

**Milestone found:** M5
**Reproduce:** Create a Ranger character; enter GameWorld; character mesh is visible but completely rigid (T-pose) despite animations playing correctly for Fighter.
**Expected:** Ranger meshes deform identically to Knight meshes — same Skeleton3D, same AnimationPlayer.
**Actual:** All `MeshInstance3D` nodes under `RangerMeshes` were missing `skeleton = NodePath("../..")`. Without this, the mesh is not driven by the Skeleton3D and ignores all bone transforms.
**Notes:** Fixed in Godot editor by Edu — set `skeleton = ../..` on all MeshInstance3D children of `RangerMeshes` in Player.tscn. Knight meshes already had this set correctly.

Status: FIXED (2026-07-09)

---

## [FIXED] Starting kit given twice — double items on spawn (2026-07-06)

**Milestone found:** M5 (noticed on Ranger; affects all classes)
**Root cause:** `RequestSetClass` clears the prior kit with `RemoveItems(sender, itemId, 999)`. `PlayerInventory.Remove` requires the player to hold at least `count` items — 999 is never satisfied — so all removes silently fail and the kit is added a second time.
**Fix:** Added `PlayerInventory.ForceRemove(itemId)` (removes all, capped at available count) + `InventorySystem.ClearItem` wrapper. `RequestSetClass` now uses `ClearItem` in the clear loop.

## [FIXED] Wrong en.json edited — loc keys missing in Godot (2026-07-06)

**Milestone found:** M5
**Root cause:** Two en.json files exist: `data/lang/en.json` (root, deleted) and `project/data/lang/en.json` (what Godot loads via `res://`). M5 Phase 1 keys (race.*, charCreate.*) were added to the root copy only.
**Fix:** Keys synced to `project/data/lang/en.json`; root `data/lang/en.json` deleted.
**Rule going forward:** All loc key edits go to `project/data/lang/en.json` only.

Template:

```
## [SEVERITY] Short description (YYYY-MM-DD)

**Milestone found:** MX
**Reproduce:** steps
**Expected:** ...
**Actual:** ...
**Notes:** any hypothesis or related code

Status: OPEN / FIXED (commit hash) / WONTFIX (why)
```

Severity levels: **P0** (blocking / crash), **P1** (major), **P2** (minor), **P3** (cosmetic).

---

## [P1] Shield does not block arrow/projectile attacks (2026-07-05)

**Milestone found:** M4
**Reproduce:** Hold RMB (shield), let a bandit archer shoot an arrow at you.
**Expected:** "Block!" text, no damage taken.
**Actual:** Arrow hit and dealt damage — no block gate existed in ProjectileSystem.
**Notes:** Fixed as part of GDD §12.4. `ProjectileSystem._PhysicsProcess` now checks `CombatSystem.Instance?.IsBlocking(targetId.Value)` after faction gate, before damage.

Status: FIXED (2026-07-05)

---

## [P1] Shield blocking has no effect against monster melee attacks (2026-07-05)

**Milestone found:** M4
**Reproduce:** Hold RMB (shield), let a wolf or goblin melee you.
**Expected:** "Block!" text, no damage taken.
**Actual:** Monster dealt full damage — MonsterSystem.TickAttack had no blocking check.
**Notes:** Block gate only existed in CombatSystem.RequestMeleeAttack (player-initiated path). Fixed by adding `CombatSystem.Instance?.IsBlocking(m.TargetPeer)` check before dice roll in TickAttack, with "Block!" feedback RPC.

Status: FIXED (2026-07-05)

---

## [P1] Bandit archer arrows produce no ghost orbs (2026-07-05)

**Milestone found:** M4
**Reproduce:** Stand near a bandit archer; output shows it is firing but no arrow ghost appears.
**Expected:** Arrow ghost orb flies from archer toward player.
**Actual:** Ghost appears and immediately vanishes — output confirmed ClientSpawnArrow fired then ClientRemoveArrow in the same tick.
**Notes:** ProjectileSystem shooter exclusion only looked up `Players/Player_{OriginPeerId}`. For monster origin IDs (≥ 10001) no node was found, nothing was excluded, and the projectile immediately sphere-hit the firing monster node on tick 1. Faction gate (Allied → same faction) dropped the hit but still removed the projectile. Fixed: also look up `Monsters/Monster_{OriginPeerId}`.

Status: FIXED (2026-07-05)

---

## [P3] Monster bodies clip through each other (2026-07-04)

**Milestone found:** M4
**Reproduce:** Spawn multiple monsters; they overlap in the same space with no physical separation.
**Expected:** Monsters push each other apart (or at minimum don't fully overlap).
**Actual:** Monsters clip through each other because MonsterSystem sets position directly and MonsterNode has `CollisionMask=0u` (intentional — monsters don't need to query anything). No `MoveAndSlide()` separation.
**Notes:** Monsters ARE detectable on Layer 7 (bitmask 64) for melee and projectile combat queries. Physical separation requires adding `MoveAndSlide()` in MonsterSystem + tuning collision masks. Cosmetic only; does not affect gameplay correctness.

Status: OPEN (deferred — cosmetic, not blocking M4)

---

## [P1] LMB shoots arrow instead of placing building when in placement mode (2026-07-04)

**Milestone found:** M4
**Reproduce:** Enter placement mode (B → pick a building), then left-click to place.
**Expected:** Building ghost is placed at cursor position.
**Actual:** BowController intercepts LMB first (last-added child → first in Godot's bottom-up `_UnhandledInput` propagation) and fires an arrow; PlacementController never sees the click.
**Notes:** Fix: BowController checks `PlacementController.Current?.IsPlacing` before consuming LMB.

Status: FIXED (2026-07-04)

---

## [P1] No way to switch between melee and ranged weapon mode (2026-07-04)

**Milestone found:** M4
**Reproduce:** Connect with debug kit (sword + shield + shortbow + 10 arrows). Try to melee attack.
**Expected:** Player can toggle between ranged and melee modes.
**Actual:** BowController always intercepts LMB when a ranged weapon is in inventory, making melee unusable.
**Notes:** Fix: `LocalState.PreferRanged` toggle (default false = melee). `Q` key switches modes. BowController yields to MeleeController when `!PreferRanged && hasMeleeWeapon`.

Status: FIXED (2026-07-04)

---

## [P2] GodotSteam steamInitEx status 0 misread as failure (2026-07-02)

**Milestone found:** M0
**Reproduce:** Call `Steam.steamInitEx()` from C# via `Engine.GetSingleton("Steam").Call("steamInitEx")` and check `status` key
**Expected:** status 1 = success (old GodotSteam convention)
**Actual:** status 0 = `k_ESteamAPIInitResult_OK` (raw Steamworks SDK enum — success)
**Notes:** GodotSteam 4.20 passes the Steamworks SDK enum directly. `verbal: ""` (empty) confirms no error. Verify with `getSteamID()` post-init.

Status: FIXED — smoke test now checks `status != 0` as failure and logs SteamID on success

