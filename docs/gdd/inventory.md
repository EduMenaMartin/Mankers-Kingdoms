# Mankers Kingdoms — Inventory System (GDD)

**Status:** v0.1 — design locked per Edu's decisions below. Flagged as a scope increase over VERTICAL_SLICE.md's original one-line description; see §7.

**Related:** `PRD.md` §4.6, `VERTICAL_SLICE.md` §3.5, §3.10, §3.12, `docs/gdd/settlements.md`

---

## 1. Core model — locked decisions

- **Capacity: hybrid.** Every container has both a grid footprint (slot count via width×height) AND a weight cap. An item must satisfy both to be picked up/placed.
- **Grid: shape-based (Tetris-like).** Items occupy a rectangular W×H footprint on a grid, not a single generic "slot." Rotation supported (swap W/H).
- **Storage: separate systems.** Personal inventory and settlement Storage Chest are distinct containers with their own UI, not a unified always-open backpack view.

---

## 2. Capacity model detail

### 2.1 Grid dimensions

- Personal inventory: a fixed grid, e.g. **8 columns × 5 rows** (40 cells total) for v1. Exact numbers are a balancing value, not architectural.
- Storage Chest: larger grid, e.g. **10 × 8**, since it's stationary and meant for bulk (balancing value).

### 2.2 Weight cap

- Every item has a `Weight` value (data-driven, per ADR-0009).
- Every character has a `MaxCarryWeight`, governed by **Strength** — nice natural tie-in to the existing AD&D stat system (docs/gdd/skills.md). Suggested formula: `MaxCarryWeight = BaseCarry + (Strength × CarryPerStrengthPoint)`. Exact constants are a balancing value.
- The Storage Chest has **no weight cap** (it's a stationary object, not carried) — grid capacity alone limits it.
- Adding an item to personal inventory must pass BOTH checks: does a valid grid position exist for its shape, AND does total weight stay under `MaxCarryWeight`. Fail either → rejected, item stays where it was (ground, chest, etc.).

### 2.3 Item shapes

- **v1 recommendation: rectangular footprints only** (1×1, 1×2, 2×1, 2×2, 1×3, etc.) — not irregular L/T-shaped Tetris pieces. True irregular pieces add real complexity (rotation math, collision detection across non-rectangular cells) for limited gameplay payoff at this stage. Revisit for later polish if desired.
- Example v1 items: Apple (1×1), Hunting Knife (1×2), Sword (1×3), Shield (2×2), Arrow Bundle (1×2, stacks), raw Wood (1×1, stacks up to N per stack).
- **Rotation:** player can rotate a held item 90° (e.g. `R` key while dragging) to fit it differently. Rotating swaps W and H.
- **Stacking:** stackable items (arrows, wood, food) occupy one shape-slot regardless of stack count up to a max stack size (data-driven per item), rather than needing a new grid position per unit.

---

## 3. Personal Inventory vs. Storage Chest — separate systems

### 3.1 Why separate rather than unified

A unified "always the same panel, just different containers" model (like a shared stash view) is common in Tarkov-likes, but for our coop settlement-builder, a **distinct Storage Chest UI** better fits the "walk up to a physical chest and open it" fantasy already established by station-based interaction (Workbench, Cooking Fire, etc. — see VERTICAL_SLICE.md §3.5).

### 3.2 UI shape

- **Personal Inventory panel:** always accessible (default keybind, e.g. `I` or `Tab`), shows only the player's own grid + equipped/hotbar slots.
- **Storage Chest panel:** opens only when interacting with a placed Storage Chest building. Shows a **dual-pane view**: Chest grid on one side, Player's personal grid on the other (side-by-side), so items can be dragged between them directly. Closing the interaction closes the dual-pane view; personal inventory panel remains independently accessible as normal.

### 3.3 Permission enforcement (ties to docs/gdd/settlements.md)

Per the settlement permissions GDD, **guests can deposit AND withdraw** from shared storage. This means the Storage Chest's dual-pane view is available to guests, not just the founder. Enforcement is server-authoritative exactly as described in settlements.md §1.4 — every item-move request into/out of a Storage Chest validates the acting player has at least Guest-level access to that settlement before applying.

Personal inventory needs no permission check beyond "is this your own player's inventory" — trivially always true for its owner, never accessible to others.

---

## 4. Interactions

### 4.1 Pickup (from world)

- Player interacts with a ground item → server validates grid space + weight → if valid, item added to inventory, removed from world. If invalid (no space or over weight), reject with player-facing feedback ("Inventory full" / "Too heavy").

### 4.2 Drag-and-drop (within or between containers)

- Player drags an item from one grid cell to another (same container) or across the dual-pane view (personal ↔ chest)
- Server validates: target cells empty (or swappable with an item that fits the source's old position), weight limits respected on the receiving container (if personal inventory is the destination)
- Client shows a placement preview (ghost outline) while dragging; actual placement only confirmed once server validates

### 4.3 Rotation

- While dragging, player can rotate the held item; client shows updated ghost outline; server validates the rotated shape fits before confirming placement

### 4.4 Drop on death

Per VERTICAL_SLICE.md §3.4's death penalty ("drop all carried inventory at death site, recoverable"):

- On death, the player's entire personal inventory grid contents are transferred into a **temporary lootable container** ("body bag" / loot pile) spawned at the death location
- This loot pile uses the same underlying container/grid logic as Storage Chest (same code path, different spawn context and lifetime) — no separate system needed
- Loot pile despawns after a configurable duration (balancing value, e.g. 10 minutes) if not retrieved, or persists until server restart — exact behavior TBD, not blocking for initial implementation

### 4.5 Dropping an item to the ground (voluntary)

- Player can manually drop an item from their inventory onto the ground (outside any container) — spawns a physical pickup-able item entity in the world, removes it from the grid

---

## 5. Networking and server authority

Per ADR-0005 (authoritative host):

- **Personal inventory state** only needs to replicate to its owning client (nobody else needs to see inside another player's backpack) — a targeted RPC/sync, not a broadcast.
- **Storage Chest state** replicates to any client with the chest's dual-pane view currently open — a small multicast group, not global broadcast, and not persistent once no one has it open (poll/subscribe pattern, or simple "send full state on open, send deltas while any client has it open").
- **All grid placement, weight validation, and permission checks happen server-side.** Client only renders predicted/optimistic placement and receives authoritative correction if the server rejects a move (same prediction/correction pattern as movement, per ARCHITECTURE.md §4.4).

---

## 6. Data model

Per ADR-0009 (Tier 1 modding, data-driven content), every item is defined in `data/base/items/*.json` (or `.tres`) with stable string IDs:

```json
{
  "id": "item.weapon.sword_iron",
  "display_name_key": "item.weapon.sword_iron.name",
  "shape": { "width": 1, "height": 3 },
  "weight": 4.5,
  "stackable": false,
  "max_stack": 1,
  "category": "weapon",
  "icon": "res://assets/items/sword_iron.png"
}
```

```json
{
  "id": "item.resource.wood_log",
  "display_name_key": "item.resource.wood_log.name",
  "shape": { "width": 1, "height": 1 },
  "weight": 1.0,
  "stackable": true,
  "max_stack": 50,
  "category": "resource",
  "icon": "res://assets/items/wood_log.png"
}
```

Container definitions (personal inventory default size, Storage Chest size) are similarly data-driven constants, adjustable without code changes.

---

## 7. Scope note — flagged for the record

VERTICAL_SLICE.md §3.10 and §3.5 currently describe the inventory only as "grid, drag-drop, functional not pretty." A true shape-based (Tetris-like) grid with rotation and dual-pane chest transfer is a larger build than that phrasing implies — closer to Diablo/Escape from Tarkov-style inventory UI, which is real engineering and UI/UX work (placement validation, rotation math, drag-preview rendering, dual-pane sync).

**This doesn't block the decision** — it's Edu's explicit choice and a good fit for the game's identity — but it likely adds time to M3 beyond what was originally estimated. Logged to `TODO.md` as a flag; consider whether M3's time estimate in `VERTICAL_SLICE.md` needs a note or adjustment.

---

## 8. Modding surface

- All items, their shapes, weights, and stack sizes are data files — modders can add new items trivially
- Container sizes (personal inventory grid dimensions, Storage Chest grid dimensions) are data-driven constants — modders (or Edu, via balancing) can adjust without code changes
- Rotation/shape-matching logic itself is code, not exposed to Tier 1 (data) modding — consistent with ADR-0009's scope

---

## 9. Open questions

1. **Exact grid dimensions** (personal 8×5, chest 10×8 above are placeholders) — final numbers are a balancing pass, not urgent now.
2. **Carry weight formula constants** (`BaseCarry`, `CarryPerStrengthPoint`) — balancing pass.
3. **Loot pile despawn timing** on death — needs a decision before M3 implementation, not architecturally blocking.
4. **Equipped/hotbar slots** — separate from the grid (e.g. a weapon "in hand" and a quickbar of 4-6 consumables) — not yet specified. Recommend a small follow-up design pass once the core grid system works, since equipped-item logic (what's "in hand" for combat) already needs to exist for M4's combat system regardless of inventory grid style.
5. **Auto-sort/auto-arrange button** — quality-of-life feature, not required for MVP but very commonly expected in Tetris-grid inventories. Consider for M3 or immediately post-M3 polish.

---

## 10. Equipment slots (addition)

**Status:** locked. Closes a gap that existed since `docs/gdd/combat.md` was written — §2.2's Target Number formula and §4's damage formula have always assumed "equipped armor/weapon" as a concept, but no system ever formally defined how an item becomes equipped vs. simply carried. This section is that system.

### 10.1 Three equipment slots — not full Minecraft 4-piece armor

- **Main Hand** — the active weapon (Longsword, Shortbow, Dagger, etc., per `docs/gdd/equipment.md`'s catalog)
- **Off-Hand** — a Shield, a second one-handed weapon (dual-wield), or locked/forced-empty while a **two-handed** weapon (per `equipment.md` §4's `two-handed` property) occupies Main Hand
- **Body Armor** — a single slot for one full armor item (Leather, Chainmail, Plate, etc.)

**Deliberately NOT split into Head/Chest/Legs/Feet** like literal Minecraft — `equipment.md`'s entire armor catalog already models armor as one item per suit (one `armor_value` per entry, not four). Fragmenting into 4 slots would require reworking that whole catalog into per-body-part pieces; a single Body Armor slot matches what's already built. Verified against the actual D&D SRD 5.1 armor table (Leather/Chain Mail/Plate/etc. are each one complete worn item in the source material, never combined or split by body part) — this single-slot design is D&D-faithful, not a simplification away from it.

### 10.2 How equipped items feed combat.md — the gap this closes

- `docs/gdd/combat.md` §2.2's Target Number formula (`10 + StatModifier(Dex) + ArmorValue + ShieldBonus`) now reads `ArmorValue` from whatever occupies **Body Armor**, and `ShieldBonus` from **Off-Hand** specifically if it holds a Shield (not a second weapon).
- `combat.md` §4's damage formula reads `damage_dice`/`damage_type` from whatever occupies **Main Hand**.
- `combat.md` §11 (armor category, movement/stealth penalties) reads `armor_category`, `str_requirement`, `stealth_disadvantage` from the **Body Armor** slot's occupant.

None of these formulas change — this section only specifies *where* they now read their inputs from, closing what was previously an implicit assumption.

### 10.3 Equip/unequip behavior

- Dragging an item from the general inventory grid (Phase A, §3.2) into an equip slot **swaps**: whatever was previously equipped there returns to the general inventory grid (subject to the existing weight-cap and grid-space checks, §2), and the dragged item becomes equipped.
- If the general inventory has no room/weight budget for the displaced item, the swap is **rejected** — the equip action fails cleanly rather than silently dropping an item.
- Equipping a **two-handed** weapon into Main Hand automatically clears and locks Off-Hand until the two-handed weapon is unequipped.
- Equip/unequip is instant — no crafting-delay or animation lock — consistent with the real-time, no-pause design (ADR-0004).

### 10.4 Server authority

Per ADR-0005/`ARCHITECTURE.md` §4.4's existing pattern: equip requests are client-initiated but **server-validated** — server checks the item exists in the requesting player's inventory, checks slot-type compatibility (a weapon can't go in Body Armor), checks the two-handed exclusivity rule, then updates both the equipped-slot state and the general inventory grid. Client shows the swap optimistically; server confirms or corrects, same prediction pattern used everywhere else in combat/movement.

### 10.5 UI placement

Equipment slots appear on the **Character Sheet** screen (already built, M5's `CharacterSheet.cs`) alongside the existing stats/skills/tool-tier display — a small paper-doll-style set of three slot icons, not a separate screen. The general inventory grid (Phase A's `InventoryPanel.cs`) stays as-is for carried-but-not-equipped items; dragging between the two panels is the equip/unequip interaction.

### 10.6 Data model

No new item fields needed — `equipment.md`'s existing schema (`damage_dice`, `damage_type`, `armor_value`, `armor_category`, `str_requirement`, `stealth_disadvantage`, `shield_bonus`) already has everything §10.2 needs. This section only adds:

```json
{
  "equipped_main_hand": "item.weapon.longsword",
  "equipped_off_hand": "item.armor.shield",
  "equipped_body_armor": "item.armor.chainmail"
}
```
...as three new fields on the character/player data (null when empty), not a change to any item definition.

### 10.7 Open questions

1. Whether NPCs (per `docs/gdd/combat.md` §6's gear-bearing humanoids) use this exact same three-slot model — recommend yes, for consistency, but not yet explicitly confirmed.
2. Whether unequipping a two-handed weapon with a full inventory (no room for it to "go" anywhere) should be blocked entirely or handled some other way — edge case, low priority for v1.
3. **Forward-compatible note for future magic items (post-slice, no action needed now):** core D&D armor is genuinely a single-slot-per-suit system, confirming §10.1's single Body Armor slot is the D&D-faithful choice, not a simplification away from it. Separately, D&D (especially 3rd/3.5e) has a distinct convention of **magic item accessory slots** — Head, Eyes, Neck, Shoulders, Waist, Wrists, Hands, Rings (×2), Feet — for non-armor magical bonuses (Cloak of Resistance, Boots of Speed, Ring of Protection, etc.), independent of the armor's own AC value. Since ADR-0006 defers all magic to post-slice, this isn't relevant now, but it's a clean, well-precedented extension point: when magic items eventually get designed, adding named accessory slots (Ring, Cloak, Boots) is a natural additive expansion to this three-slot system, not a rework of it.
