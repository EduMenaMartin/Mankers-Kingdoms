# Mankers Kingdoms — Music & Audio GDD

**Status:** v0.1 — draft. **Not an ADR.** Not yet assigned to any milestone in `VERTICAL_SLICE.md`. This document exists so creative direction isn't lost, but nothing here is locked, and nothing here authorizes implementation work.

**Related:** `PITCH.md` (tone pillars), `PRD.md` §4.8 (world/content tone), `ARCHITECTURE.md` §3 (client/server split — audio is client-only), `IDEAS_BACKLOG.md`

**Last updated:** 2026-07-05

---

## 1. Purpose

Define a musical identity for Mankers Kingdoms that a human composes once, deliberately, and that AI tools then orchestrate and vary — rather than generating a different musical identity per track. This mirrors how the project already treats content: architecture and identity are decided by Edu, execution is delegated.

This doc does **not** decide when music work happens. See §7 (Scope status).

---

## 2. Tone alignment

Cross-checked against locked tone language already in the knowledge base:

- `PITCH.md`: *"mystical D&D-flavored fantasy world"* — not grimdark, not comedic.
- `PRD.md` §4.8: *"Mystical D&D-flavored tone. Not grimdark, not comedic. High-fantasy adventuring feel."*

The proposed emotional arc (Wonder → Discovery → Adventure → Hope → Determination, explicitly excluding despair/horror/sadness as *identity* elements) is consistent with this. Those excluded moods aren't banned outright — they're appropriate for specific contexts (a dungeon, a losing battle) but shouldn't define the game's core musical signature. That distinction is worth keeping explicit so it doesn't get flattened later into "no dark music anywhere."

**No conflict found. No ADR contradicted.**

---

## 3. Music Bible (core identity)

This is the reference every future composition or AI prompt should be checked against.

| Field | Value |
|---|---|
| Title | Kingdom Motif (working name) |
| Scale | D Dorian |
| Tempo | 82 BPM |
| Time signature | 4/4 |
| Motif length | 8–12 notes |
| Primary instruments | Great Highland bagpipes, hurdy-gurdy, lute, wooden flute, strings |
| Core moods | Hopeful, ancient, noble, warm, adventurous |
| Explicitly excluded | Electric guitar, synth pads, heavy percussion, choirs, pop harmonies |

**Composition method (human-first, AI-second):**
1. Compose the motif on piano/keyboard, not via AI generation.
2. Structure as call-and-response (e.g. a 4-note "call" + 4-note "response" = one 8-note motif).
3. Validate across multiple instrument timbres (piano, flute, bagpipes, whistle) — a motif that survives re-instrumentation is structurally strong.
4. Only then hand the motif to an AI arranger (e.g. Mureka) with an explicit instruction to *orchestrate*, not *compose* — ideally seeded from a MIDI export of the motif itself, not a text description of the mood.

This keeps authorship of the actual melody with Edu; AI's role is arrangement and variation, not invention.

---

## 4. Leitmotif grammar

Rather than one theme, a small set of short, recombinable motifs — the same approach used by Uematsu, Soule, and Williams. This also maps naturally onto systems that already exist in the project's content model (settlements, monster nests/dungeons, combat, villages), so it's cheap to hook into later without new architecture.

| Motif | Length | Proposed usage |
|---|---|---|
| Hero / Kingdom | 8 notes | Ubiquitous — the core identity, reharmonized per context |
| Adventure | 6 notes | Overworld exploration |
| Mystery | 5 notes | Caves, ruins, monster nests |
| Royal | 4 notes | Settlement / kingdom interiors |
| Enemy | 4 notes | Combat / danger proximity |

**Contextual variation examples (illustrative, not final):**

| Context | Treatment |
|---|---|
| Kingdom | Bagpipes, full and proud |
| Tavern / Shelter | Lute, slow tempo |
| Forest / overworld | Solo flute |
| Dungeon / monster nest | Low strings, half speed |
| Boss encounter | Minor-key variation, full orchestration |
| Victory | Final 4 notes only, large cadence |

---

## 5. State → music triggers (open sketch, not implemented)

This is the part the original draft didn't cover, and the part that actually needs an engineering decision before this can be built: **what game state change causes what music transition, and how is that reconciled with a real-time, no-pause, multi-client architecture?**

Since `ARCHITECTURE.md` §3 puts audio playback entirely in `/client/` (never `/server/`), each player's client can independently choose its own music based on that player's own location and state. This is *not* a networking problem — no sync is required, similarly to camera or particle effects. What's missing is the trigger table itself:

| Trigger | Candidate transition | Status |
|---|---|---|
| Enter settlement | → Royal motif variant | Undecided |
| Enter overworld | → Adventure motif | Undecided |
| Enter dungeon/nest | → Mystery motif | Undecided |
| Combat starts | → Enemy motif layer/crossfade | Undecided — needs a decision on layered stems vs. hard cut |
| Combat ends / boss defeated | → Victory cadence | Undecided |
| Day → night | → tempo/instrumentation shift? | Undecided — not confirmed as needed at all |

None of this should be treated as scoped work. It's listed so the *shape* of the eventual implementation is visible, the same way the mod loader's shape was described in `ARCHITECTURE.md` §10 well before it was built.

---

## 6. Technical implementation (undecided, flagged for later)

Two real forks exist, and this doc deliberately does not resolve them:

1. **Godot native audio** (`AudioStreamPlayer` + audio buses, manual crossfading in `/client/` code) vs. **middleware** (Wwise/FMOD via GDExtension) for adaptive layering.
2. **Stem-based layering** (combat = same track + percussion layer fades in) vs. **discrete track swap** (hard cut/crossfade between separate files).

This is comparable in shape to the GodotSteam C# bindings situation — a dependency/tooling decision that shouldn't be locked until closer to the milestone that needs it. Recommend treating it the same way: tracked here as an open question, revisited when audio actually enters a milestone's scope, and given an ADR at that point if it affects architecture (e.g. adding a GDExtension dependency would trigger the "any new external dependency" escalation rule in `CLAUDE.md`).

---

## 7. Scope status

**Music is not mentioned in `VERTICAL_SLICE.md` §3 (Scope — IN) or §4 (Scope — OUT).** Per `CLAUDE.md` operating instruction #1 ("PRD-first — verify a feature is in PRD/VERTICAL_SLICE scope before implementing"), this document is creative/reference material only, not an authorized task.

Recommended backlog entry for `IDEAS_BACKLOG.md`:

> **[post-slice] Music & audio system** — Kingdom Motif composed, leitmotif grammar and Music Bible drafted in `docs/gdd/music.md`. No implementation, no engine/middleware decision, no milestone assignment. Revisit after M9 vertical slice playtest, or earlier only if a specific milestone demo would clearly benefit from placeholder audio.

---

## 8. Open questions

1. Final motif notes — the 8-note call-and-response sketch (D-F-G-A / G-F-E-D) is a starting sketch, not confirmed as final. Needs Edu's actual composition pass on a keyboard.
2. Stat generation for stems vs. swap (§6) — undecided, no urgency.
3. Whether day/night cycle warrants its own musical treatment at all, or whether that's over-scoping a 20-minute in-game day cycle.
4. Whether NPC/settlement ambient sound design (non-musical — wind, birds, forge sounds) is covered by this doc or deserves a separate `audio.md`. Currently out of scope for this draft.
5. Licensing/rights model for composed music and any sample libraries used (Great Highland bagpipes, hurdy-gurdy samples etc.) — not addressed yet; worth resolving before any commercial release, lower priority for vertical slice.

---

## 9. Reference

- `PITCH.md`, `PRD.md` §4.8 — tone pillars this document must stay consistent with
- `ARCHITECTURE.md` §3 — client/server split; audio confirmed client-only
- `VERTICAL_SLICE.md` §3–4 — confirms music is currently unscoped
- `IDEAS_BACKLOG.md` — where this should be logged as `[post-slice]`
- `CLAUDE.md` — escalation rule for any new external dependency (relevant if middleware like Wwise/FMOD is later chosen)

*Note: `docs/decisions/` (ADRs) and any existing `docs/gdd/skills.md` were not available in the current knowledge base session to cross-check numbering/format conventions directly — this document mirrors the structure of `PRD.md`/`ARCHITECTURE.md` instead. Worth a quick pass to align formatting once those files are accessible again.*
