# Mankers Kingdoms

*A cooperative top-down survival and settlement-building game in a mystical D&D-flavored fantasy world.*

**Status:** Pre-alpha, vertical slice in development. Not playable yet.

**Working title:** Mankers Kingdoms (from Spanish *manco* → "noob", evolved to *manker* for English readers). Subtitle to be added before commercial release.

---

## Documentation

Read these in order to onboard:

1. **[PITCH.md](./PITCH.md)** — the elevator pitch (5 min read)
2. **[PRD.md](./PRD.md)** — the full Product Requirements Document, the source of truth (30 min read)
3. **[VERTICAL_SLICE.md](./VERTICAL_SLICE.md)** — what we're building first, milestone by milestone
4. **[ARCHITECTURE.md](./ARCHITECTURE.md)** — technical design
5. **[docs/gdd/](./docs/gdd/)** — Game Design Documents per system (skills, combat, etc.)
6. **[docs/decisions/](./docs/decisions/)** — Architecture Decision Records (ADRs)
7. **[CLAUDE.md](./CLAUDE.md)** — context and conventions for Claude Code sessions

---

## Building (once the Godot project exists)

Prerequisites: Godot 4 (latest stable) with .NET / C# support, .NET SDK 8+, Rider or VS Code.

```bash
# Clone
git clone <repo-url> mankers-kingdoms
cd mankers-kingdoms/project

# Build
dotnet build

# Run editor
godot project.godot

# Run tests
dotnet test

# Run dedicated server (headless)
godot --headless -- --mode=dedicated --world=<save_id> --port=<port>
```

---

## Contributing

Solo project during vertical slice phase. Contributors welcome after M9 playtest, if the slice proves the concept.

For now: issues and ideas go into [`IDEAS_BACKLOG.md`](./IDEAS_BACKLOG.md).

---

## License

TBD. Base game code will be private until commercial release. Modding SDK and content licensing to be announced.

D&D SRD 5.1 content used under CC-BY-4.0.
SkillSetRPG design references used with credit.
