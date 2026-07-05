# Mankers Kingdoms — Factions & Allegiance (GDD)

**Status:** v0.1 — locked for v1. Introduced to fix a real bug found during M4 testing (monsters attacking other monsters indiscriminately) with a proper system rather than a special-cased patch.

**Related:** `docs/gdd/combat.md` §2.5 (composition with existing geometry gate), `docs/gdd/settlements.md` (founder/guest, presence-gating), `VERTICAL_SLICE.md` §3.6 (bestiary hostile/non-hostile categorization), `PRD.md` §4.2 (no PvP in v1 — locked), §4.10 (enemy AI, world hostility slider roadmap), ADR-0015 (conquest), ADR-0024 (this feature)

---

## 1. The bug this fixes

During M4 testing, monsters were attacking other monsters indiscriminately — no concept existed of "which side" an NPC is on, so AI targeting had no way to distinguish a valid enemy from another monster that should be left alone. This document replaces the ad-hoc fix with a real system: **every NPC gets a faction**, and targeting is gated on faction relationship before anything else happens.

---

## 2. The model

### 2.1 Faction assignment — per instance, not per species

**Every spawned nest, village, or player settlement gets its own unique `faction_id`** at creation time — not one faction per monster *type*. Two Goblin nests in different parts of the map are **different factions**, even though they're the same species. This directly satisfies "nest" as the boundary, not "race": Goblins from the same nest are always allied (same faction, never targets itself); Goblins from a different nest are not automatically friendly just because they share a species.

### 2.2 Two-layer relationship model

**Layer 1 — type-level defaults.** Every faction has a `faction_type`: `monster_nest`, `village`, or `player_settlement`. Default relationships are defined **once, per type pairing**, not per individual faction:

| Faction Type A | Faction Type B | Default relationship |
|---|---|---|
| monster_nest | monster_nest (different instance) | **Hostile** |
| monster_nest | village | **Hostile** |
| monster_nest | player_settlement | **Hostile** |
| village | village (different instance) | Neutral |
| village | player_settlement | Neutral (recruitable, not attacked on sight) |
| player_settlement | player_settlement (different instance) | **Allied** (hard rule, see §4) |

This directly implements the confirmed rule — **broad default-Hostile between different factions** — but scoped by `faction_type` so it doesn't break what's already locked: villages stay approachable for recruitment (§3.7's core loop), and players never fight each other (§4's hard rule).

**Layer 2 — instance-level overrides.** A specific pair of factions can have an authored override (e.g., "this specific Bandit nest and this specific Goblin nest are Allied") stored as an explicit relationship entry that takes precedence over the type-level default. This is the exception mechanism confirmed — authored later, without touching the underlying system.

### 2.3 Which bestiary types get which faction_type

Uses categorization **already locked** in `VERTICAL_SLICE.md` §3.6 — no new tagging needed:

- **`monster_nest`**: Bandit, Wolf, Goblin, Orc (all already listed as hostile-behavior types)
- **`village`**: Villager (already listed as "idles, recruitable")
- **`player_settlement`**: any settlement founded via Kingdom Marker (`docs/gdd/settlements.md`)

---

## 3. Important scoping note — why "broad default-Hostile" doesn't mean "everything attacks everything"

The confirmed direction ("broad default-Hostile across different factions") is implemented at the **faction_type pairing level** (§2.2's table), not as a blanket rule applied to every individual entity regardless of context. This preserves two things already locked elsewhere:

- **Recruitment loop** (`VERTICAL_SLICE.md` §3.6/§3.7): villages must stay Neutral to players — a village default-Hostile would make the entire recruitment mechanic impossible (you can't talk to something trying to kill you).
- **No PvP in v1** (`PRD.md` §4.2): player-controlled settlements must never be Hostile to each other, regardless of any other rule — see §4.

If this scoping doesn't match what you had in mind, flag it — but implementing literal "every different faction = Hostile, no exceptions" would silently break both of the above locked decisions, so this document takes the position that the confirmed rule applies at the type level, with monster nests being the actual "broadly hostile to everything different" category.

---

## 4. Hard rule: player factions are never Hostile to each other

**Player-controlled settlement factions cannot be set to Hostile with each other, under any circumstance, in v1** — enforced in code, not just as a data default, since this directly preserves the locked no-PvP decision (`PRD.md` §4.2, ADR referenced there). Even if a modder or future system attempts to author an override making two player settlements Hostile, this should be rejected or ignored in v1's build. Revisit only if/when PvP is ever seriously considered post-1.0 (not currently on any roadmap).

---

## 5. Recruitment as a faction transfer — no new mechanic needed

When a villager is recruited (`VERTICAL_SLICE.md` §3.6's existing recruitment dialogue flow), their `faction_id` simply **changes** from their village's faction to the recruiting player's settlement faction, at the same moment the existing "NPC follows player, can be assigned to a station" state change already happens. This is not a new mechanic — it's one more field updated during an event that's already locked and already implemented. Clean, no extra systems required.

---

## 6. Combat targeting — composition with existing systems

Faction check is now the **first gate**, ahead of `docs/gdd/combat.md` §2.5's existing geometry check:

1. **AI target selection** (new): an NPC's AI only considers entities in a Hostile-faction relationship as valid attack targets in the first place. This is a behavioral filter — it prevents an NPC from ever attempting to engage a friendly or neutral entity, rather than allowing the attempt and blocking it after the fact.
2. **Geometry gate** (existing, `combat.md` §2.5): is the target in range, in the swing arc/arrow path, not blocked?
3. **Attack roll** (existing, `combat.md` §2.2): the dice resolution.

For **player-initiated attacks** specifically: whether a player should be *allowed* to attack a Neutral-faction villager (griefing potential vs. player agency) is an open question — see §8.1. This document does not resolve it, to avoid quietly deciding a real design question you haven't weighed in on.

---

## 7. Data model

```json
{
  "id": "faction.instance.goblin_nest_042",
  "faction_type": "monster_nest",
  "archetype": "monster.goblin",
  "spawned_tick": 8420
}
```

Relationship overrides (Layer 2, §2.2):

```json
{
  "faction_a": "faction.instance.bandit_camp_017",
  "faction_b": "faction.instance.goblin_nest_042",
  "relationship": "allied",
  "authored_reason": "shared territory, narrative-authored alliance"
}
```

Type-level defaults (Layer 1) live in a single small config, not per-instance:

```json
{
  "monster_nest_vs_monster_nest": "hostile",
  "monster_nest_vs_village": "hostile",
  "monster_nest_vs_player_settlement": "hostile",
  "village_vs_village": "neutral",
  "village_vs_player_settlement": "neutral",
  "player_settlement_vs_player_settlement": "allied"
}
```

---

## 8. Forward-compatible seams (no work needed now)

- **World hostility slider** (`PRD.md` §4.10 roadmap): becomes a global override on Layer 1's type-level table — e.g. a "Peaceful" setting forces `monster_nest_vs_player_settlement` to Neutral instead of Hostile. No new system needed when this roadmap item is eventually built.
- **Conquest** (ADR-0015): capturing a settlement transfers its `faction_id` ownership to the conquering player's faction — same mechanism as §5's recruitment transfer, just applied to a settlement instead of an NPC.

---

## 9. Modding surface

- Type-level default relationships (§7): data-driven, tunable without code changes.
- Instance-level overrides: modders/scenario designers can author specific alliances or rivalries.
- New `faction_type` values (beyond monster_nest/village/player_settlement) could be added by mods for new content categories — the two-layer model doesn't hard-code the three types beyond what v1 needs.

---

## 10. Open questions

1. **Can players attack Neutral-faction villagers?** (§6) Allowing it risks griefing the recruitment loop; disallowing it removes player agency and may feel restrictive. Recommend deciding after some playtesting rather than locking now — flag as a day-one design conversation once M6 (village/recruitment) is testable.
2. **Recruitment faction transfer timing** — instant on recruitment confirmation, or is there a brief transition state (useful if a "half-recruited" NPC could still be attacked by their old nest)? Not blocking v1 implementation; default to instant unless a reason to do otherwise emerges.
3. **Exact type-level defaults beyond monster-nest hostility** — is `village_vs_village` really always Neutral, or could two rival villages exist (interesting future content, not needed for v1's single-village slice)? Not urgent given `VERTICAL_SLICE.md` §3.7 locks one procedural village for v1.
