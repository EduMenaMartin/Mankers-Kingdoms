# Mankers Kingdoms

*Working title — subtitle to be added before commercial release.*

---

## Elevator pitch

**Mankers Kingdoms** is a cooperative top-down survival and settlement-building game in a mystical D&D-flavored fantasy world. Two to six players pick a class, land in a small bounded realm, and carve out their own petty kingdoms — either sharing a single settlement or founding rival capitals that trade, ally, or feud. Recruit NPCs from procedural villages, assign them to workstations that match their hidden talents, defend against monsters drawn from the D&D SRD bestiary, and shape a kingdom whose character comes from the people who live in it.

The game is what would happen if the 1993 D&D classic *Stronghold: Kingdom Simulator* were rebuilt today with the WASD-controlled avatar coop of *Valheim*, the station-based colony management of *Bellwright*, and the use-to-level skill progression of *RuneScape: Dragonwilds* — all sitting on top of AD&D-style character stats that give every NPC permanent identity and every settlement organic variety.

---

## Design pillars

1. **Coop-first, but every player is a monarch.** Multiplayer isn't a mode bolted on; the game assumes it. Every player can found their own settlement or contribute to another's, with founder ownership and presence-gated expansion.
2. **Class identity matters, but skills belong to the individual.** You pick a class at character creation for identity and starting kit. Your actual capabilities grow through use, soft-capped by rolled stats. Two Fighters who play differently become genuinely different Fighters.
3. **Settlements are earned, not scripted.** No pre-placed towns handed to the player. You claim ground, defend it, and grow it based on who you recruit and what class they hold.
4. **Real-time, no pause.** Coop presence is preserved. Combat, crises, and decisions unfold in shared time — no one waits for anyone.
5. **Characters have permanent identity.** AD&D-style stats rolled at creation act as soft caps on skill growth. A villager with low Strength will never be a top warrior, no matter how long they train. This drives recruitment strategy and makes each pawn a real character.
6. **Modding from day one.** The base game is a content pack. Data-driven items, monsters, buildings, and skills. The community will make this game larger than the developer ever could.

---

## What makes it different

| Reference | What we take | What we reject |
|---|---|---|
| **D&D Stronghold (1993)** | Class-shapes-kingdom loop, alignment-based victory, class-specialized buildings, real-time strategic layer | Single-player only, no direct avatar control |
| **Valheim** | WASD coop avatar, persistent hosted world, dedicated server support, exploration-as-progression | Norse setting, boss-gated biomes, minimal recruitment |
| **Aska** | Station-based village assignment, top-down survival feel, procedural map | Norse setting, no class differentiation |
| **Bellwright** | NPC recruitment loop, medieval settlement building, needs and job assignment | Historical medieval tone, no coop |
| **RuneScape: Dragonwilds** | Use-to-level skills, multiclass emergence, coop survival | MMO-adjacent scope |
| **RimWorld** | Emergent character stories from stat variety | Paused tactical mode, priority-list job system, top-down disembodied control |
| **Dwarf Fortress** | Depth of NPC identity | Everything else |

---

## Target audience

Players who bounced off Valheim's minimal recruitment, wished RimWorld had multiplayer that actually worked, loved 1993's Stronghold and want a modern version, or enjoyed Bellwright but wanted coop and more mystical depth. Genre-literate players who understand "D&D" as a design vocabulary rather than a licensed franchise.

---

## Platform and scope

- **Platform:** PC first (Windows, Linux via Godot's native support). Mac and Steam Deck considered post-1.0.
- **Players:** 2–6 concurrent, coop only (no PvP in v1).
- **Session model:** Persistent hosted worlds à la Valheim, with **dedicated server support as a first-class architectural concern** from day one.
- **Campaign length:** Designed for long-form play (40+ hours to reach late-game equivalent). Session-agnostic — players drop in and out.
- **Engine:** Godot 4 + C#, with GodotSteam for Steam networking.
- **Modding:** Tier 1 (data mods) supported from launch; scripting mods on roadmap.

---

## What we are not making

- Not a RimWorld competitor. We are not building a paused-tactical global-priority simulation.
- Not a Dwarf Fortress competitor. We are not simulating every rat.
- Not a Skyrim. Single-player is not the priority; coop is the default assumption.
- Not launching with 100+ monsters, 12 classes, or full magic. Scope discipline over content spread.

---

*Status: v0.1 — working title, working pitch, expected to evolve with playtest feedback.*
