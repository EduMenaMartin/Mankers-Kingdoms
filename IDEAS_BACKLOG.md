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

### 2026-07-02 — [post-slice] Full Godot headless build in CI

CI currently only runs xUnit tests (pure .NET, no Godot). A headless Godot build step would catch C# compile errors in client/server scripts that reference Godot types. Requires `chickensoft-games/setup-godot` action on the runner. Worth adding in M1 once server scripts exist.

