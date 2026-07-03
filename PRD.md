# Mankers Kingdoms — Product Requirements Document

**Status:** v0.1 — living document. All content lists (skills, stats, buildings, classes, monsters) are v1 prototype scope and expected to evolve. Architectural decisions are locked and require an ADR to change.

**Last updated:** 2026-07-02

---

## 1. Vision

### 1.1 North star

Mankers Kingdoms is a cooperative top-down survival and settlement-building game in a mystical D&D-flavored fantasy world, where 2–6 players each pick a class and carve out their own petty kingdoms — shared or rival — in a persistent, procedurally generated bounded realm.

The design spiritual-successors 1993's *D&D Stronghold: Kingdom Simulator*, modernized with WASD avatar coop, Aska/Bellwright station-based village management, and RuneScape-style use-to-level skills soft-capped by AD&D character stats.

### 1.2 Design pillars

Pillars are the tiebreakers when two features conflict. If a proposed feature violates a pillar, the pillar wins unless we explicitly change it via ADR.

1. **Coop-first, every player is a monarch.** Multiplayer is the default assumption, not a mode. Every player can found or contribute.
2. **Class identity, individual skills.** Class at creation for identity; skills grow through use, soft-capped by rolled stats.
3. **Settlements are earned.** No pre-placed towns. Claim, defend, grow.
4. **Real-time, no pause.** Coop presence preserved. No pause exists in multiplayer.
5. **Permanent character identity.** Rolled stats gate skill ceilings. Every pawn is unique.
6. **Modding from day one.** Base game is a content pack. Data-driven, extensible.
7. **Dedicated server as architecture, not afterthought.** Enforced from repo initialization.

---

## 2. Reference matrix

| Reference | What we take | What we reject |
|---|---|---|
| D&D Stronghold (1993) | Class-shapes-kingdom, alignment victory, class buildings | Single-player only |
| Valheim | WASD coop, persistent hosted world, dedicated server, exploration progression | Norse setting, minimal recruitment |
| Aska | Station-based assignment, top-down feel, procedural map | Norse setting, no classes |
| Bellwright | Recruitment loop, needs, job assignment | Historical medieval, no coop |
| RuneScape: Dragonwilds | Use-to-level skills, coop survival, multiclass emergence | MMO scope |
| RimWorld | NPC identity depth from stats | Paused tactical, priority-list jobs, disembodied control |
| Dwarf Fortress | Depth of character | Everything else |
| Medieval Dynasty | Coop settlement contribution model | Historical setting, first-person |

---

## 3. Core loops

### 3.1 30-second loop (moment-to-moment)

Player walks to workstation → assigns NPC or gathers themselves → resource enters storage → decides next action based on visible needs.

### 3.2 5-minute loop (session tempo)

Check settlement state → address one or two pending needs (food, defense, construction) → coordinate with coop partners → travel to nearby node (village, monster nest, resource cluster) → return with progress.

### 3.3 Session loop (a play session)

Log in → assess what happened in your absence (if any) → advance a mid-term goal (recruit a Cleric to unlock the temple, defeat a bandit camp threatening the eastern road, complete the smithy) → set up work for after you log out.

### 3.4 Campaign loop (arc of a long game)

Land in the realm → survive first days → found settlement → recruit first NPCs → expand and specialize → confront regional threats → unlock class-gated buildings via recruitment → make your kingdom distinctive → optionally pursue selected win condition (or continue sandbox).

---

## 4. Locked design decisions

Each entry references the ADR where the decision is recorded.

### 4.1 Perspective and control

- **Top-down camera, WASD-controlled player avatar.** Not a disembodied cursor. See ADR-0003.
- **Real-time, no pause in multiplayer.** Single-player pause deferred; assume no pause. See ADR-0004.

### 4.2 Multiplayer architecture

- **Authoritative host model** (host runs the sim; clients are thin). Not deterministic lockstep. See ADR-0005.
- **Dedicated server support is a first-class architectural concern from day one.** Host = dedicated server that also runs a local client. See ADR-0002.
- **Target: 2–6 concurrent players.** Coop only. No PvP in v1.
- **Persistence: Valheim-model.** World and character progression persist across sessions.

### 4.3 Combat

- **Directional real-time melee.** Swing / block / dodge. No player magic in v1.
- **Ranged combat in v1.** Bow-based ranged attacks with aim, fire, and projectile trajectory. Ranger is the ranged-primary class in v1. See ADR-0013.
- **Monster and environmental magic** exists in world (traps, hazards, monster abilities).
- **Player magic** deferred to post-prototype. When added, will use the same skill-based use-to-level system as all other progression. See ADR-0006.

### 4.4 Character progression — the soft-class model

- **Class picked at character creation** for identity, starting kit, and initial skill bumps.
- **Skills grow through use** (RuneScape/Elder Scrolls model). No hard class boundaries.
- **Multiclass emerges organically** as players invest in off-class skills.
- **AD&D-style rolled stats act as soft caps** on skill ceilings. Stats do not decay.
- Stats and stat generation rules to be finalized in `docs/gdd/stats.md`. Proposed v1 stats: **Strength, Dexterity, Constitution, Wisdom** (subset of AD&D's six for prototype simplicity; expandable to full six — adding Int and Cha — at 1.0).

**Skill framework** — see `docs/gdd/skills.md` for full detail. Summary:

- Adopts the **SkillSetRPG 4-group structure** (Physique, Intellect, Charisma, Magic) plus a **5th Mankers Kingdoms-specific "Trades" group** for granular gathering/production professions (Aska-inspired).
- Skill levels 1–99, capped by **floor(99 × stat / 18)** — a Str 3 character caps Melee at 16; a Str 18 character caps at 99. Locked. See ADR-0019.
- Each Trades skill (Woodcutting, Stonecutting, Mining, Foraging, Farming, Fishing, Hunting, Cooking, Smithing) has **tool tier unlocks at level thresholds** — Aska pattern.
- Class starting kits provide skill bumps that give headroom, but not permanent advantage — a Fighter and a "Fighter class Ranger" converge given enough hours if their stats match.
- See ADR-0011.

### 4.5 NPCs

- **Every NPC has hidden archetype bias** (e.g. combatant / artisan / scholar / scout) plus assigned station preferences.
- **Station assignment drives skill investment.** A woodcutter NPC prioritizes Woodcutting + Str + Con regardless of archetype.
- **Stats rolled at generation, drawn from archetype-appropriate distributions.** Recruitment strategy = looking for high-stat NPCs in the professions you need.
- **NPCs level up through work and combat**, same as players.
- **NPC-only village population growth** (v1). Villages passively gain population over time. No baby/aging simulation in v1. See ADR-0007.

### 4.6 Settlements and colonies

- **Founder-gated at settlement founding.** Founder chooses starting settlement class alignment; that determines starting buildable set.
- **Presence-gated for expansion.** New buildings unlock when a member of the required class (player OR NPC) is present. Recruiting a Cleric unlocks the temple even if the founder is a Fighter.
- **Multiple settlements per world.** Players can found their own or contribute to another's.
- **Contribution model:** guests can deposit resources, use crafting stations, sleep, but not place or demolish. Only founder can grant elevated permissions in v1.

See `docs/gdd/settlements.md` for the full founder/guest permission table and roadmap role hierarchy.

### 4.7 Death and penalty

- **Respawn at base** on death.
- **Configurable penalty tied to difficulty setting:** skill loss, inventory drop, XP debt. Individual toggles at world creation.
- **Permadeath as opt-in difficulty modifier** (not default).

### 4.8 World and content

- **Small bounded procedural map** in v1. Not open streaming world.
- **Bestiary drawn from D&D SRD 5.1 (CC-BY-4.0)**. Legally clear content, familiar to genre audience. Original monsters as long-term additions.
- **Procedural villages populated with generated NPCs** for recruitment.
- **Mystical D&D-flavored tone.** Not grimdark, not comedic. High-fantasy adventuring feel.

### 4.9 Win conditions

- **Player-facing toggle at world creation.** Options:
  - **Sandbox** — no win state, play indefinitely.
  - **Sandbox + Boss** — optional milestone bosses provide closure without ending the world.
  - **Alignment-based** (Stronghold-style) — Lawful (become Emperor), Chaotic (destroy all enemy strongholds), Neutral (both).
- All three modes are the same game; only the ending conditions and endgame triggers differ. See ADR-0008.

### 4.10 Enemy AI

- **v1: static.** Monster nests and enemy villages sit until engaged.
- **Roadmap:** reactive (enemies raid based on settlement notoriety) → competitive (enemy strongholds expand and war), exposed as a "world hostility" slider.

### 4.11 Modding

- **Tier 1 (data mods) supported from launch.** See ADR-0009.
- All content — items, monsters, buildings, recipes, skills, NPC archetypes, translations — lives in data files (Godot `Resource` `.tres` files or equivalent), not in code.
- Stable string IDs for all content (`"monster.goblin.scout"`, not integer indices).
- Mod loader from repository initialization. Base game boots as a content pack.
- Server-authoritative mod validation with client handshake for MP compatibility.
- Missing content in saves degrades gracefully; does not crash.

---

## 5. Vertical slice summary

The full definition lives in `VERTICAL_SLICE.md`. Summary here:

**Goal:** Prove that WASD coop + class + settlement + station-based recruitment is fun in ~3 months of focused work.

**Scope headline:**
- 2 players over Steam (dedicated server code path exists)
- 2 classes (proposed: Fighter, Ranger — see VERTICAL_SLICE.md rationale)
- 4 stats, 4 skills
- 1 buildable settlement, ~6 building types
- 1 procedural village, 5 monster types
- Day/night, hunger, rest, save/load

**Explicitly out of vertical slice:** magic, babies, competitive AI, multiple biomes, mounts, ships, more than 2 classes, more than 1 procedural village, victory conditions beyond sandbox.

---

## 6. Deferred features roadmap

Ordered rough priority, not committed dates.

### 6.1 Post-vertical-slice (M1 → 0.1)

- Third and fourth classes
- Additional skills (target: 8–12)
- Expanded bestiary (target: 15–20 SRD monsters)
- Multiple procedural villages
- Multiple biomes on the bounded map
- Reactive enemy AI tier
- Full save/load durability and migration

### 6.2 Early alpha (0.1 → 0.3)

- Player magic system (skill-based, class-flavored)
- Full class list expansion (5–7 classes)
- Villager conversation and quest system for recruitment gates
- **Random encounter recruitment**: ambushed caravans with wounded survivors, wandering nomads seeking shelter, refugees fleeing monster attacks, hermits, deserters — pluralistic sources of NPCs beyond the procedural village. See ADR-0014.
- **Conquest mechanic**: raid enemy NPC settlements (bandit camps, goblin villages, orc holds) → intact buildings become claimable → converted structure joins player's settlement or becomes a new outpost. Foundation for the economic layer. See ADR-0015.
- **Economic layer (foundation)**: resource and manpower trading between owned settlements, between owned settlements and NPC villages, and eventually between players. First-pass simple; expands in beta.
- Alignment-based win conditions functional
- Sandbox+Boss milestone bosses
- 4–6 player scaling and tested with real playtests
- Dedicated server deployment tooling

### 6.3 Alpha to beta (0.3 → 0.8)

- Competitive enemy AI tier (rival strongholds)
- Mounts and larger-scale travel
- Modding Tier 2 (scripting hooks, plugin loader)
- Steam Workshop integration
- Advanced NPC social simulation (relationships, morale interactions)
- Passive village demographic simulation (still no per-child sim; abstract population growth with archetype variance)
- **Guild-tier settlement bonuses** for Trades skills: a Woodcutter's Guild building accumulates cumulative settlement-wide Woodcutting XP and levels up, granting passive bonuses to all Woodcutters in the settlement. Same pattern for each Trade. See `docs/gdd/skills.md` §7. See ADR-0017.

### 6.4 Beta and beyond (0.8 → 1.0 and post-1.0)

- Optional NPC aging + baby simulation (opt-in feature)
- Modding Tier 3 (total conversions, formal modding SDK)
- Localization (target: DE, ES, EN at minimum, given team language coverage)
- Steam Workshop dependency resolution
- Post-launch content DLC / free updates

---

## 7. Non-goals

**Explicit list of things we are not doing.** This is the most valuable section of the PRD long-term.

- **Not a paused tactical game.** No RimWorld-style pause and command.
- **Not a global priority-list job system.** Station assignment only.
- **Not a disembodied god-view game.** Every controlled entity is a physical avatar in the world.
- **Not a single-player-first game.** Solo play should work but is not the design driver.
- **Not a PvP game in v1.** No player-vs-player combat, no faction warfare between players.
- **Not launching with a full magic system.** Player magic is post-prototype.
- **Not launching with 100+ monsters.** Start with 5, grow deliberately.
- **Not launching with baby / aging simulation.** Abstract village growth only.
- **Not a Skyrim/BG3.** No first-person, no deep single-player narrative arc, no fully authored NPCs.
- **Not open-world streaming in v1.** Bounded map first.
- **Not deterministic lockstep MP.** Authoritative host only.
- **Not free-to-play or games-as-a-service.** Traditional one-time-purchase PC game.
- **Not shipping without dedicated server support.** Non-negotiable.
- **Not shipping without at least Tier 1 modding.** Non-negotiable.

---

## 8. Technical architecture summary

Full detail in `ARCHITECTURE.md` (to be drafted). Headlines:

- **Engine:** Godot 4 (latest stable) with C# / .NET.
- **Networking:** GodotSteam for Steam networking (P2P/relay + dedicated server). Authoritative host model. **Deterministic lockstep was considered and rejected** — see ADR-0022. Lockstep is objectively the right choice for Factorio-shaped games (huge world state, discrete inputs, cooperative, purpose-built engine) but is a poor fit for Mankers Kingdoms because of real-time directional combat, Godot 4's non-deterministic defaults, and modding fragility.
- **Architecture principle:** *The host is a dedicated server that also runs a local client.* Server logic never assumes local input, UI, or rendering exists. Enforced by folder structure (`/scripts/server/`, `/scripts/client/`, `/scripts/shared/`).
- **Simulation model:** Fixed-tick server simulation (target 20 Hz). Clients interpolate; local player uses input prediction for movement only.
- **Entity model:** ECS-lean — data separate from behavior. Not a full ECS framework (Arch/Entitas) unless we hit performance walls that justify it. Standard OOP with component-heavy composition.
- **Determinism:** Reproducible (seeded RNG, ordered iteration) where cheap, not required for MP sync. Enables debugging via replay.
- **Persistence:** World state serialized as versioned Godot resources or custom binary. Save format has a schema version from day one; migration path built in.
- **Content pipeline:** All content is data files (`.tres` resources or JSON). Base game is a content pack loaded through the mod loader.
- **Localization:** All player-facing strings externalized to per-language files (e.g. `data/lang/en.json`, `data/lang/de.json`, `data/lang/es.json`) from day one. No string literals in gameplay code. Loader picks language at startup, falls back to English on missing keys. Modders can drop new language files into `/mods/*/lang/` to add languages without code changes. Godot 4's `.po` gettext support is a candidate; final choice deferred to ARCHITECTURE.md. See ADR-0012.
- **Testing:** Unit tests (GdUnit4 or xUnit for C# libs) from repo init. Headless integration tests for server logic runnable via `godot --headless`.

---

## 9. Decisions log

Chronological record of major decisions. New entries go at the bottom. Full ADRs live in `docs/decisions/`.

| Date | Decision | ADR | Summary |
|---|---|---|---|
| 2026-07-02 | Working title | ADR-0001 | Initial working title "Petty Kingdoms" (superseded by ADR-0016) |
| 2026-07-02 | Dedicated server architecture | ADR-0002 | Dedicated server as first-class concern from day one; host is a special case of server + local client |
| 2026-07-02 | Perspective and control | ADR-0003 | Top-down camera with WASD player avatar; not RimWorld-style disembodied cursor |
| 2026-07-02 | Real-time no pause | ADR-0004 | No pause in multiplayer; single-player pause deferred; combat and crises unfold in shared time |
| 2026-07-02 | Authoritative host multiplayer | ADR-0005 | Host-authoritative sim; not deterministic lockstep; enables dedicated server; scoped for 2–6 players |
| 2026-07-02 | No player magic in v1 | ADR-0006 | Player magic deferred; monster/environmental magic in v1; skill-based magic system planned post-prototype |
| 2026-07-02 | No baby simulation in v1 | ADR-0007 | Villages grow population passively; no aging, no per-child simulation; abstract only |
| 2026-07-02 | Win condition player toggle | ADR-0008 | Sandbox / Sandbox+Boss / Alignment selectable at world creation; same game, different endings |
| 2026-07-02 | Modding Tier 1 from inception | ADR-0009 | Data-driven mods supported from launch; scripting on roadmap; total conversions long-term |
| 2026-07-02 | Engine and language | ADR-0010 | Godot 4 + C# + GodotSteam chosen over Unity+Mirror/Fish-Net and Unreal |
| 2026-07-02 | Skill framework | ADR-0011 | SkillSetRPG's 4-group structure (Physique/Intellect/Charisma/Magic) adopted; Mankers Kingdoms-specific 5th "Trades" group added for Aska-style granular gathering professions |
| 2026-07-02 | Localization architecture | ADR-0012 | Externalized per-language string files from day one; moddable via file drop; no string literals in gameplay code |
| 2026-07-02 | Ranged combat in v1 | ADR-0013 | Ranged (bow) combat added to v1 scope; Ranger class becomes ranged-primary; Fighter remains melee-primary; adds ~1–2 weeks to M4 |
| 2026-07-02 | Random encounter recruitment on roadmap | ADR-0014 | Multiple non-village NPC sources (caravans, nomads, refugees, hermits) deferred to Early Alpha 0.1→0.3 |
| 2026-07-02 | Conquest mechanic + economic layer on roadmap | ADR-0015 | Raid-and-claim enemy settlements deferred to Early Alpha; economic layer for resource/manpower trading is its natural follow-on |
| 2026-07-02 | Working title change | ADR-0016 | Renamed from "Petty Kingdoms" to "Mankers Kingdoms" (from Spanish "manco" → English "manker", self-deprecating "noob kingdoms" tone); working title only, subtitle deferred to commercial release |
| 2026-07-02 | Guild-tier settlement bonuses on roadmap | ADR-0017 | Per-character skill progression only in v1; settlement-wide guild buildings that accumulate cumulative XP and grant passive bonuses deferred to Alpha→Beta |
| 2026-07-02 | Ranger replaces Rogue for v1 second class | ADR-0018 | Fighter (melee) + Ranger (ranged) instead of Fighter + Rogue; Ranger fits ranged combat more naturally than Rogue-with-bow |
| 2026-07-02 | Skill cap formula locked | ADR-0019 | skill ceiling = floor(99 × stat / 18), equivalently floor(stat × 5.5); multi-stat skills use higher-of; no legendary stats; no prestige; no grandmaster tier |
| 2026-07-02 | XP formula locked | ADR-0020 | XP-per-tick-while-working; idle characters gain nothing; ties into server tick model naturally |
| 2026-07-02 | Backlog and triage process | ADR-0021 | `IDEAS_BACKLOG.md` at repo root; new ideas captured and triaged into: trivial content / post-slice feature / slice-affecting scope change (requires new ADR) / rejected |
| 2026-07-02 | Deterministic lockstep considered and rejected | ADR-0022 | Factorio-style lockstep evaluated; rejected for MK due to real-time combat input lag, Godot 4 non-determinism defaults, and modding fragility; authoritative host retained; determinism kept as discipline where cheap |

---

## 10. Open questions

Things we know we haven't decided yet. Tracked here so they don't get lost.

1. **Final stat list.** v1 uses 4 (Str, Dex, Con, Wis). Should we expand to full AD&D 6 (add Int, Cha) at 1.0? Depends on whether Int drives magic (planned yes) and whether Cha drives village diplomacy (undecided).
2. **Class list at 1.0.** Vertical slice has 2. Target 5–7 at 1.0. Which subset of D&D classes? Prime candidates: Fighter, Ranger, Cleric, Ranger, Wizard, Druid, Paladin.
3. **Multiclass at UI level.** The system supports emergent multiclass. Do we show a "class fusion" title in UI (Fighter/Cleric = "Templar") or just show skill levels? Cosmetic decision, not urgent.
4. **Village diplomacy depth.** How rich is the interaction with a village before recruitment? Just talk-and-quest, or reputation systems, gifting, marriage-adjacent mechanics?
5. **World hostility slider granularity.** Should difficulty settings expose individual toggles (enemy expansion on/off, raid frequency, monster respawn rate) or preset difficulty tiers (Peaceful/Standard/Hard/Nightmare)? Both is possible.
6. **Save format.** Custom binary, JSON, or Godot's native `PackedScene` serialization? Trade-offs: mod-friendliness (JSON wins), performance (binary wins), robustness (native wins).
7. **Modding metadata format.** Custom manifest schema vs. an existing one (BepInEx-style). Decide before mod loader is built.
8. **Player character stat rolling.** Random like NPCs, point-buy, or hybrid? NPCs are random for variety; players may want more control.
9. **Dedicated server distribution.** Standalone binary in Steam tools, Docker image, or both? Decide before public dedicated server support ships.

---

*Change control: modifications to Section 4 (Locked Design Decisions) require an ADR entry in Section 9. All other sections may be edited freely.*
