# Mankers Kingdoms — Combat Resolution (GDD)

**Status:** v0.1 — locked design for attack resolution, damage, and NPC/monster defense. Crit/fumble explicitly phased (Phase A locked for v1, Phase B roadmap).

**Related:** `PRD.md` §4.3, `VERTICAL_SLICE.md` §3.4, `docs/gdd/skills.md` (Melee/Ranged skills, stat caps), `docs/gdd/inventory.md` (item schema — needs new fields, see §7), `ARCHITECTURE.md` §4.4 (server-authoritative hit confirmation), ADR-0013 (ranged combat in v1), ADR-0022 (determinism/seeded RNG)

---

## 1. What this document does NOT change

The real-time input layer is untouched. Players still aim, swing, and block exactly as already designed in `VERTICAL_SLICE.md` §3.4 — directional melee swing/block, mouse-aimed ranged attacks with arrow trajectory. This document only changes **how the server decides what happens once an attack attempt reaches its target** — replacing (or rather, layering under) a pure geometry-only hit check with a dice-driven resolution step.

This is consistent with — not a departure from — `ARCHITECTURE.md` §4.4: *"Client shows immediate swing animation on input, but damage is only applied when the server confirms the hit."* We are simply defining **how** the server confirms it.

---

## 2. Attack resolution — the hybrid model

### 2.1 Why "hybrid," not classic THAC0

Classic AD&D stores two separate persistent numbers per creature: an attack bonus (from class/level tables) and a stored Armor Class, resolved via THAC0 lookup tables (confusing, descending-is-better math, extra bookkeeping).

Our hybrid keeps the **d20 roll** (so combat still has AD&D's swinginess and crit potential) but **never stores a separate Armor Class stat for gear-bearing entities.** The defender's target number is calculated live, at the moment of the attack, from data we already track (Dexterity, equipped armor). No new persistent attribute, no lookup tables. (Non-gear-bearing beasts are handled differently — see §6.)

### 2.2 The formula

**Attacker's Attack Bonus** = `floor(RelevantSkillLevel ÷ 10) + StatModifier(GoverningStat)`

- `RelevantSkillLevel` is Melee or Ranged, per `docs/gdd/skills.md` §2.1 (already locked, already stat-capped per ADR-0019)
- Melee uses Strength; Ranged uses Dexterity — consistent with each skill's governing stat already locked in `docs/gdd/skills.md`.

**Defender's Target Number** (gear-bearing entities) = `10 (base) + StatModifier(Dexterity) + ArmorValue (equipped armor) + ShieldBonus (if wielding a shield)`

**Resolution:** roll 1d20 (via seeded `world.Random`, per ADR-0022) + Attack Bonus. If the total **meets or beats** the Target Number → hit. Otherwise → miss.

**Natural 20** always hits regardless of the math (and triggers a critical, see §5). **Natural 1** always misses regardless of the math (and may trigger a fumble, see §5.3's asymmetry rule).

### 2.3 Stat modifier — gentler curve (locked)

**Decision:** rather than the classic AD&D-style modifier table (roughly ±4 spread across the stat range), we use a **gentler curve**: `StatModifier = floor((Stat − 10) ÷ 4)`.

Rationale: our skill-cap formula (ADR-0019, `floor(99 × stat / 18)`) already does most of the stat-differentiation work — a low-Str character is permanently capped at a low Melee skill regardless of anything else. Layering a *second*, equally steep stat swing on top of that (as classic AD&D's ±4 modifier would) double-counts stat significance in a single roll. The gentler curve lets skill level (which the player actively trains) dominate the moment-to-moment roll, while stat still provides a real but secondary nudge.

| Stat | Modifier |
|---|---|
| 3–5 | −2 |
| 6–9 | −1 |
| 10–13 | 0 |
| 14–17 | +1 |
| 18 | +2 |

### 2.4 Worked examples (updated for the gentler curve)

**Fighter (Str 16 → mod +1, Melee skill 45) attacks a Goblin (Dex 12 → mod 0, hide armor +2):**
- Attack Bonus = floor(45÷10) + 1 = 4 + 1 = **5**
- Goblin's Target Number = 10 + 0 + 2 (hide) = **12**
- Roll 14 → 14+5=19 ≥ 12 → **Hit**
- Roll 3 → 3+5=8 < 12 → **Miss**

**Same Fighter attacks a tougher Orc (Dex 14 → mod +1, chainmail +5, shield +1):**
- Orc's Target Number = 10 + 1 + 5 (chainmail) + 1 (shield) = **17**
- Roll 14 → 19 ≥ 17 → **Hit, barely**
- Roll 9 → 14 < 17 → **Miss**

### 2.5 Composition with existing swing/block system

> **Superseded — see §15 (block redesign, 2026-07-17).** Active blocking no longer acts as a hard binary gate. See §15 for the current rule: mutual exclusivity (block XOR attack) plus a +4 TN bonus while blocking.

~~The existing directional facing/range/block check (`VERTICAL_SLICE.md` §3.4) remains a **prerequisite gate** before this roll ever happens:~~

~~1. Client sends swing/fire input.~~
~~2. Server checks geometry: is the target in range, in the swing arc / arrow path, and not actively blocking? If this fails, the attempt never reaches the dice roll — same as today.~~
~~3. If the geometry gate passes, the server runs the attack roll (§2.2) to determine hit/miss.~~
~~4. If hit, the server runs the damage roll (§3).~~

~~**Active blocking** is treated as a hard binary gate at step 2 (block = attack never reaches the dice roll) — not folded into the Target Number math. This keeps blocking feeling responsive and skill-based, while the dice layer governs the *unblocked* outcome.~~

---

## 3. Ranged range penalty — confirmed: none needed (locked)

**Decision:** no additional numeric to-hit penalty for range/distance. The existing arrow-trajectory and travel-time system (`VERTICAL_SLICE.md` §3.4 — arrows have travel time and trajectory, can miss based on target movement during flight) already makes long shots physically harder to land. Adding a second, numeric range penalty on top would be redundant with a mechanic that already does the same job through actual projectile physics rather than an abstract formula.

---

## 4. Damage

**Damage = WeaponDice + StatModifier(GoverningStat)** — same gentler-curve modifier and governing-stat convention as the attack roll (Melee → Strength, Ranged → Dexterity).

### 4.1 Example weapon dice (placeholder values, tuned during balancing)

| Weapon | Dice | Category (for Phase B) |
|---|---|---|
| Dagger / hunting knife | 1d4 | Piercing |
| Shortbow (arrow) | 1d6 | Piercing |
| Longsword | 1d8 | Slashing |
| Mace / club | 1d6 | Bludgeoning |
| Shield bash | 1d3 | Bludgeoning |

### 4.2 Worked examples (updated for the gentler curve)

**Fighter (Str 16 → mod +1) with longsword (1d8):** roll 6 → 6+1 = **7 damage**
**Ranger (Dex 15 → mod +1) with shortbow (1d6):** roll 4 → 4+1 = **5 damage**

---

## 5. Critical hits and fumbles — phased

### 5.1 Historical grounding

Core AD&D rulebooks **never shipped an official crit/fumble table** — by most accounts Gary Gygax considered a natural-20 guaranteed hit sufficient on its own. The famous "old tables" people remember are a fan supplement: **"Good Hits & Bad Misses" by Carl Parlagreco (Dragon Magazine #39)**, later reprinted in *Best of Dragon Magazine, Vol. V*. That system used **separate percentile (d100) tables per weapon damage type** (Slashing/Bludgeoning/Piercing, plus a distinct animal table), with **location-based effects** rather than simple bonus damage.

Building that full system is a genuine **content-authoring project**, phased the same way the inventory system's shape-based grid was (`docs/gdd/inventory.md` §7).

### 5.2 Two lessons carried forward from real community experience

1. **Fumble asymmetry rule.** A natural 1 is only a *true* fumble if the total roll (with all bonuses) would have missed anyway. If a skilled character's bonus carries a natural-1 roll past the target number, it's just a normal hit — no fumble.
2. **Symmetry.** Crit/fumble rules apply identically to players and NPCs, from day one.

### 5.3 Critical hit / fumble does NOT affect XP gain (locked)

Combat XP remains purely tick-based-while-engaged per ADR-0020, unaffected by whether a given attack was a hit, miss, crit, or fumble. Keeps the XP model simple and consistent with the rest of the skill system.

### 5.4 Phase A (locked for v1) — small flavor table

**Critical hit** (natural 20 that hits): damage dice rolled twice (doubled), **plus** one randomly-selected bonus effect:

| Effect | Notes |
|---|---|
| Devastating Blow | Double damage only, no extra effect (most common) |
| Precise Strike | Double damage + brief stun (interrupts target's current action) |
| Bleeding Wound | Double damage + short damage-over-time bleed |
| Sundering Hit | Double damage + brief reduction to target's ArmorValue |
| Staggering Blow | Double damage + brief knockback/stagger |

**Critical fumble** (natural 1 that would've missed anyway, per §5.2's asymmetry rule): miss, **plus** one randomly-selected complication — never self-damage:

| Effect | Notes |
|---|---|
| Off-Balance | Miss + brief reduced accuracy on your own next attack (most common) |
| Weapon Slip | Miss + brief disarm (quick recovery) |
| Overextended | Miss + brief vulnerability window (bonus damage if hit during it) |
| Stumble | Miss + brief movement-speed reduction |

**Weighting: placeholder equal-ish across entries for v1** (confirmed) — real weighting is a balancing pass, not a launch blocker.

### 5.5 Phase B (roadmap, not scheduled)

Post-slice: separate tables per weapon damage type, richer location-based effects inspired by the historical Dragon Magazine table's structure, plus a parallel animal/monster-specific table. Add to `PRD.md` §6 roadmap when this document graduates from draft.

---

## 6. NPC and monster defense — flat authored values for beasts (resolved, verified against real AD&D precedent)

**The historical convention, confirmed:** AD&D never computed monster Armor Class live from component stats — the original 1977 Monster Manual's own definition states AC represents *"the general type of protection worn... protection inherent to the creature due to its physical structure or magical nature, or the degree of difficulty of hitting a creature due to its speed, reflexes"* — all pre-baked into **one single designer-authored number** per monster type. Monster THAC0/attack bonus is likewise derived from Hit Dice and written directly into the stat block.

**The one directly-relevant exception, from 2nd Edition's Monstrous Manual:** *"A human or demihuman always uses a player-character THAC0, regardless of whether they are player characters or monsters."*

### 6.1 The rule we adopt

**The split is "does this creature have equippable gear," not "player vs. NPC":**

- **Humanoid / gear-bearing entities** — players, recruited villagers, Bandit, Goblin, Orc (all humanoid per `VERTICAL_SLICE.md` §3.6's bestiary) — use the **live formula** from §2.2: Dex modifier + whatever armor they happen to have equipped, exactly like a player.
- **True beasts without gear slots** — Wolf, and any future non-humanoid monster — get a **flat, designer-authored Attack Bonus and Target Number** written directly into their monster data definition. No Dex/armor computation, no "natural armor" mechanic to invent — just two tuned numbers a designer sets once, exactly matching the original Monster Manual's approach.

### 6.2 Example data entry (flat-authored beast)

```json
{
  "id": "monster.wolf",
  "attack_bonus": 3,
  "target_number": 12,
  "damage_dice": "1d6",
  "damage_type": "piercing"
}
```

### 6.3 Example data entry (gear-bearing humanoid — uses live formula, no flat numbers needed)

```json
{
  "id": "monster.goblin.scout",
  "stats": { "str": 10, "dex": 13, "con": 9 },
  "melee_skill": 20,
  "equipped_armor": "item.armor.leather",
  "equipped_weapon": "item.weapon.shortsword"
}
```

This resolves what was previously an open question — no new mechanic needed, just a data-authoring split already validated by 45+ years of the source material's own precedent.

---

## 7. Data model additions needed

New fields on **armor** items in `docs/gdd/inventory.md`'s item schema:

```json
{
  "id": "item.armor.chainmail",
  "armor_value": 5,
  "shield_bonus": 0
}
```

New fields on **weapon** items:

```json
{
  "id": "item.weapon.sword_iron",
  "damage_dice": "1d8",
  "damage_type": "slashing"
}
```

`damage_type` is unused by Phase A's flat crit/fumble tables but included now so Phase B's weapon-type-specific tables don't require a schema migration later.

New monster data fields (§6.2/6.3): `attack_bonus` and `target_number` for flat-authored beasts; nothing new needed for gear-bearing humanoids beyond what stats/skills/inventory already define.

---

## 8. Determinism and networking

- All rolls (attack, damage, crit/fumble table selection) go through `world.Random`, the existing seeded RNG (ADR-0022, ARCHITECTURE.md §7).
- Resolution is fully server-authoritative, consistent with ADR-0005 and ARCHITECTURE.md §4.4. Client shows the swing/fire animation immediately on input; the hit/miss/damage/crit result is only ever applied once the server resolves it, then communicated back as a floating combat text event (e.g. "MISS", "7 dmg", "CRITICAL — Bleeding Wound!").

---

## 9. Modding surface

- Weapon dice, damage type, armor values, shield bonuses: data-driven per ADR-0009, extending the existing item schema.
- Beast attack_bonus/target_number: data-driven per monster definition — modders can add new beasts by authoring two numbers, no formula to reverse-engineer.
- Phase A's crit/fumble flavor tables: data-driven, weighted-random entries in `data/base/combat/crit_table.json` and `fumble_table.json`.
- Phase B's location-based tables (when built): same data-driven pattern, split by weapon damage type.

---

## 10. Resolved — no remaining open questions from initial draft

All five items flagged in the initial draft are now resolved:
1. ~~Combat XP interaction~~ → §5.3: no effect, locked.
2. ~~Crit/fumble table weighting~~ → §5.4: placeholder equal-ish confirmed, real tuning deferred to balancing pass.
3. ~~NPC-specific Target Number defaults~~ → §6: resolved via verified AD&D precedent — flat authored values for gear-less beasts, live formula for gear-bearing humanoids.
4. ~~Ranged range penalty~~ → §3: confirmed unnecessary, existing trajectory system suffices.
5. ~~Stat modifier curve~~ → §2.3: gentler curve adopted, `floor((stat-10)/4)`.

Future open questions, if any arise, should be added here rather than treated as blocking v1 implementation.

---

## 11. Armor categories, movement, and stealth (addition)

**Status:** locked, added per Edu's direction. Confirms real D&D precedent: armor traits vary per-item, not strictly per broad category (verified against the SRD armor table — e.g. one light armor, roughly half of medium armors, and nearly all heavy armors carry a stealth penalty; movement penalty ties to a per-item Strength requirement, not armor weight class directly).

### 11.1 Three armor categories (drives §2.2's Target Number Dex modifier)

| Category | Dex modifier in Target Number |
|---|---|
| Light | Full Dex modifier applies (unchanged from §2.2) |
| Medium | Dex modifier capped at +1, regardless of actual modifier |
| Heavy | No Dex modifier applies (0, regardless of actual Dexterity) |

### 11.2 Movement penalty — per-item Strength requirement, not category

Each armor item carries a `str_requirement` field. If the wearer's Strength is below that value, apply a flat movement speed penalty (placeholder: −15%, tuned during balancing). Meeting the requirement means **no movement penalty from the armor itself**, regardless of how heavy the armor's category is. A high-Strength Fighter in full plate moves normally; a low-Strength character in the same armor is measurably slower.

Light armor items typically have `str_requirement: 0` (never triggers). Medium and Heavy items have progressively higher requirements, authored per item.

### 11.3 Stealth penalty — per-item flag, not category

Each armor item carries a `stealth_disadvantage` boolean. If true, it interacts with the Stealth skill scaffold (`docs/gdd/skills.md` §2.1 — currently "movement noise reduction only" functionality): a `stealth_disadvantage` armor either imposes a flat penalty to effective detection-avoidance or negates the Stealth skill's noise-reduction benefit outright while worn. Exact severity is a balancing decision, not architectural.

This is deliberately **not** derived from category — most Heavy armors will have `stealth_disadvantage: true`, but not strictly all; a rare Light armor could plausibly have it too (padding that rustles), and a well-designed Medium armor could avoid it. Authored per item, matching real precedent.

### 11.4 Encumbrance — separate, parallel system

Independent of armor category/per-item traits entirely. Ties to `docs/gdd/inventory.md`'s already-locked weight-cap system (`MaxCarryWeight`, Strength-derived).

**Soft threshold below the hard cap:**

| % of MaxCarryWeight carried | Movement effect |
|---|---|
| Below 50% | No penalty |
| 50–80% | Minor movement penalty (placeholder: −10%) |
| 80–100% | Moderate movement penalty (placeholder: −20%) |
| At cap (100%) | Cannot carry more — existing hard block from `docs/gdd/inventory.md` §2.2 unchanged |

**This stacks with §11.2's armor-based movement penalty** — a character can be simultaneously over-encumbered AND under-strength for their armor, suffering both penalties at once. They are tracked and applied independently, not merged into one formula, so future balancing can tune them separately.

### 11.5 Updated armor item schema

Extends `docs/gdd/inventory.md`'s item schema and this document's §7:

```json
{
  "id": "item.armor.chainmail",
  "armor_value": 5,
  "shield_bonus": 0,
  "armor_category": "heavy",
  "str_requirement": 13,
  "stealth_disadvantage": true
}
```

```json
{
  "id": "item.armor.leather",
  "armor_value": 1,
  "shield_bonus": 0,
  "armor_category": "light",
  "str_requirement": 0,
  "stealth_disadvantage": false
}
```

### 11.6 Open questions

1. Exact movement penalty percentages (§11.2's −15%, §11.4's −10%/−20%) — placeholders, real values are a balancing pass.
2. Exact stealth-disadvantage severity (flat detection-range penalty vs. full negation of Stealth skill benefit) — balancing pass, not architectural.
3. Whether encumbrance also affects stamina drain rate or attack speed, beyond movement — not yet specified, flagged for whenever the needs/stamina system (currently a v1 scaffold) is fleshed out further.

---

## 12. Simultaneous block-and-attack (addition)

> **Superseded — see §15 (block redesign, 2026-07-17).** The simultaneous-block-and-attack rule (Option B, −3 AB penalty) is replaced by full mutual exclusivity. Blocking and attacking are now strictly mutually exclusive — the −3 AB penalty is removed. See §15 for the current rule.

**Status:** ~~locked~~ superseded. Fixes an unaddressed gap found during testing — no locked rule previously existed governing whether blocking and attacking could occur simultaneously, so the current implementation allows both with no trade-off, which is inconsistent with the rest of this document's design.

### 12.1 The decision

**Option B chosen** (over a hard mutually-exclusive state-lock): blocking and attacking CAN occur simultaneously, but doing so applies a flat penalty to the **Attack Bonus only** (§2.2) while the block input is actively held. Damage is unaffected if the attack hits.

Rationale: every other part of this document layers AD&D-flavored dice resolution underneath an unchanged real-time input layer (§1). A hard state-lock (swing XOR block, action-game convention) would be simpler but breaks from that established pattern. A numeric hit-chance penalty keeps "real-time input, dice-driven resolution" consistent throughout — this is meant to feel like AD&D's "fighting defensively" trade-off, not a Souls-like animation lock.

### 12.2 The rule

While the block input is held **and** an attack is thrown in the same window:

**Attacker's Attack Bonus** (§2.2) receives a flat penalty (placeholder: **−3**, tuned during balancing) for that attack roll only.

No change to:
- Damage roll (§4) if the attack hits
- The defender's Target Number math (unaffected — this penalty applies only to the attacker choosing to fight while guarding, not to anyone being attacked)
- The existing geometry gate (§2.5) — blocking still functions normally as a defensive gate against incoming attacks; this section only concerns a character's own outgoing attack roll while their own block is active

### 12.3 Worked example

Fighter (Str 16 → mod +1, Melee skill 45) attacks while holding block:
- Normal Attack Bonus = floor(45÷10) + 1 = **5**
- While blocking: Attack Bonus = 5 − 3 = **2**
- Goblin's Target Number (from §2.4's example) = 12
- Roll 14 → 14+2=16 ≥ 12 → still hits, but a roll that would have hit easily unblocked (14+5=19) now only barely clears
- Roll 9 → 9+2=11 < 12 → misses (would have hit unblocked: 9+5=14 ≥ 12)

### 12.4 Open questions

1. Exact penalty value (−3 placeholder) — balancing pass, not architectural.
2. Whether this penalty stacks with the Medium/Heavy armor Dex-cap interactions (§11.1) or is fully independent — recommend independent (simple addition/subtraction, no multiplicative interaction) unless playtesting shows otherwise.
3. Whether NPCs ever use this behavior (an AI blocking while attacking) or whether it's a player-only input combination for v1 — not yet specified; likely low priority since NPC AI complexity isn't a v1 focus area.

---

## 13. Ranged resolution asymmetry (addition)

**Status:** locked. Addresses a real design flaw found during playtesting feedback: Ranged combat currently asks players to clear three independent failure gates (manual mouse aim, leading a moving target through arrow travel time + trajectory, THEN a separate d20 hit/miss roll) while Melee only faces one meaningful skill gate (facing/range/timing) before the same roll. Not matching intent — Ranged was being punished twice for the same thing.

### 13.1 The decision

For **Ranged attacks specifically**: if the arrow **physically connects** (aim + lead prediction succeeds, existing trajectory/travel-time system per `VERTICAL_SLICE.md` §3.4 confirms contact) — **this is an automatic hit.** No separate roll determines whether it lands.

The existing d20 attack roll (§2.2) still happens for a Ranged attack, but its result no longer determines hit/miss — it determines **damage/crit tier only** (§13.3).

**Melee is unaffected** — §2.2's full hit/miss roll stands unchanged for melee attacks. Melee's own gate (facing/range/timing) is comparatively light, so the dice layer is what gives melee its AD&D swinginess and stays as designed.

### 13.2 Rationale

Physical aim already **is** Ranged's skill-expression equivalent to Melee's dice roll — layering a second, invisible coin-flip on top of a harder physical skill check was the actual design flaw, not the concept of dice-driven combat itself. If a player nails the aim/lead, they should never be robbed of the hit by an unrelated roll; if they whiff the aim, that's the correct and sufficient failure point.

### 13.3 What the attack roll now determines for Ranged — binary, not tiered (locked default for v1)

Once physical contact is confirmed, roll 1d20 + Attack Bonus as before (§2.2's formula, completely unchanged), but **interpret the result differently for Ranged only:**

- **Natural 20, or total ≥ Target Number by a wide margin** (placeholder threshold, tuned during balancing): **Critical hit**, per §5.4's existing crit table.
- **Any other successful contact:** **Normal hit**, standard damage (§4).
- **§5.4's fumble table does NOT trigger for Ranged on a natural 1** — a natural 1 no longer determines whether the shot lands at all; a physical miss already covers "the shot failed." (See §13.5, open question 2, on whether a different fumble-equivalent belongs here later.)

**A three-tier "graze" outcome** (partial damage on a low roll that still physically connected) was considered and **deferred for v1** — keeping this binary (normal-hit / crit) for simplicity. Revisit only if playtesting shows Ranged damage needs more granularity.

### 13.4 What doesn't change

Target Number formula (§2.2), armor/shield effects (§2.2, §11), and the damage formula (§4) are all unchanged. Only the **interpretation** of the roll's outcome differs for Ranged (hit/miss removed from the roll's job, crit/normal-hit-tier only remains) — not the underlying math or any other formula in this document.

### 13.5 Open questions

1. Exact "wide margin" threshold for the strong-roll auto-crit case (§13.3) — placeholder, balancing pass, not architectural.
2. Whether a fumble-equivalent should exist for Ranged at all (e.g. a natural-1 roll on a landed shot causes a minor complication like a slow nock/reload delay on the next shot) — deferred, not blocking v1.
3. Whether NPC/monster ranged attackers (e.g. a future Bandit Archer per `VERTICAL_SLICE.md` §3.6) use this same asymmetric model, or the standard full-roll model from §2.2 — recommend the same treatment for consistency, but not yet confirmed.

---

## 15. Block redesign — mutual exclusivity and active TN bonus (supersedes §2.5 and §12)

**Status:** locked, 2026-07-17. Supersedes §2.5 (hard binary block gate) and §12 (simultaneous block-and-attack with −3 AB penalty).

### 15.1 The problem with the superseded rules

§2.5 made blocking **free invulnerability**: zero cost (no stamina, no time limit, no movement penalty), pure binary negation. §12 attempted to add a trade-off (−3 AB penalty while attacking-while-blocking) but was contradicted by §2.5's logic (if block = hard negation, when do you ever fight while blocking?). The two sections were inconsistent with each other.

### 15.2 The new rule

**Blocking and attacking are mutually exclusive.** A player holding block (RMB) cannot swing (LMB) or fire. Enforced at two layers:

1. **Client-side (UX):** `MeleeController._UnhandledInput` ignores LMB while `_isBlocking` is true; `BowController.TryFireBow()` returns early if `LocalState.IsBlocking` is true. These are UX guards only — they do not define correctness.

2. **Server-side (authoritative):** `CombatSystem.RequestMeleeAttack` returns early if `IsBlocking(sender)` is true; `ProjectileSystem.RequestFireProjectile` returns early if `CombatSystem.Instance.IsBlocking(sender)` is true. Per `ARCHITECTURE.md §4.4`, client-only validation is never sufficient — these server gates are the enforceable rule.

**Active blocking grants a +4 TN bonus while holding block with a shield equipped.**

- Applied in `CombatSystem.GetPlayerTargetNumber`: `if (IsBlocking(peerId) && shieldBonus > 0) tn += 4`
- Stacks with the passive `ShieldBonus` (+2) — total shield contribution while actively blocking: +6 to TN
- Re-verifies shield equipped at hit time (`shieldBonus > 0` from ArmorRegistry) to prevent stale-block state from granting the bonus after a shield is dropped

### 15.3 Effects by attack type — intentional asymmetry

Active blocking has a **real but distinct effect** against each attack type:

**vs. Melee:** The +4 TN bonus reduces the attacker's hit frequency. Orc (AB 5) example: 70% → 50% hit chance. The dice roll still happens — blocking is no longer a hard negation gate (§2.5 superseded) — but landing a blow becomes measurably harder.

**vs. Ranged:** Physical contact is automatic (§13.1) — blocking does **not** reduce how often arrows hit. Instead, actively blocking raises the ranged **crit threshold**: scoring a critical hit requires `roll == 20 AND rollTotal ≥ 24`, versus the unblocked condition of `roll == 20` only.

Threshold value 24 = 20 + 4, mirroring the +4 melee TN bonus — the same shield contribution, expressed on a different axis (crit severity vs. hit frequency).

Bandit Archer (AB 3) vs. blocking Fighter:
- Physical hit rate: **unchanged** (auto-hit on contact)
- Crit chance before blocking: **5%** (nat20 → rollTotal 23)
- Crit chance while actively blocking: **0%** (rollTotal 23 < threshold 24)

The Bandit Archer's shots still connect at the same rate; the raised shield makes finding a vital gap impossible. An attacker with AB ≥ 4 (rollTotal on nat20 = 24) would still score crits.

Passive `ShieldBonus` (+2) always applies while a shield is equipped — the +4 active block bonus (melee) and BLOCKING_CRIT_THRESHOLD (ranged) stack on top of this baseline and only activate while RMB is held.

### 15.4 Worked example (Fighter vs Orc, AB 5)

| Situation | Fighter TN | Orc needs to roll | Orc hit chance |
|---|---|---|---|
| Unblocked, shield equipped (passive +2) | 12 | 7+ | 70% |
| Actively blocking (+4 active bonus, +2 passive) | 16 | 11+ | 50% |
| Unblocked, shield + leather armor (+1 AV) | 13 | 8+ | 65% |
| Actively blocking, shield + leather | 17 | 12+ | 45% |

Delta: −20 percentage points from active block. Meaningful trade-off — gives up your attack turn for a real but non-trivial defensive improvement.

### 15.5 Removed from codebase

- `CombatSystem.RequestMeleeAttack` lines that hard-negated attacks against blocking defenders (§2.5 gate)
- `CombatSystem.RequestMeleeAttack` line: `if (IsBlocking(sender)) attackBonus -= 3` (§12 penalty)
- `ProjectileSystem._PhysicsProcess` shield re-check block gate (projectile hard-negation, also §2.5)

### 15.6 Open questions

1. Whether monster NPC AI should ever enter a blocking state (if so, they'd benefit from the same +4 TN bonus) — not yet specified; low priority since AI complexity is post-slice.
2. Whether the +4 value needs balancing once armor variety increases — a pure Heavy-armor tank (high ArmorValue, Dex mod 0) may find the blocking trade-off less attractive if their passive TN is already high.

---

## 16. Saving Throws (addition)

**Status:** locked. Introduces a classic AD&D mechanic — resisting poison, fear, and (once `docs/gdd/magic.md` is built) spell effects — adapted to fit the no-character-level design already locked (ADR-0019).

### 16.1 The formula

**Saving Throw Bonus** = `floor(RelevantSkillLevel ÷ 10)`, using whichever skill is most contextually relevant to the effect being resisted (e.g. Athletics for a physical poison/toxin, a future Willpower-adjacent skill for fear/charm effects once one exists). No stat modifier layered on top — this keeps the roll distinct from the Attack Bonus formula (§2.2) rather than being a reskin of it.

**Resolution:** roll `1d20 + Saving Throw Bonus ≥ a fixed difficulty` (difficulty is authored per effect/source, not a universal constant — a weak toxin might require 10+, a powerful curse 16+).

### 16.2 Racial Saving Throw bonuses (verified against real AD&D convention)

Layered as flat additions on top of the base roll above, specific to the effect category being resisted:

| Race | Bonus | Verified source |
|---|---|---|
| Elf | +4 vs. sleep/charm-type effects | AD&D 2e: elves have 90% resistance to sleep and charm-related spells |
| Dwarf | +2 vs. poison, +2 vs. magic | AD&D bedrock convention across editions |
| Halfling | +2 vs. magic, +2 vs. poison | AD&D bedrock convention across editions |
| Human | none (their versatility bonus already applies elsewhere per `character-creation.md`) | — |

### 16.3 Racial combat trait — Dwarf vs. Orc/Goblin (separate from Saving Throws, but same research pass)

**Dwarf receives a flat +2 Attack Bonus specifically against Goblin and Orc** (both already in the locked bestiary, `VERTICAL_SLICE.md` §3.6) — verified AD&D convention (dwarves have a long-standing combat bonus against these humanoid types specifically). Applied in `GetPlayerAttackBonus`-equivalent logic as a target-type-conditional modifier, not a universal bonus.

### 16.4 Open questions

1. Exact per-effect difficulty numbers — authored per poison/spell/effect as content is added, not locked here.
2. Whether Elf's stealth/surprise bonus (also verified in research, not wearing metal armor) is worth adopting alongside the Saving Throw bonus — flagged for later, not blocking this addition.
3. Dwarf underground/stonework detection and Elf secret-door detection — no mechanical home yet (no stonework/secret-door system exists). Deferred, not adopted now.

---

## 17. Class traits (addition)

**Status:** locked. Both traits extend systems already built this session rather than introducing new mechanics.

### 17.1 Fighter — enhanced active-block bonus

Fighter's active-block Target Number bonus (§15.2) is **+6** instead of the standard +4 — a mechanical "shield mastery" trait, felt directly in the block/parry system already tuned earlier this session. Ranger (or any other class, later) using a shield still gets the standard +4.

### 17.2 Ranger — favorable ranged crit threshold

Ranger's Ranged crit "wide margin" threshold (§13.3) is **lowered by 2** compared to the standard formula — i.e. where a non-Ranger needs `roll==20 && total>=24` for a critical hit, a Ranger needs `roll==20 && total>=22`. Mirrors the blocking-vs-ranged-crit fix from §13.3, applied as a class bonus rather than a defensive one.
