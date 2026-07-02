# Architecture Decision Records

Each ADR is a short markdown file capturing one architectural or design decision, its context, and its consequences.

**Rules:**
- Never delete an ADR. If a decision is superseded, mark it "Superseded by ADR-XXXX" and write a new ADR.
- Never edit an ADR's substantive content after acceptance. Correct typos yes; change the decision no.
- One ADR per file. File name: `ADR-XXXX-short-slug.md`.
- Number them sequentially.

**Template** for new ADRs:

```markdown
# ADR-XXXX: Title

**Status:** Accepted | Superseded by ADR-YYYY | Deprecated
**Date:** YYYY-MM-DD
**Deciders:** Who made this call

## Context
What situation prompted this decision? What forces are at play?

## Decision
What did we choose?

## Consequences
What follows from this — positive, negative, and accepted trade-offs.

## Alternatives considered
What we rejected and why.

## References
Related ADRs, docs, external links.
```

## Index

| ADR | Status | Title |
|---|---|---|
| 0001 | Superseded by 0016 | Working title Petty Kingdoms |
| 0002 | Accepted | Dedicated server architecture from day one |
| 0003 | Accepted | Perspective and control — top-down + WASD avatar |
| 0004 | Accepted | Real-time no pause |
| 0005 | Accepted | Authoritative host multiplayer |
| 0006 | Accepted | No player magic in v1 |
| 0007 | Accepted | No baby simulation in v1 |
| 0008 | Accepted | Win condition player toggle |
| 0009 | Accepted | Modding Tier 1 from inception |
| 0010 | Accepted | Engine and language — Godot 4 + C# + GodotSteam |
| 0011 | Accepted | Skill framework — SkillSetRPG + Trades group |
| 0012 | Accepted | Localization architecture |
| 0013 | Accepted | Ranged combat in v1 |
| 0014 | Accepted | Random encounter recruitment on roadmap |
| 0015 | Accepted | Conquest mechanic and economic layer on roadmap |
| 0016 | Accepted | Working title change to Mankers Kingdoms |
| 0017 | Accepted | Guild-tier settlement bonuses on roadmap |
| 0018 | Accepted | Ranger replaces Rogue for v1 second class |
| 0019 | Accepted | Skill cap formula |
| 0020 | Accepted | XP-per-tick-while-working |
| 0021 | Accepted | Backlog and triage process |
| 0022 | Accepted | Deterministic lockstep considered and rejected |
