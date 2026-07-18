using System;
using System.Collections.Generic;

namespace MankersKingdoms.Shared;

/// <summary>
/// Save file utilities accessible from both client and server.
///
/// SaveDir is a plain string constant — no Godot dependency.
///
/// ListSaves / PeekSession are Godot-dependent (DirAccess / FileAccess) and therefore
/// live in SaveSystem (server/). SaveSystem._Ready() wires the two provider delegates
/// so client code (LoadGamePanel) can call through this class without importing server/.
///
/// Null-guard defaults match the original method behavior:
///   ListSaves()        → empty list if provider not yet wired
///   PeekSession(name)  → null if provider not yet wired
/// </summary>
public static class SaveUtil
{
    /// <summary>
    /// Virtual path prefix for all save files.
    /// Plain string constant — no Godot import required.
    /// </summary>
    public const string SaveDir = "user://saves";

    /// <summary>
    /// Wired by SaveSystem._Ready() before any client UI runs (GameWorld scene order).
    /// Returns save slot names (no .json extension), newest-first.
    /// </summary>
    public static Func<List<string>>? ListSavesProvider { get; set; }

    /// <summary>
    /// Wired by SaveSystem._Ready() before any client UI runs.
    /// Returns the SessionSave block from a named save, or null if missing/corrupt.
    /// </summary>
    public static Func<string, SessionSave?>? PeekSessionProvider { get; set; }

    /// <summary>Returns save slot names newest-first, or an empty list if the provider is not wired.</summary>
    public static List<string> ListSaves() => ListSavesProvider?.Invoke() ?? new();

    /// <summary>Returns the SessionSave for the named slot, or null if missing, corrupt, or provider not wired.</summary>
    public static SessionSave? PeekSession(string saveName) => PeekSessionProvider?.Invoke(saveName);
}
