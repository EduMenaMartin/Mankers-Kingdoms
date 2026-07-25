# Mankers Kingdoms — Research & Tech Tree (GDD)

**Status:** v0.1 — **STUB.** Full design session scheduled at M13 kickoff, after docs/gdd/production.md's tier structure is locked (this document gates and sequences that structure, so it should be designed second). Bellwright-inspired direction confirmed by Edu; nothing else locked yet.

**Related:** `docs/gdd/production.md` (stub — what this document gates), `docs/gdd/magic.md` (stub — research-gated, references this system), ADR-0026

---

## 1. The direction (locked)

A Bellwright-style research/prerequisite system: certain buildings, recipes, or Advanced Refine tiers require research or a prerequisite build to unlock, rather than being available from the start. This is explicitly a **separate concern from production.md's tiers** — tiers define *what exists*, the tech tree defines *what unlocks it and in what order*.

## 2. Open, to be resolved at full design session

- Is research an abstract currency (spend "research points" earned somehow) or purely build-gated (build X to unlock Y, no separate currency)?
- Does research require a dedicated building/NPC role, or is it passive?
- How does this interact with class-gated presence buildings already locked in `docs/gdd/settlements.md`?
- How does magic (`docs/gdd/magic.md`) plug into this tree specifically — is magic its own branch, or threaded through the same tree as mundane production?
