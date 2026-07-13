namespace MankersKingdoms.Shared;

/// <summary>
/// Shared static accessor for local-player runtime state.
/// Written by server-side systems (InventorySystem, NeedsSystem, …) when they
/// receive a state update meant for this peer; read by client-side HUD nodes.
///
/// This pattern avoids a client/ → server/ import while still letting both sides
/// communicate through a shared neutral location. Not persisted — reset on scene change.
/// </summary>
public static class LocalState
{
    public static PlayerInventory Inventory { get; private set; } = new();
    public static float Hunger    { get; private set; } = 100f;
    public static float Rest      { get; private set; } = 100f;
    /// <summary>
    /// True once the local player has successfully planted a Kingdom Marker.
    /// Set by SettlementSystem.SpawnMarker (runs on all peers) when peerId matches
    /// the local peer. Used by BuildMenu to grey out Place buttons for guests.
    /// Server-side enforcement is independent — this is UX only.
    /// </summary>
    public static bool IsFounder  { get; private set; } = false;

    public static void SetFounder() => IsFounder = true;

    /// <summary>Local player's current and maximum HP. Written by HealthSystem via RPC.</summary>
    public static float CurrentHp { get; private set; } = 100f;
    public static float MaxHp     { get; private set; } = 100f;

    /// <summary>Fired when the local player takes damage but survives (HP decreased, still > 0).</summary>
    public static event System.Action? DamageTaken;

    /// <summary>Fired when the local player's HP reaches 0.</summary>
    public static event System.Action? PlayerDied;

    /// <summary>Fired when the local player's HP rises from 0 (respawn).</summary>
    public static event System.Action? PlayerRevived;

    /// <summary>
    /// Fired by BowController on the frame the local player dispatches a ranged fire RPC.
    /// Client-side only — fires before server confirmation, sufficient for animation timing.
    /// </summary>
    public static event System.Action? LocalArrowFired;

    /// <summary>Called by BowController immediately after dispatching RequestFireProjectile.</summary>
    public static void NotifyLocalArrowFired() => LocalArrowFired?.Invoke();

    /// <summary>Called by HealthSystem.ApplyHealth when the server pushes a HP update.</summary>
    public static void SetHealth(float currentHp, float maxHp)
    {
        bool  wasZero = CurrentHp <= 0f;
        float prev    = CurrentHp;
        CurrentHp     = currentHp;
        MaxHp         = maxHp;

        if      (!wasZero && currentHp <= 0f) PlayerDied?.Invoke();
        else if ( wasZero && currentHp >  0f) PlayerRevived?.Invoke();
        else if (!wasZero && currentHp < prev) DamageTaken?.Invoke();
    }

    /// <summary>
    /// Fired on the local peer when the server rejects a request or a settlement gate is
    /// not met (missing building, full capacity, etc.).
    /// Subscribed by PlacementController to flash a message on screen.
    /// The string is already resolved via Loc.T before the event fires.
    /// </summary>
    public static event System.Action<string>? RejectionMessageReceived;

    /// <summary>
    /// Fires a pre-resolved warning message through the same flash channel used for
    /// placement rejections. Called by VillageSystem and SettlementSystem to surface
    /// settlement gate failures (missing shelter, missing stockpile, etc.).
    /// </summary>
    public static void ShowWarning(string message) => RejectionMessageReceived?.Invoke(message);

    /// <summary>
    /// Called by SettlementSystem.ClientNotifyRejection (runs on client via RpcId).
    /// Composes "Not enough {item name}" from the item's existing loc key so no
    /// extra loc entries are needed per item. Fires the event for PlacementController.
    /// </summary>
    public static void NotifyRejection(string missingItemId)
    {
        // missingItemId format: "resource.wood" → stem "wood" → loc key "item.wood.name"
        string stem    = missingItemId.Contains('.') ? missingItemId.Split('.')[1] : missingItemId;
        string name    = Loc.T($"item.{stem}.name").ToLower();
        RejectionMessageReceived?.Invoke($"Not enough {name}.");
    }

    /// <summary>
    /// Fired when the local player's inventory changes.
    /// Subscribed by InventoryPanel (M5 Phase 3) and future inventory-sensitive HUD nodes.
    /// </summary>
    public static event System.Action? InventoryChanged;

    /// <summary>
    /// Called by InventorySystem when the authoritative server sends a state snapshot
    /// for the local peer. Replaces the whole inventory (snapshot semantics).
    /// </summary>
    public static void SetInventory(PlayerInventory inv)
    {
        Inventory = inv;
        InventoryChanged?.Invoke();
    }

    /// <summary>
    /// Called by NeedsSystem when the server broadcasts a needs snapshot for the local peer.
    /// </summary>
    public static void SetNeeds(float hunger, float rest)
    {
        Hunger = hunger;
        Rest   = rest;
    }

    // ── Projectile events ─────────────────────────────────────────────────────

    /// <summary>
    /// Fired on all peers when the server spawns a new projectile.
    /// Parameters: (id, originX, originY, originZ, dirX, dirY, dirZ, speed).
    /// Subscribed by BowController to spawn a client-side arrow ghost.
    /// </summary>
    public static event System.Action<long, float, float, float, float, float, float, float>? ArrowSpawned;

    /// <summary>
    /// Fired on all peers when a projectile is removed (hit or expired).
    /// Parameter: projectile ID. BowController frees the corresponding ghost.
    /// </summary>
    public static event System.Action<long>? ArrowRemoved;

    /// <summary>Called by ProjectileSystem.ClientSpawnArrow RPC (runs on all peers).</summary>
    public static void NotifyArrowSpawned(long id,
        float ox, float oy, float oz,
        float dx, float dy, float dz,
        float speed)
        => ArrowSpawned?.Invoke(id, ox, oy, oz, dx, dy, dz, speed);

    /// <summary>Called by ProjectileSystem.ClientRemoveArrow RPC (runs on all peers).</summary>
    public static void NotifyArrowRemoved(long id)
        => ArrowRemoved?.Invoke(id);

    // ── Combat / build mode ───────────────────────────────────────────────────

    /// <summary>
    /// <summary>
    /// True when the local player is in combat mode (LMB = attack).
    /// Default is true — combat is always on. Build mode is transient: entered when the
    /// build menu opens, exited automatically when a building is placed or placement is cancelled.
    /// </summary>
    public static bool InCombatMode { get; private set; } = true;

    /// <summary>
    /// Fired whenever InCombatMode changes.
    /// Subscribed by WeaponHUD (display update).
    /// </summary>
    public static event System.Action<bool>? CombatModeChanged;

    /// <summary>
    /// Explicitly set combat mode. Used by BuildMenu (false on open, true on close)
    /// and PlacementController (true after place or cancel).
    /// </summary>
    public static void SetCombatMode(bool inCombat)
    {
        if (InCombatMode == inCombat) return;
        InCombatMode = inCombat;
        CombatModeChanged?.Invoke(InCombatMode);
    }

    /// <summary>Legacy toggle — kept so existing callers compile; prefer SetCombatMode.</summary>
    public static void ToggleCombatMode() => SetCombatMode(!InCombatMode);

    // ── Weapon mode (within combat mode) ─────────────────────────────────────

    /// <summary>
    /// True when the local player prefers ranged attacks (bow) in combat mode.
    /// False (default) means melee. Toggled by the "toggle_weapon" input action (Q key).
    /// BowController yields LMB to MeleeController when this is false and a melee
    /// weapon is also in inventory.
    /// </summary>
    public static bool PreferRanged { get; private set; } = false;

    /// <summary>Flip the active weapon mode. Called by PlayerController on "toggle_weapon" input.</summary>
    public static void ToggleWeaponMode() => PreferRanged = !PreferRanged;

    // ── Death drop marker ─────────────────────────────────────────────────────

    /// <summary>
    /// World XZ position of the local player's last death drop, or null if none active.
    /// Shown as a red X on the minimap and world map.
    /// Set by HealthSystem.ClientShowDeathMarker; cleared when the drop is picked up.
    /// Stored as plain floats to avoid a Godot.Vector3 dependency in shared/.
    /// </summary>
    public static (float X, float Z)? DeathDropWorldPos { get; private set; }
    private static long _deathDropId = -1L;

    /// <summary>Called by HealthSystem when the server notifies this peer of their death drop.</summary>
    public static void SetDeathDrop(long dropId, float worldX, float worldZ)
    {
        _deathDropId      = dropId;
        DeathDropWorldPos = (worldX, worldZ);
    }

    /// <summary>
    /// Clears the marker if dropId matches the tracked drop.
    /// Called by HealthSystem.ClientRemoveItemDrop on pickup.
    /// </summary>
    public static void ClearDeathDrop(long dropId)
    {
        if (_deathDropId == dropId)
        {
            _deathDropId      = -1L;
            DeathDropWorldPos = null;
        }
    }

    // ── Follower NPC ─────────────────────────────────────────────────────────

    /// <summary>
    /// Villager ID of the NPC currently following this player, or "" if none.
    /// Set by VillageSystem.ClientSetFollower; cleared by ClientClearFollower.
    /// Read by RecruitmentDialogue (enable/disable Leave button) and StockpilePanel (Phase 3).
    /// </summary>
    public static string FollowerNpcId { get; private set; } = "";

    /// <summary>
    /// Fired when the follower NPC changes (parameter = new id, or "" if cleared).
    /// Subscribed by RecruitmentDialogue to refresh the Leave button state.
    /// </summary>
    public static event System.Action<string>? FollowerChanged;

    /// <summary>Called by VillageSystem when a villager is successfully recruited.</summary>
    public static void SetFollower(string npcId)
    {
        FollowerNpcId = npcId;
        FollowerChanged?.Invoke(npcId);
    }

    /// <summary>Called by VillageSystem when the follower is dismissed or assigned to a station.</summary>
    public static void ClearFollower()
    {
        FollowerNpcId = "";
        FollowerChanged?.Invoke("");
    }

    // ── Settlement stockpile ──────────────────────────────────────────────────

    /// <summary>
    /// Items accumulated in the local player's settlement stockpile.
    /// Written by SettlementSystem.ClientUpdateStockpile; read by StockpilePanel.
    /// </summary>
    public static System.Collections.Generic.IReadOnlyDictionary<string, int> StockpileSnapshot
        => _stockpile;

    private static readonly System.Collections.Generic.Dictionary<string, int> _stockpile = new();

    /// <summary>Fired whenever the stockpile contents change.</summary>
    public static event System.Action? StockpileChanged;

    /// <summary>
    /// Called by SettlementSystem.ClientUpdateStockpile (JSON-encoded dictionary).
    /// Replaces the whole snapshot (server is authoritative).
    /// </summary>
    public static void SetStockpile(string stockpileJson)
    {
        _stockpile.Clear();
        if (!string.IsNullOrEmpty(stockpileJson) && stockpileJson != "{}")
        {
            try
            {
                var dict = System.Text.Json.JsonSerializer
                    .Deserialize<System.Collections.Generic.Dictionary<string, int>>(stockpileJson);
                if (dict != null)
                    foreach (var (k, v) in dict) _stockpile[k] = v;
            }
            catch { /* malformed JSON — leave empty */ }
        }
        StockpileChanged?.Invoke();
    }

    // ── Kingdom Marker world position ─────────────────────────────────────────

    /// <summary>
    /// XZ world position of the local player's Kingdom Marker, or null if not planted.
    /// Set by SettlementSystem.SpawnMarker on the local peer.
    /// Used by PlayerController to detect proximity for stockpile panel access.
    /// Stored as plain floats to avoid a Godot.Vector3 dependency in shared/.
    /// </summary>
    public static (float X, float Z)? MarkerWorldPos { get; private set; }

    /// <summary>Called by SettlementSystem.SpawnMarker when this peer's marker is placed.</summary>
    public static void SetMarkerWorldPos(float x, float z) => MarkerWorldPos = (x, z);

    // ── Village roster ────────────────────────────────────────────────────────

    /// <summary>
    /// JSON array of settlement NPCs sent by VillageSystem.ClientSetVillageRoster.
    /// Consumed by BuildingAssignmentPanel to populate the assignment UI.
    /// Empty string = no NPCs yet recruited.
    /// </summary>
    public static string VillageRosterJson { get; private set; } = "[]";

    /// <summary>Fired when VillageRosterJson changes. BuildingAssignmentPanel refreshes its list.</summary>
    public static event System.Action? VillageRosterChanged;

    public static void SetVillageRoster(string json)
    {
        VillageRosterJson = json;
        VillageRosterChanged?.Invoke();
    }

    // ── Hotbar ────────────────────────────────────────────────────────────────

    private static readonly string?[] _hotbar = new string?[9];

    /// <summary>Index of the currently selected hotbar slot (0–8). Default 0.</summary>
    public static int ActiveHotbarSlot { get; private set; } = 0;

    /// <summary>
    /// Fired when a hotbar slot's content changes.
    /// Parameters: (slotIndex 0–8, itemId or null for empty).
    /// Subscribed by HotbarHUD to refresh individual slot labels.
    /// </summary>
    public static event System.Action<int, string?>? HotbarSlotChanged;

    /// <summary>
    /// Fired when the active (highlighted) hotbar slot changes.
    /// Parameter: new slot index 0–8. Subscribed by HotbarHUD to redraw highlight.
    /// </summary>
    public static event System.Action<int>? ActiveHotbarSlotChanged;

    /// <summary>
    /// Fired by HotbarHUD when a number key 1–9 is pressed so that InventoryPanel
    /// can assign the currently hovered item to that slot without needing a direct reference.
    /// Parameter: slot index 0–8.
    /// </summary>
    public static event System.Action<int>? HotbarKeyPressed;

    /// <summary>Returns the itemId assigned to the given hotbar slot, or null if empty.</summary>
    public static string? GetHotbarSlot(int slot) =>
        (slot >= 0 && slot < 9) ? _hotbar[slot] : null;

    /// <summary>
    /// Called by InventorySystem.ApplyHotbarSlot RPC (runs on owning peer).
    /// Applies move semantics: clears any other slot already holding the same itemId.
    /// </summary>
    public static void SetHotbarSlot(int slot, string? itemId)
    {
        if (slot < 0 || slot >= 9) return;
        if (itemId != null)
        {
            for (int i = 0; i < 9; i++)
            {
                if (_hotbar[i] == itemId)
                {
                    _hotbar[i] = null;
                    HotbarSlotChanged?.Invoke(i, null);
                }
            }
        }
        _hotbar[slot] = itemId;
        HotbarSlotChanged?.Invoke(slot, itemId);
    }

    /// <summary>Called by HotbarHUD when a number key changes the active slot.</summary>
    public static void SetActiveHotbarSlot(int slot)
    {
        if (slot < 0 || slot >= 9 || slot == ActiveHotbarSlot) return;
        ActiveHotbarSlot = slot;
        ActiveHotbarSlotChanged?.Invoke(slot);
    }

    /// <summary>
    /// Called by HotbarHUD when a number key is pressed (after updating active slot).
    /// InventoryPanel subscribes while open to assign the hovered item.
    /// </summary>
    public static void NotifyHotbarKeyPressed(int slot) => HotbarKeyPressed?.Invoke(slot);

    // ── Skill levels ──────────────────────────────────────────────────────────

    /// <summary>
    /// Current effective level per skill for the local player.
    /// Keys are skill IDs (e.g. "skill.melee"). Written by SkillSystem via RPC.
    /// </summary>
    public static System.Collections.Generic.IReadOnlyDictionary<string, int> SkillLevels
        => _skillLevels;

    private static readonly System.Collections.Generic.Dictionary<string, int> _skillLevels = new();

    /// <summary>
    /// Fired when any skill level changes. Parameter: (skillId, newLevel).
    /// Subscribed by the character sheet HUD in M5 Phase 4.
    /// </summary>
    public static event System.Action<string, int>? SkillLevelChanged;

    /// <summary>
    /// Called by SkillSystem.ClientApplySkillLevel RPC (runs on the owning peer).
    /// </summary>
    public static void SetSkillLevel(string skillId, int level)
    {
        if (_skillLevels.TryGetValue(skillId, out int existing) && existing == level) return;
        _skillLevels[skillId] = level;
        SkillLevelChanged?.Invoke(skillId, level);
    }
}
