# Mankers Kingdoms — Production & Industry Tiers (GDD)

**Status:** v0.1 — **STUB.** Captures the locked framework and an initial sketch so the idea is never lost. Full design session scheduled at M12 kickoff, after M10 (world-gen quality) and M11 (water/thirst) land. Do not treat the table below as final — it's a starting shape to refine, not a locked spec.

**Related:** `docs/gdd/skills.md` (Trades group), `docs/gdd/equipment.md`, ADR-0026 (production-mode shift), `docs/gdd/tech_tree.md` (stub — gates what's unlockable, references these tiers)

---

## 1. The pattern (locked, Aska-inspired)

Every production trade follows the same three-tier shape:

**Gather → Basic Refine → Advanced Refine**

Applied uniformly across all trades rather than bespoke per-resource — this is the actual design commitment, separate from any specific numbers below.

## 2. Initial sketch (pre-design, subject to full revision)

| Trade | Gather | Basic Refine | Advanced Refine |
|---|---|---|---|
| Woodcutting | Raw logs | Planks (Workbench) | Reinforced timber, charcoal |
| Stonecutting | Raw stone | Cut stone blocks | Masonry (walls, foundations) |
| Mining | Raw ore | Smelted ingots (Smithy) | Alloyed/tempered metal |
| Foraging | Herbs, fibers | Bandages, dyes | — |
| Hunting | Raw hide, meat | **Tanned leather** (→ Waterskin, per M11) | Fine leather goods, armor |
| Water collection | Raw water (river/well, per M11) | Stored water (Barrel, per M11) | Purified/enhanced water — possible Magic tie-in |
| Cooking | Raw food | Cooked meals | Preserved/enhanced meals |
| Farming | (deferred to after M11, per production-mode sequencing) | | |

## 3. Open, to be resolved at full design session

- Exact recipes, resource costs, and station requirements per tier
- Which stations are new buildings vs. upgrades to existing ones (Workbench, Smithy, etc.)
- How this interacts with the guild-tier settlement bonuses already logged in ADR-0017
- Whether Advanced Refine tiers require the tech tree (docs/gdd/tech_tree.md) to unlock, or are pure recipe/resource gates
