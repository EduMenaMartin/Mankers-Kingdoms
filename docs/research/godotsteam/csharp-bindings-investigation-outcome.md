# GodotSteam C# Bindings — Investigation Outcome (2026-07-02)

**Context:** While implementing M1.5 (ENet → GodotSteam P2P swap), Claude Code hit a real blocker: GodotSteam's `MultiplayerPeer` class has no official C# instantiation pattern. Investigated directly against godotsteam.com and GitHub. Findings below. **Decision: defer M1.5, stay on ENet for now.**

---

## What we found

**GodotSteam has no official .NET/C# bindings.** Confirmed directly from GodotSteam's own docs (`godotsteam.com/tutorials/c-sharp/`): *"GodotSteam does not have a C# version currently."* The `MultiplayerPeer` class we documented in `docs/research/godotsteam/multiplayer-peer-class.md` is real and correct, but it's only natively callable from GDScript. C# access requires a third-party binding layer.

**Two unrelated "SteamMultiplayerPeer" things exist — do not confuse them:**
1. GodotSteam's own built-in `MultiplayerPeer` class (what we want) — the one documented in our snapshots
2. ExpressoBits' `steam-multiplayer-peer` — a **separate, unrelated GDExtension**, whose own maintainer has **paused development** and says "please try using GodotSteam if possible." Its C# support exists only as an **unmerged pull request**. Do not use this.

**The C# path for GodotSteam's own MultiplayerPeer is `LauraWebdev/GodotSteam_CSharpBindings`** — a third-party (not GP Garcia's own) binding layer. Investigated directly on GitHub:

- Labeled **"Open Beta"** by its own maintainer
- Latest release **1.1.0, dated July 2024** — roughly two years old
- **Officially targets Godot 4.4+ with GodotSteam 4.6.1 specifically**
- **Our project is on GodotSteam 4.20** (per CHANGELOG.md, M0 notes) — a significant version gap from what these bindings were built/tested against
- A community fork (`craethke/GodotSteam_CSharpBindings`) exists specifically because the original "haven't yet been updated for GodotSteam 4.11+" — independent confirmation of the version-lag problem
- Found an **open GitHub issue** on the original repo reporting the `lobby_created` signal doesn't fire correctly via these bindings — this is the exact signal our entire host/join flow depends on (see `docs/research/godotsteam/lobbies-tutorial.md`)

## Why this matters

If we build M1.5 on these bindings and hit a bug, we'd be debugging three layers removed from our own code: our C# → LauraWebdev's beta wrapper → GodotSteam's GDExtension → Steam's API. That's a bad debugging position for a solo dev, and the open issue on the exact signal we need is a concrete warning sign, not theoretical risk.

## Decision

**Defer M1.5 (GodotSteam P2P swap). Continue M1–M4 gameplay work on ENet/LAN as originally scoped as the M0–M1 fallback.**

This does NOT reverse ADR-0002 (dedicated server first-class) or ADR-0005 (authoritative host) or ADR-0010 (Godot 4 + C# + GodotSteam). Those stay locked. This is a **sequencing change**: prove the core gameplay loop is fun on a networking layer that won't fight us, and revisit the Steam transport swap once either (a) LauraWebdev's bindings mature and close the version gap, or (b) we've properly evaluated alternative integration approaches without time pressure on core milestones.

## What NOT to do right now

- Do not attempt to use ExpressoBits' `steam-multiplayer-peer` (paused, unrelated project, unmerged C# PR)
- Do not attempt to install LauraWebdev's bindings and push through version-mismatch errors — the version gap (4.6.1 vs our 4.20) plus the known open signal bug make this a poor use of session time right now
- Do not silently keep working in GDScript for just the networking layer as a workaround without discussing it with Edu first — this would be a real architectural mixed-language decision requiring its own sign-off, not a quick patch

## What TO do right now

1. Revert/keep `NetworkManager.cs` on ENet (Godot's built-in `MultiplayerAPI` + `ENetMultiplayerPeer`) for LAN testing
2. Continue M1 (main menu + two clients see each other) using ENet as the transport
3. Proceed through M2–M4 gameplay milestones normally — none of them are blocked by this
4. Revisit the GodotSteam swap as its own milestone later, once either the binding ecosystem matures or we've deliberately evaluated the mixed-language (GDScript networking boundary) approach as an ADR-level decision with Edu

## Reference

- `docs/research/godotsteam/multiplayer-peer-class.md` — the class we wanted to use (still accurate reference for when we revisit this)
- `docs/research/godotsteam/matchmaking-class.md`, `lobbies-tutorial.md` — same, still valid reference material for later
- `docs/research/godotsteam/M1.5-implementation-guide.md` — the implementation plan we drafted; **paused, not deleted** — reusable once the bindings situation resolves
- ADR-0002, ADR-0005, ADR-0010 — unaffected, still locked
- This file should be referenced from a new ADR if/when Edu wants to formalize the deferral (not yet written)
