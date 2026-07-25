# Mankers Kingdoms — Visual Character Customization (GDD)

**Status:** v0.1 — **STUB.** Direction confirmed by Edu; full design session TBD (not tied to a specific milestone yet — likely alongside or shortly after art direction work). Captures the confirmed approach so it isn't lost.

**Related:** `docs/gdd/character-creation.md` §13, `docs/gdd/skills.md`, art direction (not yet drafted)

---

## 1. Confirmed direction

**Discrete preset system, not continuous sliders.** True WoW-style morphable sliders (face shape, body proportions) require custom rigged models with blend shapes — not realistic with premade asset packs (KayKit) as a solo dev. Instead:

- **2–3 predefined options maximum** per customizable trait at launch (e.g. 2–3 hairstyles, 2–3 skin tones, 2–3 face variants), selected from a list — not dragged on a slider.
- **Height and weight applied as a scale multiplier** on the base model — cheap, already partially enabled by the existing `Knight.tscn` / `ModelContainer` instancing pattern from earlier model-swapping work.
- Age/Height/Weight rolled per race (`character-creation.md` §13) feed this system directly but have zero mechanical effect — purely cosmetic.

## 2. Why this scope, not more

Matches the same "correct architecture, simple content, expand later" pattern used elsewhere this session (inventory Phase A/B, production tier stubs). More presets, and eventually real sliders if custom models are ever commissioned, layer on later without rearchitecting — the underlying system (pick-a-preset + scale-the-model) doesn't need to change to support more options.

## 3. Open, to be resolved at full design session

- Exact trait list (hairstyle, skin tone, face variant — confirmed categories; others TBD)
- Whether presets are shared across all 4 races or authored per-race (a Dwarf's 2-3 hairstyles likely shouldn't be identical to an Elf's)
- Height/weight scale range and limits (how extreme can the multiplier go before it looks broken on the base rig)
- UI layout — likely integrated into the same screen as `CharacterCreateScreen` per the original ask, not a separate screen
- Whether this ties into the still-unbuilt `docs/gdd/production.md`'s Leatherworking tier (armor/clothing visual variation) at all, or stays purely cosmetic/independent
