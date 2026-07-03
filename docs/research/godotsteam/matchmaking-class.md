# GodotSteam — Matchmaking Class Reference (Snapshot)

**Source:** https://godotsteam.com/classes/matchmaking/
**Fetched:** 2026-07-02
**License:** Documentation belongs to GodotSteam project (GP Garcia | Contributors). Local development reference only.
**Purpose:** local reference for Mankers Kingdoms M1.5 (ENet → GodotSteam P2P swap). Site blocks Claude Code's automated fetches.

Only available in the main GodotSteam branches.

---

## Functions relevant to our lobby-based host/join flow

### createLobby

```
createLobby( LobbyType lobby_type, int max_members )
```

| Parameter | Type | Notes |
|---|---|---|
| lobby_type | LobbyType enum | Visibility/type of lobby. Changeable later via setLobbyType. |
| max_members | int | Max players. Cannot exceed 250. |

Creates a lobby on Steam's servers. If private, won't show in `requestLobbyList` — share the lobby ID via invite instead.

**Asynchronous.** Returns void immediately; results come via the `lobby_created` signal AND `lobby_joined` signal (host auto-joins their own lobby) AND `lobby_data_update` signal.

### joinLobby

```
joinLobby( uint64_t steam_lobby_id )
```

Joins an existing lobby (ID obtained from search, friend invite, or direct share).

**Triggers:** `lobby_joined` signal for yourself; `lobby_chat_update` signal for everyone already in the lobby.

### leaveLobby

```
leaveLobby( uint64_t steam_lobby_id )
```

Leaves immediately, client-side. Others notified via `lobby_chat_update`.

### getLobbyOwner

```
getLobbyOwner( uint64_t steam_lobby_id )
```

Returns the Steam ID (uint64_t) of the current lobby owner/host. You must be a lobby member to call this. **This is what MultiplayerPeer's `connect_to_lobby` uses internally to find who to `create_client` against.**

### getNumLobbyMembers / getLobbyMemberByIndex

```
getNumLobbyMembers( uint64_t steam_lobby_id )        // returns int count
getLobbyMemberByIndex( uint64_t steam_lobby_id, int member )  // returns Steam ID
```

Call `getNumLobbyMembers` first, then iterate with `getLobbyMemberByIndex` to enumerate all lobby members' Steam IDs. Used for building the peer list.

### setLobbyData / getLobbyData

```
setLobbyData( uint64_t steam_lobby_id, string key, string value )   // bool, owner only
getLobbyData( uint64_t steam_lobby_id, string key )                  // string
```

Key/value metadata on the lobby (e.g. lobby display name, game mode tag). Owner-only to set. Propagates to all members via `lobby_data_update` signal.

### requestLobbyList / filters

```
requestLobbyList()   // triggers lobby_match_list signal with Array of lobby IDs
addRequestLobbyListDistanceFilter( LobbyDistanceFilter )
addRequestLobbyListFilterSlotsAvailable( int slots_available )
addRequestLobbyListResultCountFilter( int max_results )
addRequestLobbyListStringFilter( string key, string value, LobbyComparison comparison )
```

Filters must be added BEFORE calling `requestLobbyList()` — they're cleared after each call. Only returns non-full `LOBBY_TYPE_PUBLIC` or `LOBBY_TYPE_INVISIBLE` lobbies that have `setLobbyJoinable(true)` (default).

### sendLobbyChatMsg

```
sendLobbyChatMsg( uint64_t steam_lobby_id, string message_body )
```

Broadcasts text/binary to all lobby members via Steam backend (not your P2P game networking — separate, lower-bandwidth channel). Good for chat and simple host-arbitrated commands (e.g. kick).

### setLobbyJoinable

```
setLobbyJoinable( uint64_t steam_lobby_id, bool joinable )
```

Owner-only. Set `false` to lock the lobby once the match starts — prevents new joins and removes it from search results.

### inviteUserToLobby

```
inviteUserToLobby( uint64_t steam_lobby_id, uint64_t steam_id_invitee )
```

Direct invite. If accepted while the invitee is already running the game, fires their `join_requested` signal. If not running, Steam launches the game with `+connect_lobby <lobby_id>` on the command line — must be handled at startup (see Lobbies tutorial).

---

## Signals (require callback setup per Initializing tutorial)

| Signal | Fires when | Key fields |
|---|---|---|
| `lobby_created` | After `createLobby()` resolves | `connect` (Result enum), `lobby` (uint64_t, 0 if failed) |
| `lobby_joined` | After `joinLobby()` resolves, or automatically for host | `lobby_id`, `permissions` (unused), `locked`, `response` (ChatRoomEnterResponse — check for `CHAT_ROOM_ENTER_RESPONSE_SUCCESS`) |
| `lobby_chat_update` | Member joins/leaves/disconnects/kicked/banned | `lobby_id`, `changed_id`, `making_change_id`, `chat_state` (bitfield) |
| `lobby_data_update` | Lobby or member metadata changed | `success`, `lobby_id`, `member_id` |
| `lobby_match_list` | After `requestLobbyList()` resolves | Array of lobby IDs (uint64_t) |
| `lobby_message` | Chat message received | `lobby_id`, `user`, `buffer` (message text), `chat_type` |
| `lobby_invite` | Someone invited you | `inviter`, `lobby`, `game` |
| `lobby_kicked` | You were force-removed | `lobby_id`, `admin_id`, `due_to_disconnect` |

**Also relevant from Friends class (not Matchmaking):** `join_requested` — fires when a friend accepts an invite while you're already running the game.

---

## Key enums

### LobbyType

| Value | Meaning |
|---|---|
| `LOBBY_TYPE_PRIVATE` (0) | Invite-only, not searchable |
| `LOBBY_TYPE_FRIENDS_ONLY` (1) | Visible to friends, not in public lobby list |
| `LOBBY_TYPE_PUBLIC` (2) | Visible to friends AND public lobby list — **likely our default for M1.5 testing** |
| `LOBBY_TYPE_INVISIBLE` (3) | Searchable but not shown to friends |
| `LOBBY_TYPE_PRIVATE_UNIQUE` (4) | Special case, rarely used |

### LobbyDistanceFilter

`LOBBY_DISTANCE_FILTER_CLOSE` (0) / `DEFAULT` (1) / `FAR` (2) / `WORLDWIDE` (3) — affects search radius by IP-based region. Default is fine for now; only relevant once we implement lobby search/browse (not needed for M1.5's direct friend-join flow).

---

## What M1.5 actually needs from this class

Given M1.5's scope is "friends join via Steam" (not public lobby browsing), the minimal function set is:

1. **Host:** `createLobby(LOBBY_TYPE_FRIENDS_ONLY or PUBLIC, max_players)` → wait for `lobby_created` + `lobby_joined` signals → optionally `setLobbyData` for a display name
2. **Client (via Steam friend invite/overlay):** Steam handles the invite UI natively (`inviteUserToLobby` on host side, or players use Steam's built-in "Join Game" from friends list) → client's `join_requested` (Friends class) or command-line `+connect_lobby` fires → call `joinLobby(lobby_id)` → wait for `lobby_joined` signal
3. **Both:** once `lobby_joined` fires successfully, call `MultiplayerPeer.host_with_lobby(lobby_id)` (host) or `MultiplayerPeer.connect_to_lobby(lobby_id)` (client) — see `multiplayer-peer-class.md` snapshot

We do NOT need `requestLobbyList` / search filters for M1.5 — that's for public lobby browsing, which is a post-slice feature (auto-matchmaking, not friend-invite). Skip that function set for now.

---

*End of snapshot.*
