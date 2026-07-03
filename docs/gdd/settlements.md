# Mankers Kingdoms — Settlement Permissions (GDD)

**Status:** v0.1 — v1 section is locked scope (PRD §4.6). Roadmap section is design-ahead, not yet scheduled.

**Related:** `PRD.md` §4.6, `VERTICAL_SLICE.md` §3.5, §4

---

## 1. v1 scope — Founder / Guest (locked)

Two roles only. No promotion, no hierarchy, no transfer of founder status in v1.

### 1.1 Founder

- Automatically assigned to whoever plants the settlement's **Kingdom Marker**
- Exactly one founder per settlement, permanent for v1 (no transfer mechanic)
- A player can found multiple settlements (see PRD §4.6 "players can found their own or contribute to another's") — founder role is per-settlement, not global

### 1.2 Guest

- Automatically assigned to any player present at a settlement who is not its founder
- No invite step needed in v1 — presence at the settlement is sufficient (matches "no ownership permission tiers beyond founder" simplicity goal). Revisit if this causes problems in playtesting (e.g. griefing via uninvited guests) — see Open Questions.

### 1.3 Permission table

| Action | Founder | Guest |
|---|---|---|
| Plant Kingdom Marker (found settlement) | — (one-time, makes you founder) | ❌ |
| Place buildings | ✅ | ❌ |
| Demolish buildings | ✅ | ❌ |
| Deposit resources into shared storage | ✅ | ✅ |
| Withdraw resources from shared storage | ✅ | ✅ |
| Use crafting stations (Workbench, Cooking Fire, etc.) | ✅ | ✅ |
| Sleep in Shelter (respawn point, rest recovery) | ✅ | ✅ |
| Assign NPCs to stations | ✅ | ❌ |
| Un-assign NPCs from stations | ✅ | ❌ |
| Recruit NPCs into the settlement | ✅ | ❌ |
| Set settlement's respawn point | ✅ | ❌ |

**Decision on the previously-open question:** guests CAN withdraw from shared storage, not just deposit. Rationale: coop trust is already implicit in who's present at your settlement (you chose to let them in), and restricting withdrawal adds friction without meaningfully reducing griefing risk in a 2–6 player coop game. Revisit if playtesting shows this causes problems.

### 1.4 Enforcement

Server-authoritative, per ADR-0005. Every action in the table above is validated server-side against the acting player's role for that specific settlement before being applied — never trust client-side permission checks alone (client can grey out buttons for UX, but the server is the actual gate).

Suggested implementation shape: each settlement entity stores `FounderId` (player EntityId). Any privileged action's server handler checks `actingPlayer.Id == settlement.FounderId` before proceeding. Simple boolean check for v1 — no role enum needed yet since there are only two states (founder / not-founder).

### 1.5 What's explicitly NOT in v1

- No promotion/demotion (no Co-Founder, Officer, Member tiers — see roadmap below)
- No founder transfer (if the founder leaves permanently or goes offline, the settlement has no mechanism to reassign founder status in v1)
- No kick/ban mechanic for unwanted guests
- No invite system — presence-based guest status only
- No per-action fine-grained permissions (e.g. "can withdraw wood but not stone") — all-or-nothing per the table above

---

## 2. Roadmap — full role hierarchy (post-slice, not yet scheduled)

Design-ahead only. Not scheduled to any milestone. Captured here so the design doesn't need to be re-derived later, and so v1's founder/guest model has an obvious extension path rather than needing a rework.

### 2.1 Proposed roles

| Role | Granted by | Notes |
|---|---|---|
| **Founder** | Automatic (planted the Kingdom Marker) | Can promote/demote all other roles, transfer founder status to another player, disband the settlement. Only one per settlement. |
| **Co-Founder** | Founder only | All permissions except transfer founder status or disband. Effectively a second founder. |
| **Officer** | Founder or Co-Founder | Build, demolish, assign/recruit NPCs. Cannot promote/demote or manage settlement-level settings (respawn point, alignment, etc.) |
| **Member** | Founder or above | Deposit/withdraw storage, use stations, sleep. No building rights. Functionally similar to v1's "guest" but explicitly invited/persistent rather than presence-based. |
| **Guest** | Automatic on arrival, uninvited | Same restricted permissions as v1 guest. The "default" state for anyone not explicitly given a role. |

### 2.2 Why this hierarchy

- Mirrors common coop-game guild/clan patterns (rank ladders players already understand)
- Gives a real reason to *want* promotion — a coop-native progression axis independent of character skills
- **Member vs. Guest distinction solves the "presence-based guest" limitation from v1** — a trusted regular player can be promoted to Member and keep their access even when the founder isn't around to vouch for them each session
- Cleanly extends the presence-gated building rule already locked (PRD §4.6) — class-gated buildings could eventually also check for Officer+ presence of the right class, not just any class member being physically present

### 2.3 Open design questions for when this is built

1. Does Co-Founder require a class (or can any Founder promote regardless of the promoted player's class)?
2. Can multiple Co-Founders exist, or is it capped at one (like Founder)?
3. What happens to role assignments if a settlement is captured via the conquest mechanic (ADR-0015)? Fresh founder = the conqueror, or does it inherit some structure?
4. Should role changes be logged/visible (a simple settlement activity log) for transparency in larger coop groups?
5. Does role apply per-settlement only, or does a "Member" status at Settlement A mean anything at Settlement B founded by the same player?

---

## 3. Modding surface

- Founder/Guest check in v1 is a simple boolean, not data-driven — low modding relevance for v1
- When the full role hierarchy is built, role definitions and their default permission sets should be data-driven (`data/base/roles/*.json`) so mods can add custom roles or adjust default permissions per role, consistent with ADR-0009 (Tier 1 modding)

---

## 4. Open questions

1. **Presence-based guest status (v1):** is showing up at someone's settlement enough to become a "guest" with deposit/withdraw/use rights, or should there be a minimal gate (e.g. founder must be online / must have visited once)? Currently locked to "presence is enough" per PRD simplicity goal — flag if playtesting surfaces griefing concerns.
2. **Founder offline/permanent departure (v1):** no mechanic exists to reassign founder status if they stop playing. Acceptable gap for a 2-player vertical slice; revisit before any 4-6 player alpha testing.
