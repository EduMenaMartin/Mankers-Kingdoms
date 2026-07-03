# GodotSteam — MultiplayerPeer Class Reference (Snapshot)

**Source:** https://godotsteam.com/classes/multiplayer_peer/
**Fetched:** 2026-07-02
**License:** Documentation belongs to GodotSteam project (GP Garcia | Contributors). Retained here as a local development reference only.
**Purpose:** local reference for Mankers Kingdoms M1.5 networking work (swap ENet → GodotSteam P2P). The godotsteam.com site blocks Claude Code's automated fetches.

**IMPORTANT NAMING NOTE:** This class is called `MultiplayerPeer` in GodotSteam's own docs — NOT "SteamMultiplayerPeer". There is a separate, unrelated, now-paused community plugin called `expressobits/steam-multiplayer-peer` that also uses a class people informally call "SteamMultiplayerPeer" in casual writing/tutorials — do not confuse the two APIs. We are using GodotSteam's own built-in `MultiplayerPeer` (part of the GDExtension already installed per ADR-0010), documented below.

Only available in the main GodotSteam branches (not server-only branches).

---

## Functions

### create_host

```
create_host( int virtual_port = 0 )
```

| Parameter | Type | Notes |
|---|---|---|
| virtual_port | int | Expected to be 0; only helpful for multiple connections between two users. Defaults to 0. |

On success: sets `server` to `true`, `unique_id` to `1`. Internally creates a listen socket via `createListenSocketP2P` and a poll group via `createPollGroup`. Sets `set_refuse_new_connections` to `false`, `connection_status` to `CONNECTION_CONNECTED`.

**Returns:** Error enum. `OK` on success; `ERR_ALREADY_IN_USE` if `connection_status` isn't `CONNECTION_DISCONNECTED`.

### create_client

```
create_client( uint64_t steam_id, int virtual_port = 0 )
```

| Parameter | Type | Notes |
|---|---|---|
| steam_id | uint64_t | Steam ID of the host we're connecting to. |
| virtual_port | int | Expected to be 0; same caveat as above. |

On success: sets `server` to `false`, `unique_id` to a newly generated unique ID. Creates a listen socket (`createListenSocketP2P`) and poll group (`createPollGroup`) internally. Calls `add_peer` with the host's `steam_id` and `virtual_port`. Sets `set_refuse_new_connections` to `false`, `connection_status` to `CONNECTION_CONNECTING`.

**Returns:** Error enum. `OK` on success; `ERR_ALREADY_IN_USE` if `connection_status` isn't `CONNECTION_DISCONNECTED`.

### add_peer

```
add_peer( uint64_t steam_id, int virtual_port = 0 )
```

| Parameter | Type | Notes |
|---|---|---|
| steam_id | uint64_t | Steam ID of the player to add as a peer. |
| virtual_port | int | Expected to be 0. |

Creates a connection with a new peer. Runs `connectP2P` (Networking Sockets class) under the hood.

**Returns:** Error enum. `OK` on success; `ERR_CANT_CREATE` if the underlying P2P connection fails.

### host_with_lobby

```
host_with_lobby( uint64_t lobby_id )
```

| Parameter | Type | Notes |
|---|---|---|
| lobby_id | uint64_t | The lobby ID we are hosting. |

**Prerequisite:** you must have already created a lobby via `createLobby` (Matchmaking class) before calling this. If you pass a `lobby_id` you don't own, errors with `ERR_CANT_CREATE`.

Sets `tracked_lobby` to this lobby on success. **Calls `create_host` AND `add_peer` for all existing lobby members automatically.** This is the convenience wrapper — likely what we want for the M1.5 host flow rather than calling `create_host` bare.

**Returns:** Error enum. `OK` on success; `ERR_ALREADY_IN_USE` if `connection_status` isn't `CONNECTION_DISCONNECTED`.

### connect_to_lobby

```
connect_to_lobby( uint64_t lobby_id )
```

| Parameter | Type | Notes |
|---|---|---|
| lobby_id | uint64_t | The Steam lobby ID to connect to. |

Verifies you're in the lobby via `getLobbyOwner` (Matchmaking class), or errors with `ERR_CANT_CREATE`. **Calls `create_client` for the host automatically**, or prints an error if no host is found. Then connects to every other lobby member by calling `add_peer` for each. This is the convenience wrapper — likely what we want for the M1.5 join flow rather than calling `create_client` bare.

**Returns:** Error enum. `OK` on success; `ERR_ALREADY_IN_USE` if `connection_status` isn't `CONNECTION_DISCONNECTED`.

### get_peer

```
get_peer( int peer_id )
```

Returns a `SteamPacketPeer` RefCounted object for the given peer ID, or `null` if not found.

### get_peer_id_for_steam_id

```
get_peer_id_for_steam_id( uint64_t steam_id )
```

Returns the Godot multiplayer peer ID for a given Steam ID (or the local `unique_id` if passing your own Steam ID). Returns `0` if not found.

### get_steam_id_for_peer_id

```
get_steam_id_for_peer_id( int peer_id )
```

Returns the Steam ID for a given Godot peer ID (or the local user's Steam ID if passing your own `unique_id`). Returns `0` if not found.

### Debug / tuning setters/getters

- `set_debug_level(DebugLevel)` / `get_debug_level()` — internal debug_level property
- `set_no_delay(bool)` / `get_no_delay()` — whether to use `NETWORKING_SEND_NO_DELAY` flag
- `set_no_nagle(bool)` / `get_no_nagle()` — whether to use `NETWORKING_SEND_NO_NAGLE` flag

---

## Properties

| Name | Type | Set | Get |
|---|---|---|---|
| debug_level | int | set_debug_level | get_debug_level |
| no_delay | bool | set_no_delay | get_no_delay |
| no_nagle | bool | set_no_nagle | get_no_nagle |

---

## Enums

### DebugLevel

| Enumerator | Value |
|---|---|
| DEBUG_LEVEL_NONE | 0 |
| DEBUG_LEVEL_PEER | 1 |
| DEBUG_LEVEL_STEAM | 2 |

---

## Recommended usage pattern for Mankers Kingdoms M1.5

Based on the API, the intended flow is:

**Host:**
1. Call `Matchmaking.createLobby(...)` — get a `lobby_id` back (async, via callback/signal)
2. Call `host_with_lobby(lobby_id)` — this internally does `create_host` + `add_peer` for all existing members
3. Assign the resulting peer to `Multiplayer.MultiplayerPeer` in Godot's high-level API

**Client (joining):**
1. Obtain the `lobby_id` (via Steam friend invite, lobby list, etc. — see Matchmaking/Lobbies docs)
2. Join the lobby via `Matchmaking.joinLobby(lobby_id)`
3. Call `connect_to_lobby(lobby_id)` — this internally finds the host and calls `create_client` + `add_peer` for all members
4. Assign the resulting peer to `Multiplayer.MultiplayerPeer`

This means `host_with_lobby` / `connect_to_lobby` are almost certainly the two methods `NetworkManager.cs` should call — not the raw `create_host` / `create_client`, unless there's a specific reason to manage peer connections outside Steam's lobby system entirely.

**Not yet fetched, needed next:** the Matchmaking class (`createLobby`, `joinLobby`, `getLobbyOwner`) and the Lobbies tutorial, to see the full host/join flow including the async callback signals Godot needs to wire up.

---

*End of snapshot.*
