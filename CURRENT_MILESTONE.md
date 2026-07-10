# Current Milestone: M7 — Class-gated building

**Started:** 2026-07-10
**Target demo:** Fighter player can't build Herbalist's Hut (locked in build menu) → recruits Ranger-archetype villager → assigns them to any station → Herbalist's Hut unlocks → player builds it → assigns Ranger NPC to it → herbs appear in stockpile → craft a bandage → Ranger NPC removed from settlement → Herbalist's Hut goes dormant.

## Scope (from VERTICAL_SLICE.md §3.5 + §3.6)

- Herbalist's Hut — new building type, gated on Ranger-class NPC presence in settlement ⬜
- Presence-gating logic: building locked when no Ranger-archetype assigned, dormant (not destroyed) when Ranger leaves ⬜
- Foraging NPC job loop: Ranger NPC assigned to Herbalist's Hut produces herbs → settlement stockpile ⬜
- Bandage crafting at Herbalist's Hut: E key → craft from herbs → heals 20 HP (no Medicine skill in v1) ⬜
- Demo gate ⬜

## Key decisions (pending / to be locked during M7)

- **Dormant vs locked:** When the Ranger NPC leaves, does the hut go locked (can't interact) or dormant (crafting menu opens but shows "no Ranger present" and disables craft)? — to decide.
- **Presence check:** Is presence determined by NPC archetype tag (`archetype.forager`), class assignment, or a Ranger-class player being in the settlement? — to decide (v1 spec says "Ranger class present in settlement" — could be NPC or player Ranger).
- **Herb item:** New item `item.herb` — base gather item; consumed 2→1 bandage at Hut — to confirm.
- **Bandage heal amount:** Fixed 20 HP, no Medicine skill check in v1 — to confirm.

## Out of scope for M7

- Full crafting tree depth beyond bandages
- Herbalist's Hut tier 2 recipes
- NPC Ranger having a ranged attack (NPCs are non-combatants in v1)
- Morale / relationship tracking
- Presence-gated buildings beyond Herbalist's Hut
