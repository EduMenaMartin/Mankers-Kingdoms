# Current Milestone: M6 — Village and Recruitment

**Started:** 2026-07-09
**Target demo:** Player travels to a procedural village, recruits a high-Str villager, brings them home, assigns them to the Woodcutter's Post — NPC chops trees autonomously while the player does something else.

## Scope (from VERTICAL_SLICE.md §3.6 + §5)

- Procedural village: 6–10 villagers, rolled stats, hidden archetype tags, generated names ⬜
- Simple recruitment dialogue: talk → offer to join → NPC leaves village and follows player ⬜
- Recruited NPC follows player to settlement ⬜
- Recruited NPC can be assigned to a station (Woodcutter's Post, Cooking Fire) ⬜
- Station job loop: NPC executes the station task autonomously, levels appropriate skill ⬜
- NPC needs (hunger/rest) tick down; NPC seeks Shelter when rest is low ⬜
- Demo gate ⬜

## Key decisions (pending / to be locked during M6)

- **NPC movement:** straight-line follow (same as monster AI) for v1; no pathfinding.
- **Dialogue:** single-prompt interaction (E key near villager → "Join us?" → Y/N), no full dialogue tree.
- **Station assignment:** E key near a station while NPC is following → assigns NPC; NPC walks to station and starts job loop.
- **NPC combat:** out of scope for M6. Recruited NPCs are non-combatants in v1.

## Out of scope for M6

- Village population growth
- NPC morale / relationship tracking
- Trade with village
- NPC combat
- Named archetype abilities (archetype tag stored but dormant)
- Orc enemy type
- Pathfinding (straight-line movement only)
