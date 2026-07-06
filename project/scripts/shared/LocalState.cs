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

    /// <summary>Called by HealthSystem.ApplyHealth when the server pushes a HP update.</summary>
    public static void SetHealth(float currentHp, float maxHp)
    {
        CurrentHp = currentHp;
        MaxHp     = maxHp;
    }

    /// <summary>
    /// Fired on the local peer when the server rejects a building placement request.
    /// Subscribed by PlacementController to flash a message on screen.
    /// The string is already resolved via Loc.T before the event fires.
    /// </summary>
    public static event System.Action<string>? RejectionMessageReceived;

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
    /// True when the local player is in combat mode (LMB = attack).
    /// False (default) = build mode (LMB = building placement, B opens the build menu).
    /// Toggled by the "toggle_combat" input action (C key).
    /// </summary>
    public static bool InCombatMode { get; private set; } = false;

    /// <summary>
    /// Fired on every combat-mode flip. bool parameter is the new InCombatMode value.
    /// Subscribed by PlacementController (cancel ghost) and BuildMenu (close panel).
    /// </summary>
    public static event System.Action<bool>? CombatModeChanged;

    /// <summary>Flip combat/build mode. Called by PlayerController on "toggle_combat" input.</summary>
    public static void ToggleCombatMode()
    {
        InCombatMode = !InCombatMode;
        CombatModeChanged?.Invoke(InCombatMode);
    }

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
