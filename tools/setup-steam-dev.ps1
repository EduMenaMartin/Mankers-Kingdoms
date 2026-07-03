<#
.SYNOPSIS
    Set up Steam runtime files in the Godot editor directory for development.

.DESCRIPTION
    GodotSteam requires steam_api64.dll and steam_appid.txt to be present in the
    same directory as the Godot editor executable for Steam to initialise at run time.
    This script locates those files in the project's addon directory and copies them.

    Run once per dev machine, and re-run if you update GodotSteam or change the Godot
    editor installation path.

.PARAMETER GodotPath
    Full path to the Godot editor executable (e.g. C:\Godot\Godot_v4.7_win64.exe).
    If omitted, the script searches PATH for a binary named "godot" or "godot4".

.EXAMPLE
    .\setup-steam-dev.ps1
    .\setup-steam-dev.ps1 -GodotPath "C:\Godot\Godot_v4.7_win64.exe"
#>
param(
    [string]$GodotPath = ""
)

$ErrorActionPreference = "Stop"

$ProjectRoot  = Split-Path $PSScriptRoot -Parent
$AddonBinDir  = Join-Path $ProjectRoot "project\addons\godotsteam"

# ── Locate Godot editor ──────────────────────────────────────────────────────

if ($GodotPath -eq "") {
    $found = Get-Command "godot4","godot" -ErrorAction SilentlyContinue |
             Select-Object -First 1
    if ($null -ne $found) { $GodotPath = $found.Source }
}

if ($GodotPath -eq "" -or -not (Test-Path $GodotPath)) {
    Write-Error @"
Godot editor executable not found.
Pass the path explicitly:
  .\setup-steam-dev.ps1 -GodotPath "C:\Godot\Godot_v4.7_win64.exe"
"@
    exit 1
}

$GodotDir = Split-Path $GodotPath -Parent
Write-Host "Godot editor dir : $GodotDir"

# ── Copy steam_api64.dll ─────────────────────────────────────────────────────

$dll = Get-ChildItem -Path $AddonBinDir -Recurse -Filter "steam_api64.dll" -ErrorAction SilentlyContinue |
       Select-Object -First 1

if ($null -eq $dll) {
    Write-Error @"
steam_api64.dll not found under $AddonBinDir.
Make sure the GodotSteam GDExtension addon has been imported by the Godot editor
(open the project once, let the editor import assets, then re-run this script).
"@
    exit 1
}

Copy-Item $dll.FullName -Destination $GodotDir -Force
Write-Host "Copied           : $($dll.Name) → $GodotDir"

# ── Write steam_appid.txt ────────────────────────────────────────────────────
# App ID 480 is Valve's "Spacewar" test app, available to all Steamworks developers.
# Replace with your real App ID before shipping.

$appIdPath = Join-Path $GodotDir "steam_appid.txt"
Set-Content -Path $appIdPath -Value "480" -Encoding ASCII -NoNewline
Write-Host "Wrote            : steam_appid.txt (appid=480 / Spacewar) → $GodotDir"

Write-Host ""
Write-Host "Steam dev setup complete."
Write-Host "Launch the Godot editor (with Steam already running) and Steam should init."
Write-Host "Replace appid 480 in steam_appid.txt with your real App ID before shipping."
