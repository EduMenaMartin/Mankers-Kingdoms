# GodotSteam — Lobbies Tutorial Reference (Snapshot)

**Source:** https://godotsteam.com/tutorials/lobbies/
**Fetched:** 2026-07-02
**License:** Documentation belongs to GodotSteam project (GP Garcia | Contributors). Local development reference only.
**Purpose:** local reference for Mankers Kingdoms M1.5. Site blocks Claude Code's automated fetches.

**IMPORTANT:** The tutorial's code examples are all in **GDScript**. We are a C# project (ADR-0010). Do not copy GDScript syntax directly — translate the flow/logic described below into C#. Signal connection syntax, snake_case function names becoming... actually GodotSteam's C# bindings keep the same `snake_case` function names as GDScript (per the C# tutorial we should also fetch), only the language syntax around them changes (e.g. `Steam.CreateLobby(...)` vs `Steam.createLobby(...)` — verify actual casing convention when we fetch the C# tutorial).

---

## Overall flow (host side)

1. Player clicks "Host" → call `Steam.createLobby(lobbyType, maxMembers)`
2. This is **async** — returns immediately, real result comes via `lobby_created` signal
3. In the `lobby_created` handler: check `connect_status == Steam.Result.RESULT_OK`. If OK, store the returned `lobby_id`. Optionally call `Steam.setLobbyData(lobbyId, "lobby_name", "...")` to tag it for display.
4. A **second** signal, `lobby_joined`, will also fire — because creating a lobby auto-joins the host to their own lobby. This is where you transition to the actual lobby/game scene.
5. Once in `lobby_joined` with a success response, call `MultiplayerPeer.host_with_lobby(lobbyId)` (see multiplayer-peer-class.md snapshot) to actually stand up the P2P networking layer, then assign it to Godot's `Multiplayer.MultiplayerPeer`.

## Overall flow (client/join side)

Three ways a client ends up joining, all converging on the same `lobby_joined` signal:

**A. Joining from a lobby list (public lobby browse)** — not needed for M1.5, skip.

**B. Joining via direct Steam friend invite while game is already running:**
- Friends class emits `join_requested` signal with `(lobby_id, friend_id)`
- Handler calls `Steam.joinLobby(lobby_id)`

**C. Joining via Steam invite/friends-list "Join Game" while game is NOT running:**
- Steam launches the game executable with command-line argument `+connect_lobby <lobby_id>`
- On startup, before showing the main menu, check `OS.get_cmdline_args()` for this pattern
- If found, parse the lobby ID and call `Steam.joinLobby(int(lobbyId))`
- This must happen early in the boot sequence, before the normal main menu shows, so the player lands directly in the join flow

**Both B and C converge on:** `Steam.joinLobby(lobbyId)` → wait for `lobby_joined` signal.

**In the `lobby_joined` handler**, check the `response` field against `ChatRoomEnterResponse` enum:
- `CHAT_ROOM_ENTER_RESPONSE_SUCCESS` → proceed: store lobby_id, transition to lobby/game scene, call `MultiplayerPeer.connect_to_lobby(lobbyId)`
- Anything else → failure. The tutorial lists specific failure reasons (doesn't exist, not allowed, full, banned, community ban, blocked by member, rate limited, etc.) — worth logging the specific reason for debugging, but the recovery action is the same: show an error, return to menu.

## Signals to wire up (per the Initializing tutorial's callback setup — see that tutorial before this one if not already done)

- `Steam.lobby_created` → handle create result
- `Steam.lobby_joined` → handle join result (fires for BOTH host's auto-join AND client's explicit join)
- `Steam.lobby_chat_update` → member joined/left, refresh member list display
- `Steam.lobby_data_update` → lobby or member metadata changed, refresh display
- `Steam.join_requested` (Friends class, not Matchmaking) → handle in-game friend invite acceptance

## Minimal data model needed

From the tutorial's pattern, track at minimum:
- `lobby_id: ulong` — 0 means "not in a lobby"
- `invite_lobby_id: ulong` — optional, only needed if supporting the command-line/friend-invite join paths (B and C above); stash the pending lobby ID here until the manager scene is ready to act on it

## Getting the member list

```
numMembers = Steam.getNumLobbyMembers(lobbyId)
for i in 0..<numMembers:
    steamId = Steam.getLobbyMemberByIndex(lobbyId, i)
```

Re-run this any time `lobby_chat_update` or `lobby_data_update` fires, to keep the displayed member list current. Simplest approach: clear and rebuild rather than diffing.

## What NOT to build for M1.5

The full tutorial covers a lot we don't need yet:
- Public lobby search/browse UI with filters (distance, slots, search terms) — that's auto-matchmaking, post-slice feature per PRD roadmap
- In-lobby text chat UI — nice-to-have, not required to prove the P2P connection works
- Kick/promote-to-host UI — needed eventually (host migration matters for our "authoritative host" model per ADR-0005) but not for the first working connection
- Steam overlay invite dialog button — can wait; for initial testing, you and your test partner can each grab the lobby ID by other means (e.g. print it to console) or just use direct Steam friend "Join Game"

## What IS needed for M1.5 (the actual milestone scope)

1. Host button → `createLobby` → on success, `host_with_lobby`
2. Client joins via Steam's native friend-invite flow (Steam handles the UI) → `join_requested` or command-line path → `joinLobby` → on success, `connect_to_lobby`
3. Both players' `CharacterBody3D` capsules visible and moving for each other — this is the actual thing being tested, everything above is just plumbing to get two `MultiplayerPeer` instances connected

## Command-line join handling — where it fits in Main.tscn

Per your GameWorld.tscn structure (Main → MainMenu / GameWorld), the check for `+connect_lobby` command-line args should happen in `Main.cs`'s startup logic, BEFORE deciding to show MainMenu — if the arg is present, skip straight to attempting the join instead of showing the menu.

---

## Still not fetched (fetch if/when needed)

- **C# tutorial** (`godotsteam.com/tutorials/c-sharp/`) — actual C# syntax conventions for GodotSteam calls, signal connection pattern in C# (`+=` vs `.Connect(...)`), casing conventions. **Recommend fetching this before writing any GodotSteam C# code**, since everything above is GDScript-flavored and needs translation.
- **Initializing tutorial** (`godotsteam.com/tutorials/initializing/`) — the three callback setup methods mentioned repeatedly above ("you may want to double-check our Initialization tutorial"). We likely already have working init since Steam logs show successful connection, but worth confirming our callback setup matches recommended patterns.
- **Friends class** — for `join_requested` signal signature and `getFriendPersonaName` used in invite handling.

---

*End of snapshot.*
