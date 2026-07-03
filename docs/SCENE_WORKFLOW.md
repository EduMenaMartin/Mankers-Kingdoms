# Scene Creation Workflow

**Status:** Locked working practice. Not an ADR (it's a workflow discipline, not an architectural decision), but treat it with the same seriousness — violating it burns real time and tokens.

**Context:** Early M1 work showed that having Claude Code generate or edit `.tscn` files directly leads to repeated failure loops. Godot's scene format has internal metadata, UIDs, sub-resource indices, and import dependencies that aren't reliably reproducible by an LLM writing text files from outside the editor. A session attempting this burned 1.5 hours and a full usage window without a visible result for what should be a 10-minute task.

---

## The rule

**Godot's editor owns scene creation. Claude Code owns script logic.**

Concretely:

1. **Claude Code never generates or edits `.tscn` or `.tres` files directly**, with one narrow exception below.
2. **All node tree setup** (adding nodes, setting positions, attaching meshes/collision shapes, wiring exported variables in the Inspector) **happens manually in the Godot editor** by Edu.
3. **Claude Code writes and edits `.cs` script files only.** This is where the actual game logic lives, and it's a normal text file with no hidden format traps.
4. When a new scene or node is needed, Claude Code **describes the node tree** it needs (node types, names, hierarchy, key properties) and Edu builds it in the editor — typically 2–5 minutes of clicking.
5. Once the scene exists, Claude Code attaches scripts to nodes (via code referencing node paths, or Edu manually drags the script onto the node in the editor — whichever is more reliable in the moment) and writes the logic.

**Narrow exception:** Claude Code may read `.tscn` files to understand the current scene structure (node names, hierarchy, existing script attachments) for debugging purposes. Reading is safe; writing is not.

---

## Why this works

- Godot's editor generates `.tscn` files correctly by construction — it knows its own format
- Claude Code's strength is logic, systems, and text-based code — not binary-adjacent structured formats it can't execute or preview
- This mirrors the client/server/shared split already in ARCHITECTURE.md: clear ownership boundaries prevent tools from working against each other
- 95% of actual gameplay work (movement, combat, needs, skills, networking, save/load) is pure C# logic anyway — this rule barely limits what Claude Code contributes

---

## Practical workflow example

**Bad (what caused the 1.5-hour loop):**
> Edu: "Create the GameWorld scene with a player and a ground plane."
> Claude Code: *generates GameWorld.tscn with hand-written UIDs and node structure*
> Godot: rejects it / fails to load / player doesn't appear
> Claude Code: *regenerates with different metadata*
> Repeat until usage limit hit.

**Good:**
> Claude Code: "I need a GameWorld scene with this structure: Node3D (root, named GameWorld) → StaticBody3D (Ground) with CollisionShape3D + MeshInstance3D (PlaneMesh 50×50) → CharacterBody3D (Player) with CollisionShape3D (CapsuleShape3D) + MeshInstance3D (CapsuleMesh) → Camera3D (top-down, Position Y=10, Rotation X=-90) → DirectionalLight3D. Can you set this up in the editor? I'll write the Player.cs movement script once it exists."
> Edu: *builds it in Godot editor, 5 minutes*
> Edu: "Done, Player node exists at res://scenes/GameWorld.tscn"
> Claude Code: *writes Player.cs, attaches via editor instruction or code, tests logic*

---

## When debugging "nothing appears" type issues

Symptoms like "scene doesn't show up" or "player doesn't appear" are almost always one of:

1. A node exists in the tree but has no `MeshInstance3D` child (invisible but present)
2. A spawn script (`PlayerSystem`, `NetworkManager`, etc.) has logic that should instance a `PackedScene` at runtime but isn't running, has a silent error, or is waiting on a condition that never fires in the test context (e.g. waiting for a network event during solo testing)
3. An exported variable (e.g. `PlayerScene: PackedScene`) is left `<empty>` in the Inspector — the script references it but nothing was ever assigned
4. The wrong scene is set as the Main Scene in Project Settings → Application → Run

**Debug order:** check the Output panel for silent errors first, then check Inspector for unset exported fields on any spawner-type nodes, then verify the node tree actually contains what's expected. Don't regenerate scene files as a first response to "nothing shows up."

---

## Reference

- `ARCHITECTURE.md` §3 (client/server/shared split — same ownership-boundary principle)
- `CLAUDE.md` — should reference this file under Operating Instructions
