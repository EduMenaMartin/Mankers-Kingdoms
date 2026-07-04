# BUGS — Known Bugs

**Format:** one entry per bug. Newest at the top.

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

