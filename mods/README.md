# Mods folder

User-installed mods go here, one folder per mod.

Each mod folder contains:
- `manifest.json` — metadata (id, version, dependencies)
- `data/` — content files (items, monsters, buildings, recipes, lang)

The base game at `/data/base/` is loaded first; user mods here load after.

See `ARCHITECTURE.md` §10 for the mod loader spec.

