# Ideas Backlog

**The pressure release valve.** Every new idea that emerges during dev goes here first. No idea is too small to write down; no idea gets scope-crept into the current milestone without triage.

## Triage tags

Every entry gets one:

- `[trivial-content]` — new item, monster, building, recipe, decorative asset. Cheap to add whenever. Not architectural.
- `[post-slice]` — real feature; add to PRD roadmap after M9.
- `[slice-affecting]` — would change the vertical slice scope. Requires ADR discussion before accepting.
- `[rejected]` — considered and declined. Keep with a "why not" note so we don't relitigate.

---

## Entries

### 2026-07-02 — [post-slice] Content loading from repo-root `data/` via filesystem

Currently `data/lang/en.json` lives both at repo root (`data/lang/`) and duplicated inside the Godot project (`project/data/lang/`) as a workaround for `res://` access limits. The proper architecture loads all content from the repo-root `data/` directory using filesystem paths resolved relative to the game executable — consistent with how mods load content. Needs a content loader system. Not needed until M3+ (mod loading milestone).

### 2026-07-02 — [trivial-content] GodotSteam editor setup script

Getting GodotSteam working in editor play mode requires manually copying `steam_api64.dll` and `steam_appid.txt` into the Godot editor executable directory on each dev machine. A small `tools/setup-godotsteam-editor.ps1` script would automate this for onboarding. Low effort, useful for second dev PC.

### 2026-07-03 — [post-slice] Right-mouse orbital camera rotation

Right-click + drag to orbit the camera horizontally around the player. Implementation: add a `CameraPivot` Node3D child to `Player.tscn` (between the CharacterBody3D and Camera3D), accumulate mouse delta on `InputEventMouseMotion` while right button held, rotate pivot Y. Pitch stays fixed from editor transform. All client-side, no networking changes.

### 2026-07-03 — [post-slice] Kingdom Marker upgrade system

Kingdom Marker has a level (1–5). Each upgrade increases the territory radius by +10 units (base 40, max 90). Upgrading costs wood + stone (resources unlock at M4). Upgrade UI shown on E-interact with the marker. Visual: marker gets taller / adds decorative rings per level. Pairs naturally with the settlement progression loop — grow your territory as you gather resources.

### 2026-07-04 — [post-slice] Full settlement role hierarchy

Founder/Co-Founder/Officer/Member/Guest tiers. Fully designed in `docs/gdd/settlements.md` §2. Not scheduled. Solves v1's presence-based-guest limitation (guests today have same storage rights as trusted members; post-slice hierarchy introduces explicit Member promotion with persistent access even when founder is offline). Revisit post-M9.

### 2026-07-04 — [post-slice] Toxic raw foods / poison status effect

Some foods are flagged `IsToxicRaw=true` in FoodData — eating raw inflicts poison for `PoisonDuration` seconds (HP drain over time). Cooked form is safe. Data fields already in `FoodData` and `FoodRegistry`. Implementation blocked on M4's health/damage system. When ready: `NeedsSystem` tracks `PoisonedUntil` timestamp and drains HP per tick; `LocalState` exposes poison state; `NeedsHUD` shows purple indicator. Log server-side already prints `[TOXIC]` warning as a placeholder.

### 2026-07-03 — [post-slice] Ambient audio tied to day/night and monster proximity

Two ambient audio layers: daytime (birdsong) and nighttime (insects/crickets), crossfaded by DayNightClient on sunrise/sunset. A proximity check silences ambient audio when a hostile entity is within ~20 units — classic "audio tells you something is nearby" tension cue. Implementation: `client/AmbientAudioSystem.cs` — two AudioStreamPlayer nodes, volume tweened on day/night change, muted when enemy detection radius triggers. No new server-side logic needed.

### 2026-07-02 — [post-slice] Full Godot headless build in CI

CI currently only runs xUnit tests (pure .NET, no Godot). A headless Godot build step would catch C# compile errors in client/server scripts that reference Godot types. Requires `chickensoft-games/setup-godot` action on the runner. Worth adding in M1 once server scripts exist.

