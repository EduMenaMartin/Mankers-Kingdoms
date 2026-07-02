# Mankers Kingdoms — Skills System (GDD)

**Status:** v0.1 — living document. Framework locked; specific skill list expected to evolve.

**Related:** `PRD.md` §4.4, `VERTICAL_SLICE.md` §3.3

**References:**
- SkillSetRPG (skillsetrpg.com) — structural framework for skill grouping
- RuneScape / RuneScape: Dragonwilds — use-to-level progression model
- Aska — tool-tier unlock progression for gathering skills
- Elder Scrolls series — soft-class emergence from skill investment

---

## 1. Framework

Skills in Mankers Kingdoms are the concrete measure of what a character can *do*. They grow through use, are soft-capped by rolled stats, and emerge into a soft-class identity over play.

### 1.1 Five skill groups

Adopting SkillSetRPG's four-group tabletop structure plus a fifth game-specific group:

| Group | Governs | Governing stats |
|---|---|---|
| **Physique** | Physical body — strength, mobility, endurance, combat | Str, Dex, Con |
| **Intellect** | Mental faculties — ingenuity, knowledge, wisdom | Int, Wis |
| **Charisma** | Social presence — charm, persuasiveness, leadership | Cha |
| **Magic** | Access to supernatural power | Wis, Int (final choice TBD) |
| **Trades** | Gathering and production professions (Mankers Kingdoms-specific) | Varies by trade |

### 1.2 Skill levels

- Levels **1 through 99** (RuneScape-style granularity for feedback)
- Effective ceiling = **governing stat × threshold multiplier** (TBD; likely stat × 10 or stat × 8)
  - Example with stat × 10: character with Str 8 → Melee ceiling of 80
  - Example with stat × 8: character with Str 8 → Melee ceiling of 64
- No decay
- No hard class boundaries — any character can invest in any skill
- Some skills have prerequisites (e.g. Sorcery may require Weirding level 20 first)

### 1.3 Class interaction

Classes are picked at character creation and provide:
- **Starting kit** (2–3 items)
- **Initial skill bumps** (+3 or +5 to two to four class-appropriate skills)
- **Cosmetic differentiation**
- **No hard restrictions** — a Fighter can still learn Sorcery, they just start behind a Wizard

Emergent multiclass identity happens through use.

---

## 2. Skill list

### 2.1 Physique group

| Skill | Governs | Governing stat | v1? |
|---|---|---|---|
| Agility | Balance, tumbling, dodging in combat | Dex | Post-v1 |
| Athletics | Running, jumping, climbing, carry weight | Str, Con | **Yes** |
| Melee | Combat with melee weapons (sword, spear, club, mace, fist) | Str | **Yes** |
| Ranged | Combat with ranged weapons (bow, crossbow, throwing) | Dex | **Yes** |
| Stamina | Resistance to fatigue, poison, disease, exertion | Con | Post-v1 |
| Stealth | Sneaking, hiding, avoiding detection | Dex | Scaffold in v1 |

### 2.2 Intellect group

| Skill | Governs | Governing stat | v1? |
|---|---|---|---|
| Academics | Formal knowledge — arts, science, history (lore lookups, quest unlocks) | Int | Post-v1 |
| Craft/Mend | Repairing gear, general construction quality | Int, Wis | **Yes** |
| Languages | Reading ancient texts, understanding non-human languages | Int | Post-v1 |
| Medicine | Treating wounds, applying bandages, brewing potions | Wis | **Yes** (basic) |
| Perception | Spotting hidden things, detecting traps, minimap radius | Wis | Post-v1 (v1 uses fixed radius) |
| Survival | Foraging edibles, tracking, navigating wilds | Wis | Post-v1 (superseded by Trades in v1) |
| Trade | Bartering, price-checking, market awareness | Cha, Int | Post-v1 |
| Warfare | Battlefield tactics, siege engine use, unit deployment | Int | Post-v1 |

*Note: SkillSetRPG's "Pilot" (ships/vessels) is deferred until ships exist in the game.*

### 2.3 Charisma group

Full group deferred to post-v1. Requires a dialogue and social simulation system.

| Skill | Governs | Governing stat | v1? |
|---|---|---|---|
| Artistry | Music, drawing, performance — decorative/social value | Cha | Post-v1 |
| Command | Leading troops, coordinating groups | Cha | Post-v1 |
| Investigate | Gathering information through inquiry | Wis, Cha | Post-v1 |
| Persuade | Convincing others in dialogue | Cha | Post-v1 |
| Ride/Team | Riding mounts, driving animal-drawn vehicles | Dex, Cha | Post-v1 (needs mounts) |
| Willpower | Resisting mental effects, fear, temptation | Wis | Post-v1 |

### 2.4 Magic group

Entire group deferred per PRD §4.3. Introduced with the magic system in post-slice milestones.

| Skill | Governs | Governing stat | v1? |
|---|---|---|---|
| Alchemy | Changing physical matter's properties | Int | Post-slice |
| Sorcery | Direct manifestation of power (offensive magic) | Wis or Int | Post-slice |
| Weirding | Divination, remote viewing, hidden information | Wis | Post-slice |

### 2.5 Trades group (Mankers Kingdoms-specific, Aska-inspired)

Trades are gathering and production professions with granular progression. Each has associated stations and tool tier unlocks at level thresholds.

| Skill | Governs | Governing stat | Station | v1? |
|---|---|---|---|---|
| Woodcutting | Chopping trees for wood | Str | Woodcutter's Post | **Yes** |
| Stonecutting | Quarrying stone from rocks | Str | Stonecutter's Post | Post-v1 |
| Mining | Extracting ore from deposits | Str, Con | Mine | Post-v1 |
| Foraging | Gathering plants, mushrooms, herbs | Wis | Herbalist's Hut | **Yes** (subset) |
| Farming | Planting, tending, harvesting crops | Con, Wis | Farm plot | Post-v1 |
| Fishing | Catching fish from water | Dex, Wis | Fishing spot | Post-v1 |
| Hunting | Tracking and killing game animals | Dex, Wis | (open world) | Post-v1 |
| Cooking | Preparing meals from raw ingredients | Wis | Cooking Fire | **Yes** (basic) |
| Smithing | Forging metal tools and weapons | Str, Int | Forge | Post-v1 |

### 2.6 Tool tier progression (Aska-inspired)

Each Trades skill gates tool access. A woodcutter starts with a stone hand-axe and unlocks better axes as their skill grows.

Example — Woodcutting:

| Skill level | Tool unlocked | Effect |
|---|---|---|
| 1 | Stone hand-axe | Base yield, slow |
| 15 | Bronze axe | +25% speed, +10% yield |
| 30 | Iron axe | +50% speed, +20% yield, can fell hardwoods |
| 50 | Steel axe | +75% speed, +30% yield, chance for extra logs |
| 75 | Mithril axe | +100% speed, +50% yield, no stamina cost |
| 90 | Enchanted axe | Chops instantly, occasional rare drops |

Each Trades skill has a similar tier ladder. Specific numbers tuned in balancing pass.

---

## 3. Stat caps

### 3.1 Cap formula (locked)

**Skill ceiling = floor(99 × stat / 18)**, equivalently floor(stat × 5.5).

This maps the 3d6 stat range (3–18) directly onto the 1–99 skill range:

| Stat | Skill ceiling |
|---|---|
| 3 | 16 |
| 4 | 22 |
| 5 | 27 |
| 6 | 33 |
| 7 | 38 |
| 8 | 44 |
| 9 | 49 |
| 10 | 55 |
| 11 | 60 |
| 12 | 66 |
| 13 | 71 |
| 14 | 77 |
| 15 | 82 |
| 16 | 88 |
| 17 | 93 |
| 18 | 99 |

**Consequence:** low-stat characters are *permanently* limited in the corresponding skill. A Str 3 character caps at Melee 16 no matter how many years they train. This is the intended feature — it makes recruitment strategy meaningful and gives every NPC permanent identity.

### 3.2 Multi-stat skills (locked)

Skills governed by two stats (e.g. Athletics: Str + Con) use the **higher of the two** for the cap.

Rationale: rewards character strengths, prevents "you need to roll well twice" frustration, matches the AD&D primary-attribute spirit. A character with Str 14 and Con 8 has Athletics ceiling 77 (from Str).

### 3.3 Class initial bumps

Class starting skill bonuses can push a starter above what the raw stat would allow, but that character then trains slowly toward their actual ceiling. Example: a Wis 10 Cleric starts with Medicine 5 from class kit; they can raise it to 55 (Wis 10 × 5.5), so they have headroom.

Class bonuses never raise the ceiling — only the current level.

### 3.4 No legendary stats (locked)

Stats are hard-capped at 18. No mechanic exists to push stats above 18, ever. No magic items, no level-up bonuses, no grandmaster tier above 99. This keeps the ceiling meaningful and simplifies balance.

---

## 4. NPC skill investment

### 4.1 Hidden archetype bias

Each generated NPC has a hidden **archetype tag**:
- **Combatant** — invests in Physique combat skills
- **Artisan** — invests in Trades and Craft/Mend
- **Scholar** — invests in Intellect skills (post-v1 mostly)
- **Scout** — invests in Physique mobility + Perception + Ranged

### 4.2 Assigned station overrides archetype

An NPC assigned to a Woodcutter's Post prioritizes Woodcutting + Str skills *regardless* of archetype. Archetype provides the *fallback* / secondary investment when the NPC is unassigned or between jobs.

This means:
- A Combatant assigned as a woodcutter still becomes competent at it, but slower than an Artisan would
- Their stats still cap them — a Combatant with Wis 6 assigned as Herbalist will hit Foraging 60 ceiling and stop

### 4.3 Recruitment strategy

The player looks for high-stat NPCs in the professions they need. A Str 16 villager is a top-tier warrior-in-waiting. A Wis 15 villager will make a great Herbalist. This is the intended recruitment tension.

---

## 5. Modding surface

- All skills defined in `data/skills/*.tres` (or JSON) with stable string IDs like `"skill.melee"`, `"skill.woodcutting"`, `"skill.trades.smithing"`
- Skill groups defined in `data/skill_groups/*.tres`
- Tool tier tables defined per-skill in the same file
- Stat governance defined as data, not code
- Modders can:
  - Add new skills (unique ID required)
  - Add new tool tiers to existing skills
  - Rebalance stat governance
  - Override display names and descriptions via localization files
- Modders cannot (v1):
  - Change the group structure (locked to 5 groups)
  - Add new skill groups (planned for Tier 2 modding)

---

## 6. XP formula (locked)

**XP-per-tick-while-working.**

While an NPC or player is actively engaged in a skill-relevant action (chopping wood, swinging in combat, foraging plants, cooking), they accumulate XP at a per-tick rate. Idle characters do not gain XP even if "assigned" to a station.

Rationale: encourages active engagement, discourages AFK grinding, integrates cleanly with the tick-based server simulation (server already knows what each character is doing every tick), gives visible progress feedback ("Woodcutting +1" every N seconds).

Specific XP-per-tick rates and level curves are balancing values, not architectural, and will be tuned during v1 and beyond.

---

## 7. Guild systems (roadmap, not v1)

**v1:** Skill progression is purely per-character. Each NPC has their own Woodcutting level, individually tracked and permanent to that character.

**Roadmap (late Alpha / Beta):** Settlement-wide guild bonuses layered on top. A **Woodcutter's Guild** building, when constructed and staffed, accumulates cumulative settlement-wide Woodcutting XP and levels up as a building. At each guild tier, all Woodcutters in the settlement gain passive bonuses (+% output, +% XP gain, unlock rare drops, unlock higher tool tiers earlier). Similar guilds for each Trade skill.

This is a strong late-game progression system for long campaigns — it gives settlements a distinct "specialty" character, rewards long-term investment in a particular trade, and creates a reason to grow beyond a small band. Deferred because it requires the base per-character system to be stable and playtested first.

Added to PRD §6 roadmap.

---

## 8. Deferred design choices

The following remain undecided but are not blocking v1 implementation.

1. **XP rate curves per skill** — flat per-tick vs. logarithmic (harder to level as you approach ceiling). Balancing decision, tuned during v1.
2. **Whether stats can be improved during play** — currently locked to no (no legendary stats, no level-up bonuses). Could be revisited if playtest shows late-game feels stale.
3. **Skill decay for unassigned NPCs** — currently locked to no decay. Could add "rusty" mechanic (small skill temporary reduction after long idle) if late-game specialization becomes too easy.
4. **Off-class skill investment friction** — currently unrestricted. Could add "class affinity" modifiers (Fighter learns Sorcery at 50% XP rate, but no hard block) as a soft-class flavor layer.
