# GodotSteam — Main Class Reference (Snapshot)

**Source:** https://godotsteam.com/classes/main/
**Fetched:** 2026-07-02
**License:** Documentation belongs to GodotSteam project (GP Garcia | Contributors). Retained here as a local development reference only. Do not redistribute.
**Purpose:** local reference for Mankers Kingdoms development; the godotsteam.com site blocks automated fetches, so this snapshot is kept in-repo for Claude Code to read directly.

**Refresh policy:** re-fetch and update this file whenever the GodotSteam version we use changes. Note the fetch date at the top so we know how current it is.

---

## Overview

The `Main` class in GodotSteam is the entry point for Steam functionality. It handles initialization/shutdown, tracks internally-stored handles (browser, app ID, clan ID, Steam ID, inventory, leaderboards, server list requests), and provides account-type checks against Steam IDs.

Only available in the main GodotSteam branches (not in the server-only branches).

---

## Key Functions

### Lifecycle
- `steamInit(app_id = 0, embed_callbacks = false)` — legacy initializer, returns `bool`. Won't tell you *why* it failed.
- `steamInitEx(app_id = 0, embed_callbacks = false)` — modern initializer, returns a **dictionary** with `status` (SteamAPIInitResult enum) and `verbal` (error message string). **Use this.**
- `steamShutdown()` — shuts down the Steamworks API, releases pointers and frees memory. Called automatically by GodotSteam when the game shuts down.
- `restartAppIfNecessary(app_id)` — if you launched the executable outside Steam, this relaunches through Steam. Returns `true` if it restarted (in which case you should quit). Returns `false` if launched by Steam OR if a `steam_appid.txt` file is present (dev-only override).
- `run_callbacks()` — must be placed in a `_process` function unless you set "Embed Callbacks" in Project Settings > Steam > Initialization.
- `releaseCurrentThreadMemory()` — frees internal thread-local memory. Called automatically by `run_callbacks()`.
- `isSteamRunning()` — check if the Steam client is running.
- `get_steam_init_result()` — returns the same dictionary as `steamInitEx()` for later inspection (only works with `steamInitEx` or auto-init, not `steamInit`).

### Internally-stored handles (getters and setters)
For each of these, GodotSteam maintains an internal value you can get/set:
- `browser_handle` (uint32)
- `current_app_id` (uint32)
- `current_clan_id` (uint64)
- `current_steam_id` (uint64) — this is the local user's Steam ID after init
- `inventory_handle` (int32)
- `inventory_update_handle` (uint64)
- `leaderboard_handle` (uint64)
- `leaderboard_details_max` (int, capped at 256)
- `leaderboard_entries_array` (Array)
- `server_list_request` (uint64)

Convention: `get_thing()` / `set_thing(new_value)`.

### Utility
- `getSteamID32(steam_id)` — convert a SteamID64 to a SteamID32.
- `get_godotsteam_version()` — get the current GodotSteam version string.

### Account type checks
Boolean checks on a Steam ID:
- `isAnonAccount(steam_id)`
- `isAnonUserAccount(steam_id)`
- `isChatAccount(steam_id)`
- `isClanAccount(steam_id)`
- `isConsoleUserAccount(steam_id)`
- `isIndividualAccount(steam_id)`
- `isLobby(steam_id)`

---

## Properties

Each has a matching get/set pair listed above:

| Name | Type |
|---|---|
| `current_app_id` | uint32 |
| `current_browser_handle` | uint32 |
| `current_clan_id` | uint64 |
| `current_steam_id` | uint64 |
| `inventory_handle` | int32 |
| `inventory_update_handle` | uint64 |
| `leaderboard_details_max` | int |
| `leaderboard_entries_array` | Array |
| `leaderboard_handle` | uint64 |

---

## Constants — highlights

Common invalid values used as sentinels:
- `ACCOUNT_ID_INVALID` = 0
- `API_CALL_INVALID` = 0x0
- `APP_ID_INVALID` = 0x0
- `AUTH_TICKET_INVALID`
- `DEPOT_ID_INVALID` = 0x0
- `PARTY_BEACON_ID_INVALID` = 0

Steam ID utilities:
- `STEAM_ACCOUNT_ID_MASK` = 0xFFFFFFFF — used to mask out the AccountID_t
- `STEAM_ACCOUNT_INSTANCE_MASK` = 0x000FFFFF
- `STEAM_ID_NIL` — generic invalid CSteamID
- `STEAM_USER_DEFAULT_INSTANCE` = 1 (desktop client)
- `STEAM_USER_CONSOLE_INSTANCE` = 2
- `STEAM_USER_WEB_INSTANCE` = 4

Query port errors (for game server info):
- `QUERY_PORT_ERROR` = 0xFFFE — couldn't get the query port
- `QUERY_PORT_NOT_INITIALIZED` = 0xFFFF — haven't asked yet

Buffer sizes (custom to GodotSteam):
- `STEAM_BUFFER_SIZE` = 255
- `STEAM_LARGE_BUFFER_SIZE` = 8160
- `STEAM_MAX_ERROR_MESSAGE` = 1024
- `GAME_EXTRA_INFO_MAX` = 64

---

## Enums — highlights

### `SteamAPIInitResult` (returned by `steamInitEx`)
- `STEAM_API_INIT_RESULT_OK` = 0 — initialization successful
- `STEAM_API_INIT_RESULT_FAILED_GENERIC` = 1 — other failure
- `STEAM_API_INIT_RESULT_NO_STEAM_CLIENT` = 2 — Steam probably not running
- `STEAM_API_INIT_RESULT_VERSION_MISMATCH` = 3 — Steam client out of date

### `AccountType`
- `ACCOUNT_TYPE_INVALID` = 0
- `ACCOUNT_TYPE_INDIVIDUAL` = 1 — single user account
- `ACCOUNT_TYPE_MULTISEAT` = 2 — cybercafe etc.
- `ACCOUNT_TYPE_GAME_SERVER` = 3
- `ACCOUNT_TYPE_ANON_GAME_SERVER` = 4
- `ACCOUNT_TYPE_PENDING` = 5
- `ACCOUNT_TYPE_CONTENT_SERVER` = 6
- `ACCOUNT_TYPE_CLAN` = 7
- `ACCOUNT_TYPE_CHAT` = 8
- `ACCOUNT_TYPE_CONSOLE_USER` = 9
- `ACCOUNT_TYPE_ANON_USER` = 10

### `AuthSessionResponse` (from Steam ticket auth)
Values 0–10. Most useful: OK (0), USER_NOT_CONNECTED_TO_STEAM (1), NO_LICENSE_OR_EXPIRED (2), VAC_BANNED (3), LOGGED_IN_ELSEWHERE (4), AUTH_TICKET_CANCELED (6), AUTH_TICKET_INVALID (8), PUBLISHER_ISSUED_BAN (9).

### `BeginAuthSessionResult`
Values 0–5. OK, INVALID_TICKET, DUPLICATE_REQUEST, INVALID_VERSION, GAME_MISMATCH, EXPIRED_TICKET.

### `BetaBranchFlags`
Bit-flag enum for beta branch state: NONE (0), DEFAULT (1, "public"), AVAILABLE (2), PRIVATE (4, password protected), SELECTED (8, currently active), INSTALLED (16, currently mounted).

### `ChatEntryType`
For chat messages: INVALID (0), CHAT_MSG (1), TYPING (2), INVITE_GAME (3), EMOTE (4, deprecated), LEFT_CONVERSATION (6), ENTERED (7), WAS_KICKED (8), WAS_BANNED (9), DISCONNECTED (10), HISTORICAL_CHAT (11), LINK_BLOCKED (14).

### `ChatRoomEnterResponse`
1 = success. Values 2–15 cover various join failures: doesn't exist, not allowed, full, banned, community ban, member blocked, rate-limited, etc.

### `NotificationPosition` (for Steam overlay notifications)
- POSITION_TOP_LEFT (0), POSITION_TOP_RIGHT (1), POSITION_BOTTOM_LEFT (2), POSITION_BOTTOM_RIGHT (3), POSITION_INVALID (-1).

### `Result` (huge — 130+ entries; most common):
- RESULT_OK = 1
- RESULT_FAIL = 2
- RESULT_NO_CONNECTION = 3
- RESULT_INVALID_PASSWORD = 5
- RESULT_INVALID_PARAM = 8
- RESULT_TIMEOUT = 16
- RESULT_ACCESS_DENIED = 15
- RESULT_INSUFFICIENT_PRIVILEGE = 24
- RESULT_LIMIT_EXCEEDED = 25
- RESULT_BANNED = 17
- RESULT_ACCOUNT_NOT_FOUND = 18
- RESULT_INVALID_STEAMID = 19
- RESULT_SERVICE_UNAVAILABLE = 20
- RESULT_NOT_LOGGED_ON = 21
- RESULT_PENDING = 22
- Many more application-specific ones (family sharing, market limits, phone auth, etc.). Consult the full source when you hit a specific error.

### Other enums present (less critical for initial dev)
`BroadcastUploadResult`, `ChatSteamIDInstanceFlags`, `DenyReason`, `DurationControlProgress`, `DurationControlOnlineState`, `DurationControlNotification`, `GameIDType`, `IPType`, `IPv6ConnectivityProtocol`, `IPv6ConnectivityState`, `MarketNotAllowedReasonFlags`, `Universe`, `UserHasLicenseForAppResult`, `VoiceResult`.

---

## Related classes in the GodotSteam API (linked from this page)

For our project, the most relevant ones to fetch when needed:
- **MultiplayerPeer** — the abstraction we'll use for coop networking on top of Steam
- **Networking Sockets** — modern Steam relay networking
- **Networking Messages** — packet-based messaging
- **Matchmaking** — lobby creation and discovery
- **User** — local user info (name, Steam ID, avatar hooks)
- **Friends** — for friend-based invites
- **Apps** — DLC checks, launch parameters
- **Remote Storage** — Steam Cloud saves

Lower priority:
- HTML Surface, HTTP, Input, Inventory, Music, Music Remote, Parental Settings, Parties, Remote Play, Screenshots, Timeline, UGC (Workshop), User Stats, Utils, Video, App Lists, Game Search, Game Server, Game Server Stats, Matchmaking Servers.

---

## Related tutorials (linked from this page)

- Initializing Steam — how to boot Steamworks cleanly
- C# — how to use GodotSteam from C# projects (**critical for us**)
- Lobbies — creating/joining lobbies
- MultiplayerPeer — using GodotSteam's MultiplayerPeer implementation
- Networking Sockets — direct low-level networking
- Networking Messages — higher-level messaging
- Friends' Lobbies — friend-based joining
- Authentication — Steam ticket auth for anti-cheat
- Exporting and Shipping — packaging for Steam release
- Remove Steam — how to strip out Steam integration for non-Steam builds

---

*End of snapshot.*
