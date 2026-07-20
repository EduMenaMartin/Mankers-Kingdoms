using System.Text.Json;
using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Server;

/// <summary>
/// JSON save/load orchestrator (M8).
///
/// Save triggers:
///   - Autosave every AUTOSAVE_INTERVAL seconds (5 minutes).
///   - Host exit (_ExitTree).
///
/// Load trigger:
///   - Once at startup, deferred one frame so every system's OnPlayerConnected
///     handler fires first (initializing default per-peer state before overwrite).
///
/// Client reconnect (after initial load):
///   - OnLatePlayerConnected fires for peers joining a live server.
///   - Calls SendFullStateToClient to replay the current in-memory state via RPCs.
///
/// Node must appear in GameWorld.tscn AFTER all other server systems
/// (SettlementSystem, VillageSystem, SkillSystem, HealthSystem, NeedsSystem, InventorySystem).
/// </summary>
public partial class SaveSystem : Node
{
    public static SaveSystem Instance { get; private set; } = null!;

    private const float AUTOSAVE_INTERVAL = 300f; // 5 minutes

    /// <summary>Full path for the current save slot, e.g. user://saves/save_20260101_120000.json</summary>
    private static string SavePath => $"{SaveUtil.SaveDir}/{GameSession.SaveName}.json";

    private float _autosaveTimer;
    private bool  _loaded; // set true once TryLoad() completes (or is skipped)

    /// <summary>
    /// True if TryLoad() actually restored a save this GameWorld session (not just skipped
    /// because no file was found). HealthSystem.RequestSetClass reads this to skip re-giving
    /// the starting kit when the inventory was already restored from save.
    /// Automatically reset to false in _Ready() so each new session starts clean.
    /// </summary>
    public static bool SaveWasLoaded { get; private set; }

    private const string PLAYERS_PATH = "/root/GameWorld/Players";

    public override void _Ready()
    {
        Instance = this;
        SaveWasLoaded = false; // reset for each new GameWorld session

        // Wire the SaveUtil provider delegates so client code (LoadGamePanel) can enumerate
        // and preview saves through shared/ without a client/ → server/ import.
        // Runs unconditionally — both host-server and dedicated server need these.
        SaveUtil.ListSavesProvider   = DoListSaves;
        SaveUtil.PeekSessionProvider = DoPeekSession;

        if (!Multiplayer.IsServer()) return;

        // Defer load to next frame so all sibling systems' OnPlayerConnected
        // handlers (HealthSystem, SkillSystem, NeedsSystem, etc.) fire first,
        // giving them time to initialize default per-peer state that we then overwrite.
        CallDeferred(MethodName.TryLoad);

        // Register for peers that connect AFTER the initial load (reconnect / new clients).
        NetworkManager.Instance.PlayerConnected += OnLatePlayerConnected;

        // Client UI (PauseMenu) raises this event to request a save without a direct
        // cross-namespace dependency on SaveSystem.
        GameSession.SaveRequested += Save;
    }

    public override void _Process(double delta)
    {
        if (!Multiplayer.IsServer()) return;

        _autosaveTimer += (float)delta;
        if (_autosaveTimer >= AUTOSAVE_INTERVAL)
        {
            _autosaveTimer = 0f;
            Save();
            GD.Print("[Save] Autosave complete");
        }
    }

    public override void _ExitTree()
    {
        GameSession.SaveRequested -= Save;
        if (Multiplayer.IsServer())
        {
            Save();
            GD.Print("[Save] Exit save written");
        }
        if (Instance == this) Instance = null!;
    }

    // ── Save ──────────────────────────────────────────────────────────────────

    public void Save()
    {
        if (!Multiplayer.IsServer()) return;

        var data = new SaveData { WorldSeed = GameSession.WorldSeed };

        // Settlement: markers, placed buildings, stockpile.
        if (SettlementSystem.Instance != null)
        {
            var (markers, buildings, stockpile) = SettlementSystem.Instance.GetSaveState();
            data.Markers   = markers;
            data.Buildings = buildings;
            data.Stockpile = stockpile;
        }

        // Felled tree IDs (so they don't respawn on reload).
        if (TreeSystem.Instance != null)
            data.FelledTreeIds = TreeSystem.Instance.GetFelledTreeIdsForSave();

        // Active NPC work assignments.
        if (VillageSystem.Instance != null)
            data.NpcAssignments = VillageSystem.Instance.GetAssignmentsForSave();

        // Per-peer state.
        if (HealthSystem.Instance != null)
        {
            foreach (long peerId in HealthSystem.Instance.GetPlayerIds())
            {
                var ps = new PlayerSave { PeerId = peerId };

                // Inventory + hotbar.
                var inv = InventorySystem.Instance?.GetInventory(peerId) ?? new PlayerInventory();
                foreach (var (id, cnt) in inv.Items)
                    ps.Items[id] = cnt;
                for (int i = 0; i < 9; i++)
                    ps.HotbarSlots[i] = inv.GetHotbarSlot(i);

                // Equipment slots (inventory.md §10.6).
                ps.EquippedMainHand  = inv.EquippedMainHand;
                ps.EquippedOffHand   = inv.EquippedOffHand;
                ps.EquippedBodyArmor = inv.EquippedBodyArmor;

                // Skill raw XP and class bumps.
                if (SkillSystem.Instance != null)
                {
                    ps.SkillXp    = SkillSystem.Instance.GetXpForSave(peerId);
                    ps.SkillBumps = SkillSystem.Instance.GetBumpsForSave(peerId);
                }

                // HP (current + rolled base so MaxHp is correctly restored on load).
                ps.Hp     = HealthSystem.Instance.GetHealth(peerId)?.CurrentHp ?? 100f;
                ps.BaseHp = HealthSystem.Instance.GetBaseHp(peerId);

                // Needs.
                if (NeedsSystem.Instance != null)
                {
                    var (hunger, rest) = NeedsSystem.Instance.GetNeeds(peerId);
                    ps.Hunger = hunger;
                    ps.Rest   = rest;
                }

                // World position from the player node.
                var node = GetNodeOrNull<Node3D>($"{PLAYERS_PATH}/Player_{peerId}");
                if (node != null)
                {
                    ps.PosX = node.GlobalPosition.X;
                    ps.PosY = node.GlobalPosition.Y;
                    ps.PosZ = node.GlobalPosition.Z;
                }

                data.Players.Add(ps);
            }
        }

        // Fog-of-war grid (worldgen.md §11.4).
        if (FogSystem.Instance != null)
            data.FogBase64 = FogSystem.Instance.GetFogBase64();

        // Session character data (so Load Game can restore GameSession without CharacterCreateScreen).
        data.Session = new SessionSave
        {
            ChosenClassId   = GameSession.ChosenClassId,
            ChosenRaceId    = GameSession.ChosenRaceId,
            RolledStats     = GameSession.RolledStats,
            HumanChosenStat = GameSession.HumanChosenStat,
            Alignment       = GameSession.ChosenAlignment.ToString().ToLowerInvariant()
        };

        EnsureSaveDir();

        var path = SavePath;
        var opts = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(data, opts);

        using var f = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (f == null)
        {
            GD.PrintErr($"[Save] Cannot write to {path}: {FileAccess.GetOpenError()}");
            return;
        }
        f.StoreString(json);

        GD.Print($"[Save] Saved {data.Players.Count} player(s), {data.Buildings.Count} building(s) → {path}");
    }

    // ── Load ─────────────────────────────────────────────────────────────────

    public void TryLoad()
    {
        if (!Multiplayer.IsServer()) return;

        var path = SavePath;
        if (!FileAccess.FileExists(path))
        {
            GD.Print($"[Save] No save file at {path} — starting fresh");
            _loaded = true;
            return;
        }

        SaveData data;
        try
        {
            using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (f == null)
            {
                GD.PrintErr($"[Save] Cannot read save at {path}: {FileAccess.GetOpenError()}");
                _loaded = true;
                return;
            }
            var json = f.GetAsText();
            data = JsonSerializer.Deserialize<SaveData>(json)
                   ?? throw new System.Exception("Deserializer returned null");
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[Save] Corrupt save — starting fresh. ({ex.Message})");
            _loaded = true;
            return;
        }

        // Reject saves from a different world.
        if (data.WorldSeed != GameSession.WorldSeed)
        {
            GD.PrintErr($"[Save] Seed mismatch (file={data.WorldSeed} vs session={GameSession.WorldSeed}) — ignoring");
            _loaded = true;
            return;
        }

        // Run forward migrations so old saves load cleanly under the current schema.
        MigrateForward(data);

        GD.Print($"[Save] Loading v{data.Version}: seed={data.WorldSeed}, " +
                 $"{data.Players.Count} player(s), {data.Buildings.Count} building(s)");

        // Signal that a real save was loaded — used by HealthSystem.RequestSetClass to skip
        // re-distributing the starting kit (which would overwrite the restored inventory).
        SaveWasLoaded = true;

        // Restore session character data so subsequent systems have correct class/race/stats.
        if (data.Session != null)
        {
            GameSession.ChosenClassId   = data.Session.ChosenClassId;
            GameSession.ChosenRaceId    = data.Session.ChosenRaceId;
            GameSession.RolledStats     = data.Session.RolledStats;
            GameSession.HumanChosenStat = data.Session.HumanChosenStat;
            GameSession.ChosenAlignment = AlignmentExtensions.FromString(data.Session.Alignment);
        }

        // Restore fog-of-war grid (worldgen.md §11.4) and push it to all clients.
        // BroadcastFog() must be called explicitly here because _Process only broadcasts
        // when UpdateVisibility() returns changed=true — already-explored cells never
        // re-trigger as changed, so restored fog would otherwise be invisible to clients.
        FogSystem.Instance?.RestoreFogFromBase64(data.FogBase64);
        FogSystem.Instance?.BroadcastFog();

        // Restore felled trees (hide nodes that were cut last session).
        TreeSystem.Instance?.RestoreFelledTreesFromSave(data.FelledTreeIds);

        // Restore settlement (spawns marker + building nodes on all peers via RPC).
        SettlementSystem.Instance?.RestoreFromSave(data.Markers, data.Buildings, data.Stockpile);

        // Restore NPC assignments.
        VillageSystem.Instance?.RestoreAssignmentsFromSave(data.NpcAssignments);

        // Restore per-peer state.
        foreach (var ps in data.Players)
        {
            long peerId = ps.PeerId;

            InventorySystem.Instance?.RestoreInventoryFromSave(
                peerId, ps.Items, ps.HotbarSlots,
                ps.EquippedMainHand, ps.EquippedOffHand, ps.EquippedBodyArmor);
            SkillSystem.Instance?.RestoreSkillsFromSave(peerId, ps.SkillXp, ps.SkillBumps);
            HealthSystem.Instance?.RestoreHpFromSave(peerId, ps.Hp, ps.BaseHp);
            NeedsSystem.Instance?.RestoreNeedsFromSave(peerId, ps.Hunger, ps.Rest);

            // Move player to saved position (skip if position is origin, which means it wasn't saved).
            if (ps.PosX != 0f || ps.PosY != 0f || ps.PosZ != 0f)
            {
                var node = GetNodeOrNull($"{PLAYERS_PATH}/Player_{peerId}");
                node?.Call("ForceRespawn", new Vector3(ps.PosX, ps.PosY, ps.PosZ));
            }
        }

        _loaded = true;
        GD.Print("[Save] Load complete");
    }

    // ── SaveUtil provider implementations ────────────────────────────────────
    // These are wired into SaveUtil.ListSavesProvider / PeekSessionProvider in _Ready()
    // so that client code (LoadGamePanel) can call through shared/SaveUtil without a
    // client/ → server/ import. The Godot DirAccess/FileAccess dependency stays here.

    private static System.Collections.Generic.List<string> DoListSaves()
    {
        var result = new System.Collections.Generic.List<string>();
        var dir = DirAccess.Open(SaveUtil.SaveDir);
        if (dir == null) return result;

        dir.ListDirBegin();
        string name = dir.GetNext();
        while (name != "")
        {
            if (!dir.CurrentIsDir() && name.EndsWith(".json"))
                result.Add(name[..^5]); // strip .json
            name = dir.GetNext();
        }
        dir.ListDirEnd();

        // Descending by name: timestamp slots sort newest-first.
        result.Sort(static (a, b) => string.CompareOrdinal(b, a));
        return result;
    }

    private static SessionSave? DoPeekSession(string saveName)
    {
        var path = $"{SaveUtil.SaveDir}/{saveName}.json";
        if (!FileAccess.FileExists(path)) return null;
        try
        {
            using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (f == null) return null;
            var data = JsonSerializer.Deserialize<SaveData>(f.GetAsText());
            return data?.Session;
        }
        catch { return null; }
    }

    // ── Save format migrations ───────────────────────────────────────────────

    /// <summary>
    /// Applies forward migrations so saves from any prior version load correctly
    /// under the current schema. Each case bumps data.Version after applying its changes.
    /// Add a new case for every SaveData.Version increment — even additive-only fields
    /// need a stub so the chain remains complete (ARCHITECTURE.md §8.2).
    /// </summary>
    private static void MigrateForward(SaveData data)
    {
        // v1 → v2: SessionSave.Alignment added. Null on old saves defaults to Neutral in
        //          GameSession on load (AlignmentExtensions.FromString(null) returns Neutral).
        //          No data transformation needed; stub keeps the version chain intact.
        if (data.Version < 2) data.Version = 2;
    }

    // ── Save directory helpers ────────────────────────────────────────────────

    private static void EnsureSaveDir()
    {
        var abs = ProjectSettings.GlobalizePath(SaveUtil.SaveDir);
        if (!DirAccess.DirExistsAbsolute(abs))
            DirAccess.MakeDirRecursiveAbsolute(abs);
    }

    // ── Reconnect state push ──────────────────────────────────────────────────

    /// <summary>
    /// Called when a peer connects AFTER the initial TryLoad() has completed.
    /// Replays the current in-memory state to bring the new client up to speed.
    /// Peer 1 (host/solo) is always handled by TryLoad(); ignore it here.
    /// </summary>
    private void OnLatePlayerConnected(long peerId)
    {
        if (!_loaded || peerId == 1L) return;
        SendFullStateToClient(peerId);
    }

    private void SendFullStateToClient(long peerId)
    {
        // Re-send inventory + hotbar, skills, and HP using the existing sync helpers.
        // Needs drains ≤ 2s for the next NeedsSystem broadcast — acceptable gap.
        InventorySystem.Instance?.SyncInventoryAndHotbarTo(peerId);
        SkillSystem.Instance?.BroadcastLevelsTo(peerId);
        HealthSystem.Instance?.SyncHealthTo(peerId);
        VillageSystem.Instance?.BroadcastRosterToAll();

        // Re-send fog state (otherwise the new client starts with a black map).
        FogSystem.Instance?.SendFogToClient(peerId);

        GD.Print($"[Save] Full state replayed to reconnected peer {peerId}");
    }
}
