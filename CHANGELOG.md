# Changelog

Notable changes to the project. Not a git log — a human-facing history of what shipped in each milestone or version.

Follows [Keep a Changelog](https://keepachangelog.com/) conventions loosely.

---

## [Unreleased]

Pre-M0. Documentation and design foundations only.

### Added
- PITCH.md — elevator pitch
- PRD.md — full Product Requirements Document
- VERTICAL_SLICE.md — M0–M9 milestone breakdown
- ARCHITECTURE.md — technical design bible
- docs/gdd/skills.md — skill system spec
- docs/decisions/ — 22 ADRs (ADR-0001 through ADR-0022)
- CLAUDE.md, HANDOVER.md, README.md, TODO.md, BUGS.md, IDEAS_BACKLOG.md, .gitignore

### Design decisions locked
- Working title: Mankers Kingdoms
- Engine: Godot 4 + C# + GodotSteam
- Multiplayer: authoritative host, dedicated server first-class
- Skill framework: SkillSetRPG 4-group structure + Trades group
- Skill cap formula: floor(99 × stat / 18)
- Modding: Tier 1 (data) from day one
- Vertical slice: 2 players, 2 classes (Fighter + Ranger), 6 skills, 1 procedural village, 5 monster types

