# Language files

Base game localization lives here. One JSON file per language.

`en.json` is the canonical file — every key must exist here.

Other languages fall back to English on missing keys, then to `[key.name]` displayed if English is also missing.

Mods can add languages by dropping their own lang files into `/mods/<modname>/data/lang/`.

See `ARCHITECTURE.md` §9 for the localization architecture.
