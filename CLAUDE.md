# CLAUDE.md — Context and Conventions for Claude Code

**This file is read at the start of every Claude Code session.** It defines the project, the working style, and the rules.

---

## Project

**Mankers Kingdoms** — a cooperative top-down survival and settlement-building game in a mystical D&D-flavored fantasy world, spiritual successor to 1993's *D&D Stronghold: Kingdom Simulator*, modernized with Valheim-style WASD coop avatar, Aska/Bellwright station-based village management, and RuneScape-style use-to-level skills soft-capped by AD&D stats.

Currently in vertical slice phase (M0–M9). See `VERTICAL_SLICE.md` for exact scope.

---

## Read first

At the start of every session, read these:

1. `HANDOVER.md` — where we left off last session
2. `CURRENT_MILESTONE.md` — what we're actively working on (if exists)
3. `TODO.md` — active task list
4. `BUGS.md` — known bugs
5. This file (CLAUDE.md) — you're reading it now

For system-level questions, consult:
- `PRD.md` for design intent
- `ARCHITECTURE.md` for technical design
- `docs/decisions/ADR-XXXX-*.md` for the *why* of specific decisions
- `docs/gdd/<system>.md` for a specific system's design detail
- `docs/scene_workflow.md` for scene creation and "nothing appears" debug order — **Claude Code never edits `.tscn` or `.tres` files**

---

## Operating instructions (Edu's personal norms)

These override any default Claude Code behavior.

1. **PRD-first.** Before implementing any new feature, verify it's in the PRD or VERTICAL_SLICE scope. If it isn't, stop and ask before writing code.
2. **No sycophancy.** If a proposed approach is wrong, say so directly with reasoning. Don't apologize; correct.
3. **Reversibility gates.** Before any destructive action (file deletion, git force-push, schema change), pause and confirm.
4. **Task capture before implementation.** Before implementing any feature or fix:
   - Feature → add entry to `TODO.md`
   - Bug → add entry to `BUGS.md`
   - Present the entry and explicitly wait for approval before writing code or making file changes.
5. **New ideas go to backlog.** Any new idea that arises during work goes into `IDEAS_BACKLOG.md` with a triage tag (see the file). Do not silently scope-creep the current milestone.
6. **Locked decisions require ADRs.** If we discover a locked decision needs to change, don't just change it. Draft the ADR update first, present it, wait for approval.
7. **Handover discipline.** At the end of any substantial session, update `HANDOVER.md` with: what was done, what's next, what's blocked, what needs a human decision.
8. **Do not commit for me.** Prepare commits and describe them; I run `git commit` myself.

---

## Architectural rules

Non-negotiable. Enforced by folder structure, code review, and CI where possible.

1. **Server/client/shared discipline.** All `.cs` files live in one of `/project/scripts/server/`, `/project/scripts/client/`, `/project/scripts/shared/`. Server imports from shared only. Client imports from shared only. Shared imports from nothing platform-specific. See `ARCHITECTURE.md` §3.
2. **Host is a dedicated server + local client.** All simulation logic in `/server/` must run headless. Never assume UI, input, or rendering is available in server code.
3. **Content is data.** All items, monsters, buildings, recipes, skills, classes, and NPC archetypes are loaded from `.json` or `.tres` files in `/data/base/`. No content is hardcoded in C#.
4. **Stable string IDs everywhere.** Every content entity has an ID like `"monster.goblin.scout"`. Saves reference IDs. No integer indices for content references.
5. **No string literals in gameplay code.** All player-facing text goes through `Loc.T("key.path")`. Enforced by CI grep check.
6. **Seeded RNG only.** Each server system that needs randomness holds its own `System.Random` seeded from `GameSession.WorldSeed ^ <system-constant>` in `_Ready()`. Never `Random.Shared` or an unseeded `new System.Random()`. Never share one global RNG across systems — per-system instances keep sequences independent and reproducible. See `ARCHITECTURE.md §7`.
7. **Ordered iteration.** Never iterate a `Dictionary<T>` or `HashSet<T>` in server logic. Use `SortedDictionary<T>` or explicit ordering.
8. **Save-format has a version field.** Increment `SaveData.Version` on every schema-affecting change — including additive-only additions (new nullable fields, new nested objects with defaults). Always add a migration entry for each version increment, even if the body is a no-op stub, so the migration chain stays complete and future migrations have the correct base version to chain from. "Additive field, no version bump required" is not acceptable reasoning; bump and stub. See `ARCHITECTURE.md §8.2` for the migration pattern.
9. **The base game loads through the mod loader.** Base game is at `/data/base/`, loads first. No special-casing.

---

## Style and conventions

### C# / .NET
- Target .NET 8+
- 4-space indent, **Allman braces** (opening brace on its own line) — per Godot's official C# Style Guide
- `PascalCase` for types, methods, public properties
- `camelCase` for local variables and parameters
- `_camelCase` for private fields
- `SCREAMING_SNAKE_CASE` for constants
- File name = primary type name
- One top-level type per file
- `using` directives sorted, no wildcard imports
- **All Godot-derived classes must use the `partial` keyword** — required for the source generator; `[Export]` and `[Signal]` silently fail to wire up without it
- **Prefer properties over public fields**
- **Prefer plain .NET collections for internal logic** (`List<T>`, `Dictionary<T>`, `SortedDictionary<T>`); only use `Godot.Collections` types at the actual Godot API boundary (method parameters/returns that Godot itself requires). This reinforces, not conflicts with, the existing "never iterate Dictionary/HashSet in server logic" rule.
- **Modifier ordering:** `public` / `protected` / `private` / `internal` / `virtual` / `override` / `abstract` / `new` / `static` / `readonly`

### Naming patterns
- Components: `<Thing>Component` (e.g. `HealthComponent`)
- Systems: `<Thing>System` (e.g. `NeedsSystem`)
- Data / DTOs (in shared): `<Thing>Definition` for content, `<Thing>Data` for save state
- Registries: `<Thing>Registry` (e.g. `MonsterRegistry`)
- Networking messages: `Msg<Thing>` (e.g. `MsgPlayerInput`)

### Comments
- Focus on *why*, not *what*
- Reference ADRs for non-obvious decisions: `// See ADR-0005: authoritative host chosen over lockstep`
- Public API doc comments for anything in `/shared/`

### Commit messages
- Conventional Commits style: `feat:`, `fix:`, `refactor:`, `docs:`, `test:`, `chore:`
- Reference milestones and ADRs: `feat(server): add hunger decay system [M3] [ADR-0011]`
- Present tense, imperative mood

---

## Testing expectations

- **New simulation system → new unit tests.** No exceptions.
- **Bug fix → regression test.** Prove it stays fixed.
- **Save-format change → round-trip test.** Save → load → save → byte-compare.
- **Netcode change → determinism test where applicable.**
- Tests run in CI. Broken `main` is a P0.

---

## Session workflow

Typical session shape:

1. **Read** `HANDOVER.md` and `CURRENT_MILESTONE.md`
2. **Confirm scope** with Edu: "Working on X from M[N]; touching files A, B, C. Correct?"
3. **Plan** in `TODO.md` — add subtasks with checkboxes if not already there
4. **Implement**
5. **Test** — write and run tests before declaring done
6. **Update** relevant docs: TODO.md tick off items, HANDOVER.md at session end
7. **Prepare commit** with a well-formed message; do not commit
8. **Report** what was done, what remains, and any decisions needed from Edu

---

## What to escalate to Edu (never decide alone)

- Any change to a locked design decision (requires ADR update)
- Any change to the client/server split rule
- Any new external dependency (NuGet package, git submodule)
- Any performance trade-off that adds complexity for < 2× speedup
- Any addition of a scripting language, DSL, or embedded interpreter
- Any change to the save format schema (must add a migration + ADR reference)
- Any weakening of the modding surface

---

## Language

Documentation and code in English. All player-facing strings in the localization file (English canonical, community-translated).

Edu is a native Spanish speaker based in Munich (German-speaking). If a term is ambiguous or if a translation nuance matters, ask.

---

## Reference

- Primary IDE: JetBrains Rider (Claude Code plugin)
- Secondary IDE: VS Code (Claude Code extension)
- Godot editor for scene editing only
- Rider/VS Code for all `.cs` editing
