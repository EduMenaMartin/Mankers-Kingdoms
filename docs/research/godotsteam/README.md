# GodotSteam Documentation Snapshots

**Purpose:** GodotSteam docs (https://godotsteam.com) block Claude Code's automated fetching. This folder holds locally-vendored snapshots of the pages Claude Code needs to reference during development.

## How this works

Each `.md` file here is a fetched-and-summarized snapshot of one page from https://godotsteam.com, with:
- Source URL at the top
- Date fetched
- License / attribution note
- A structured summary of the content

## Refresh policy

**Re-fetch when:** we upgrade GodotSteam versions, or when we hit an issue and suspect our snapshot might be stale.

**Fetch process:** ask Claude in the chat interface (not Claude Code) to fetch the page — the chat interface has a different fetch path that isn't blocked. Save the result here with the date at the top.

## Files

- `main-class.md` — the `Main` class (init/shutdown, handles, account checks, key enums). Fetched 2026-07-02.

## Pages to fetch as needed

Priority order for our project:
1. `tutorials/c-sharp/` — how to use GodotSteam from C# (essential)
2. `tutorials/initializing/` — clean init pattern
3. `classes/multiplayer_peer/` — MultiplayerPeer API
4. `tutorials/multiplayer_peer/` — MultiplayerPeer tutorial
5. `tutorials/lobbies/` — lobby creation/joining
6. `classes/matchmaking/` — matchmaking API
7. `tutorials/networking_sockets/` — Steam Datagram Relay
8. `howto/gdextension/` — GDExtension setup notes
9. `tutorials/friends_lobbies/` — friend-based joining
10. `tutorials/exporting_shipping/` — packaging for release

Others can wait until we hit specific features.

## Not for redistribution

These snapshots are for our development use only. The documentation belongs to the GodotSteam project (GP Garcia and contributors). If we ever open-source Mankers Kingdoms, remove this folder or replace with links.
