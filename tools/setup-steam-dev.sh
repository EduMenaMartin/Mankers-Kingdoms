#!/usr/bin/env bash
# Set up Steam runtime files in the Godot editor directory for development.
# See tools/setup-steam-dev.ps1 for a detailed description.
#
# Usage:
#   ./tools/setup-steam-dev.sh [/path/to/godot]
#
# If the Godot path is omitted, the script searches PATH for "godot4" or "godot".
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
ADDON_BIN_DIR="$PROJECT_ROOT/project/addons/godotsteam"

# ── Locate Godot editor ──────────────────────────────────────────────────────

GODOT_PATH="${1:-}"

if [[ -z "$GODOT_PATH" ]]; then
    GODOT_PATH="$(command -v godot4 2>/dev/null || command -v godot 2>/dev/null || true)"
fi

if [[ -z "$GODOT_PATH" || ! -f "$GODOT_PATH" ]]; then
    echo "Error: Godot editor executable not found." >&2
    echo "Usage: $0 /path/to/godot" >&2
    exit 1
fi

GODOT_DIR="$(dirname "$GODOT_PATH")"
echo "Godot editor dir : $GODOT_DIR"

# ── Copy Steam library ───────────────────────────────────────────────────────
# Linux uses libsteam_api.so; macOS uses libsteam_api.dylib.

STEAM_LIB=""
for pattern in "libsteam_api.so" "libsteam_api.dylib" "steam_api64.dll"; do
    STEAM_LIB="$(find "$ADDON_BIN_DIR" -name "$pattern" 2>/dev/null | head -1 || true)"
    [[ -n "$STEAM_LIB" ]] && break
done

if [[ -z "$STEAM_LIB" ]]; then
    echo "Error: Steam library not found under $ADDON_BIN_DIR." >&2
    echo "Open the project in the Godot editor once (to import addon assets), then re-run." >&2
    exit 1
fi

cp "$STEAM_LIB" "$GODOT_DIR/"
echo "Copied           : $(basename "$STEAM_LIB") → $GODOT_DIR"

# ── Write steam_appid.txt ────────────────────────────────────────────────────
# App ID 480 = Valve's "Spacewar" test app. Replace with real ID before shipping.

printf "480" > "$GODOT_DIR/steam_appid.txt"
echo "Wrote            : steam_appid.txt (appid=480 / Spacewar) → $GODOT_DIR"

echo ""
echo "Steam dev setup complete."
echo "Launch the Godot editor (with Steam already running) and Steam should init."
echo "Replace appid 480 in steam_appid.txt with your real App ID before shipping."
