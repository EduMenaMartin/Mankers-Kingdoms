# BUGS — Known Bugs

**Format:** one entry per bug. Newest at the top.

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

