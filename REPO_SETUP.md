# Repo Setup — Getting Mankers Kingdoms on your machine

This walks you through initializing the local dev environment. Do it once.

**Time estimate:** 30–60 minutes if everything goes smoothly, longer if you're installing tools for the first time.

---

## Prerequisites

Verify or install:

- **Git** (any recent version)
- **Git LFS** — for binary assets. `git lfs install` after installing.
- **Godot 4** (latest stable, .NET version). Download from [godotengine.org](https://godotengine.org). The .NET build is a separate download from the standard one.
- **.NET SDK 8+** — verify with `dotnet --version`
- **JetBrains Rider** (recommended) OR **VS Code** with C# Dev Kit extension
- **Claude Code** — installed and configured with your Anthropic account
- **Steam** installed (needed for GodotSteam testing later, not for M0)

Optional but recommended:
- A GitHub account for eventual private repo hosting (not required to start)

---

## Step 1: Set up the repo locally

The zip you got from Claude contains the entire folder structure with all docs and ADRs. Extract it wherever you keep code (matches your ImkerTracker pattern — probably `F:\ClaudeCode\` or similar).

```bash
# Wherever you keep your projects:
cd F:\ClaudeCode\
# Extract the zip (or unzip manually via Explorer)
# You should have: F:\ClaudeCode\mankers-kingdoms\

cd mankers-kingdoms

# Verify the structure
ls
# Expected: ARCHITECTURE.md, CLAUDE.md, PITCH.md, PRD.md, README.md, VERTICAL_SLICE.md,
#           HANDOVER.md, TODO.md, BUGS.md, IDEAS_BACKLOG.md, CHANGELOG.md,
#           .gitignore, .gitattributes, docs/, project/, data/, mods/, tools/
```

---

## Step 2: Initialize git

```bash
cd mankers-kingdoms
git init
git lfs install
```

Verify `.gitattributes` is picked up:

```bash
git lfs track
# Should list *.png, *.jpg, *.wav, etc.
```

---

## Step 3: Initial commit — documentation baseline

```bash
git add .
git status
# Review: should show all docs, .gitignore, .gitattributes, and README.md files
# in mods/ and data/lang/

git commit -m "chore: initial commit — documentation foundation

Added:
- PITCH, PRD, VERTICAL_SLICE, ARCHITECTURE docs
- SKILLS gdd
- 22 ADRs (0001-0022) in docs/decisions/
- CLAUDE.md, HANDOVER.md, TODO.md, BUGS.md, IDEAS_BACKLOG.md
- CHANGELOG.md, README.md
- .gitignore, .gitattributes
- Folder structure for project/, data/, mods/, tools/
- 2 stubs README.md for mods/ and data/lang/

Design phase complete. Ready to begin M0 (Godot project scaffolding)."
```

---

## Step 4: Set up remote (optional, later)

When you're ready to push to GitHub:

```bash
# Create a private repo named "mankers-kingdoms" on github.com
# Then:
git remote add origin git@github.com:<your-username>/mankers-kingdoms.git
git branch -M main
git push -u origin main
```

Not required to start. You can work purely locally through M0 if you prefer.

---

## Step 5: Initialize the Godot project (start of M0)

Once the git repo is committed, the actual game project goes into `/project/`. This is M0's first task.

**In Godot 4:**

1. Launch Godot 4 (.NET build)
2. Click "New Project"
3. Set project path to: `<repo>/project/`
4. Set project name: `mankers-kingdoms`
5. Renderer: **Forward+** (good default for a 2D-ish game)
6. Click "Create & Edit"

This will populate `/project/` with `project.godot`, `.godot/` (which is gitignored), and open the editor.

**In Rider / VS Code:**

1. Open the repository folder as a workspace
2. Rider should auto-detect the `.csproj` once Godot generates it (after first build)
3. If not: File → New → Project → Console App → set location to `/project/` and adjust the `.csproj` for Godot compatibility
4. Install the Claude Code plugin for your IDE
5. Configure Claude Code to use this repo as its working directory

---

## Step 6: First Claude Code session in the new repo

Open a terminal in the repo root (or use Rider/VS Code's terminal), and start Claude Code.

**Recommended first prompt:**

> Read CLAUDE.md, HANDOVER.md, and TODO.md. Then confirm you understand:
> 1. The project (Mankers Kingdoms — coop settlement builder)
> 2. The current milestone (M0)
> 3. My Operating Instructions
> 4. The client/server/shared code discipline
>
> Then propose the next 3 concrete steps for M0.

Claude should read the files, summarize back to you what it understands, and propose next steps. If it doesn't reference the ADRs or the folder discipline, remind it to consult CLAUDE.md again.

---

## Step 7: Recurring session pattern

Every subsequent Claude Code session should start with:

> Read HANDOVER.md and TODO.md. What are we working on and where did we leave off?

Every session should end with:

> Update HANDOVER.md with what we did today, what's next, and any decisions I need to make. Prepare a commit message but don't commit — I'll do that.

This is the muscle memory that keeps context tight across sessions.

---

## Step 8: Verify the environment (M0 exit criterion)

M0 is complete when:

- [ ] `git status` shows clean working tree after the initial commit
- [ ] Godot 4 opens the project without errors
- [ ] `dotnet build` in `/project/` succeeds
- [ ] Running the project (F5 in Godot) opens a window
- [ ] The window title reads "Mankers Kingdoms" (from the localization file)
- [ ] Same behavior on your second dev PC (if applicable)

Then update `TODO.md`, `HANDOVER.md`, and `CHANGELOG.md`, and move to M1.

---

## Troubleshooting

### Godot doesn't see .NET
Install .NET SDK 8+ and re-launch Godot. Verify with `dotnet --version` in a fresh terminal.

### Rider doesn't find Godot's C# API
First run: build the project in Godot editor once (Project → Tools → C# → Create C# Solution). This generates the `.sln` and stub `.csproj` that Rider needs.

### GodotSteam confusion
Do NOT add GodotSteam in M0. It's an M1 task. First get the project building at all.

### CI setup (deferred)
GitHub Actions for build+test on push is optional for M0. Address in M1 when there's actual code to build. Don't get sidetracked here.

---

## What NOT to do

- Don't add the GodotSteam plugin yet — that's M1
- Don't scaffold a UI framework — placeholder scenes only
- Don't write gameplay code in M0 — just prove the environment works
- Don't skip the localization scaffold — even the M0 splash reads from `en.json` per ADR-0012

---

## Reference

- CLAUDE.md — full context for Claude Code sessions
- VERTICAL_SLICE.md §5 — M0 through M9 details
- ARCHITECTURE.md — the technical bible
- All 22 ADRs in `docs/decisions/`
