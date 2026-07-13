# Current Milestone: M8 — Save/load and polish

**Started:** 2026-07-11
**Target demo:** Play for 30 minutes, quit, restart, resume exactly where left off.

## Scope (from VERTICAL_SLICE.md §3.5 + §5 M8)

- JSON save/load of full world state (terrain seed, buildings, stockpile, NPC assignments, player inventories, positions, needs, skills, HP) ⬜
- Autosave every 5 minutes + on host exit ⬜
- Client reconnect handling ⬜
- All existing systems tested with save→quit→reload cycle ⬜
- Basic UI polish (readable, not pretty) ⬜
- Localization file audit — confirm no hardcoded strings remain in gameplay code ⬜
- Fog of war probe — full-map toggle screen (unseen/previously-seen/currently-visible), shared reveal across party, persisted in save data (see `docs/gdd/worldgen.md` §11) ⬜
- Demo gate ⬜

## Key decisions (to be locked during M8)

- **Save format scope:** which systems serialize first? Suggested order: world seed → buildings → stockpile → NPC assignments → inventories → skill levels → player HP/needs → positions.
- **Fog of war implementation:** full per-tile reveal map vs. radial snapshot — see `docs/gdd/worldgen.md` §11 for locked probe spec.
- **Client reconnect:** does reconnect replay the full state via RPCs from server, or read a shared save file?

## Out of scope for M8

- Binary save format optimization (JSON for prototype)
- Steam cloud saves
- More than one language exposed
- Tutorial / onboarding
- Content additions (new buildings, monsters, items)
