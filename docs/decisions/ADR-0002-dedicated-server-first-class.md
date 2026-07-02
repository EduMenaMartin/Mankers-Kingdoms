# ADR-0002: Dedicated server architecture from day one

**Status:** Accepted
**Date:** 2026-07-02
**Deciders:** Edu + Claude

## Context

Many coop survival games ship without dedicated server support and add it later — Valheim, Enshrouded, Palworld, Aska. In every case, the retrofit takes months to years because the code assumed "host = a player's machine running the full game." Refactoring means separating the sim from client-only concerns (rendering, input, UI, local file system, audio) throughout the codebase.

The technique that avoids this: treat the host as a special case of a dedicated server that also happens to run a local client. If server code never depends on client scenes existing, dedicated server support is a build flag, not a project.

Mankers Kingdoms commits to persistent Valheim-model worlds and community-hostable servers. Without dedicated server support, community longevity is capped — hosts have to be online for anyone else to play.

## Decision

The host is a dedicated server that also runs a local client. Server code never assumes UI, input, or rendering exists. Dedicated server mode is invoked by a `--headless --mode=dedicated` flag on the same binary, with the client scene tree never loaded.

Enforced by:
- Folder structure: `/project/scripts/server/`, `/project/scripts/client/`, `/project/scripts/shared/`
- Import rules: server never imports from client; client never imports from server; both may import from shared
- CI check: static grep-based enforcement of the import rules
- Header comment convention on every server file

## Consequences

**Positive:**
- Dedicated server support is not a separate project — it's a runtime configuration
- Testing the dedicated path = running the game headless (which CI does automatically)
- Community server hosting becomes viable from launch
- Code discipline pays back in cleaner architecture generally

**Negative:**
- ~10–20% extra design overhead per feature — every new system asks "does this touch client or server or both?"
- Some conveniences (e.g. debug print + immediate UI feedback) are harder in server code

**Accepted:** the up-front cost. The alternative — retrofitting later — is 6+ months of pain.

## Alternatives considered

- **Host-only, ship dedicated later.** Rejected — well-documented failure mode.
- **Peer-to-peer with rotating host.** Rejected — no dedicated support, poor persistence guarantees.

## References

- PRD.md §4.2, §8
- ARCHITECTURE.md §3, §4
- Discussed extensively in project design conversations
