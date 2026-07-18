using System.Collections.Generic;
using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Server;

/// <summary>
/// Server-authoritative HP tracker for all entities (players + monsters).
///
/// Player IDs = Godot peer IDs (long). Monster IDs = sequential longs assigned
/// by MonsterSystem (starting at 10001 to avoid collision with peer IDs).
///
/// On damage reaching 0:
///   Players → inventory cleared, respawn at shelter, HP restored, HP synced.
///   Monsters → death logged; MonsterSystem polls IsAlive() to handle loot/despawn (Phase 4).
///
/// HP is broadcast to ALL peers on every change so coop partners can see ally bars.
/// Each client only applies the update to LocalState when entityId == own peer ID.
///
/// Player HP is rolled at character creation using class Hit Dice + Con modifier per die (M9).
/// Node must appear in GameWorld.tscn AFTER InventorySystem and SettlementSystem.
/// </summary>
public partial class HealthSystem : Node
{
    public static HealthSystem Instance { get; private set; } = null!;

    private const string PLAYERS_PATH = "/root/GameWorld/Players";

    // HP for all entities. SortedDictionary: ADR-0011 deterministic iteration.
    private readonly SortedDictionary<long, (float current, float max)> _health  = new();
    // Subset of IDs that are players — drives which death path to take.
    private readonly SortedSet<long>                                     _players = new();

    // Rolled base HP per player (set once at character creation, restored from save).
    // Null = not yet rolled (e.g. peer connected but stats not received yet).
    private readonly SortedDictionary<long, float?> _playerBaseHp       = new();
    // Cumulative Athletics HP bonus: floor(level / 2) — grows as skill levels up.
    private readonly SortedDictionary<long, int>    _playerAthleticsBonus = new();

    // Seeded RNG for HP rolls: WorldSeed XOR peerId (reproducible but per-player unique).
    // Only created when the first HP roll is needed.
    private System.Random? _hpRng;

    // Item drops: server-side item maps keyed by drop ID.
    private long _nextDropId = 1L;
    private readonly SortedDictionary<long, System.Collections.Generic.Dictionary<string, int>>
        _itemDrops = new();

    public override void _Ready()
    {
        Instance = this;
        var net = NetworkManager.Instance;
        net.PlayerConnected    += OnPlayerConnected;
        net.PlayerDisconnected += OnPlayerDisconnected;
    }

    private void OnPlayerConnected(long peerId)
    {
        _playerBaseHp[peerId]        = null; // will be set when stats arrive
        _playerAthleticsBonus[peerId] = 0;

        // Temporary HP until ApplyConstitution resolves the rolled value.
        _health[peerId] = (100f, 100f);
        _players.Add(peerId);
        SendHealthTo(peerId);
        GD.Print($"[Health] peer {peerId} registered (pending stat roll)");

        // Distribute starting kit from the class the player selected.
        // For the host/solo player this is always correct. Remote clients will call
        // RequestSetClass immediately after spawning to override with their own choice.
        // Stats (StatBlock) arrive separately via CombatSystem.RequestSetStats.
        var kit = ClassKitRegistry.Find(GameSession.ChosenClassId)
                  ?? ClassKitRegistry.Find("class.fighter");
        if (kit != null)
        {
            foreach (var item in kit.StartingItems)
                InventorySystem.Instance.AddItem(peerId, item.ItemId, item.Count);
            AutoEquipKitItems(peerId, kit);
            GD.Print($"[Health] peer {peerId} received {kit.ClassId} kit ({kit.StartingItems.Length} stacks)");
        }
    }

    private void OnPlayerDisconnected(long peerId)
    {
        _health.Remove(peerId);
        _players.Remove(peerId);
        _playerBaseHp.Remove(peerId);
        _playerAthleticsBonus.Remove(peerId);
    }

    // ── Class selection RPC ───────────────────────────────────────────────────

    /// <summary>
    /// Called by the local player's PlayerController immediately after spawning.
    /// Clears and re-distributes the correct kit for the peer's chosen class,
    /// fixing the case where a remote client has a different class than the host.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestSetClass(string classId)
    {
        if (!Multiplayer.IsServer()) return;

        long sender = Multiplayer.GetRemoteSenderId();
        if (sender == 0) sender = 1L;

        // When loading from a save, the inventory, equipment, and skills are already restored
        // by SaveSystem.TryLoad() (which runs in deferred slot 2, before AnnounceClass fires
        // in deferred slot 4). Re-giving the kit here would overwrite the restored state.
        // Note: this guard is M8-safe for solo play. In a future multiplayer load scenario,
        // newly-joining peers (not in the save) would also be skipped — those cases need a
        // per-peer "was this peer in the save?" check.
        if (SaveSystem.SaveWasLoaded) return;

        var kit = ClassKitRegistry.Find(classId) ?? ClassKitRegistry.Find("class.fighter");
        if (kit == null) return;

        // Clear any items from the default kit given in OnPlayerConnected.
        // ForceRemove (not RemoveItems) because RemoveItems requires exact stock and
        // would silently fail for quantities like 1 shortbow with a request of 999.
        foreach (var kitDef in ClassKitRegistry.All)
            foreach (var item in kitDef.StartingItems)
                InventorySystem.Instance.ClearItem(sender, item.ItemId);

        foreach (var item in kit.StartingItems)
            InventorySystem.Instance.AddItem(sender, item.ItemId, item.Count);

        AutoEquipKitItems(sender, kit);

        // Apply class skill bumps (M5): flat starting bonuses defined in ClassKitData.SkillBumps.
        SkillSystem.Instance?.ApplyBump(sender, kit.SkillBumps);

        // Stats (StatBlock) are managed separately by CombatSystem.RequestSetStats.
        GD.Print($"[Health] peer {sender} confirmed class {classId} ({kit.StartingItems.Length} stacks)");
    }

    // ── Player HP: rolled base + Athletics growth ─────────────────────────────

    /// <summary>
    /// Called by CombatSystem.RequestSetStats when a peer's Constitution score arrives.
    /// If the peer's base HP has not been rolled yet, performs the initial roll and
    /// broadcasts the new max HP.
    /// </summary>
    public void ApplyConstitution(long peerId, int con)
    {
        if (!_health.ContainsKey(peerId)) return;
        if (_playerBaseHp.TryGetValue(peerId, out float? existing) && existing.HasValue)
            return; // already rolled — stats re-send on load should not re-roll

        // Derive which class kit this peer is using to know the HD count.
        var kit = ClassKitRegistry.Find(GameSession.ChosenClassId)
                  ?? ClassKitRegistry.Find("class.fighter");
        int hd      = kit?.HitDiceCount ?? 4;
        int dieSize = kit?.HitDieSize   ?? 8;

        float baseHp = RollPlayerHp(peerId, hd, dieSize, con);
        _playerBaseHp[peerId] = baseHp;

        RecomputeMaxHp(peerId);
        GD.Print($"[Health] peer {peerId} HP rolled: {hd}d{dieSize}+ConMod({con}) → base {baseHp:F1}");
    }

    /// <summary>
    /// Called by SkillSystem.NotifyAction when skill.athletics crosses a new level.
    /// Awards floor(newLevel/2) - floor(oldLevel/2) bonus HP.
    /// </summary>
    public void OnAthleticsLevelUp(long peerId, int newLevel)
    {
        if (!_health.ContainsKey(peerId)) return;

        _playerAthleticsBonus.TryGetValue(peerId, out int oldBonus);
        int newBonus = newLevel / 2;          // floor(level/2)
        if (newBonus <= oldBonus) return;

        _playerAthleticsBonus[peerId] = newBonus;
        RecomputeMaxHp(peerId);
        GD.Print($"[Health] peer {peerId} Athletics level {newLevel} → HP bonus +{newBonus - oldBonus} (total +{newBonus})");
    }

    /// <summary>
    /// Returns the peer's rolled base HP for SaveSystem to persist.
    /// Defaults to 100f if not yet rolled (should not happen in normal flow).
    /// </summary>
    public float GetBaseHp(long peerId) =>
        _playerBaseHp.TryGetValue(peerId, out float? v) && v.HasValue ? v.Value : 100f;

    /// <summary>
    /// Rolls peerId's starting HP: sum of HitDiceCount rolls of 1dDieSize,
    /// each clamped to minimum 1, with StatModifier(con) added per die.
    /// Uses a seeded RNG derived from WorldSeed XOR peerId for reproducibility.
    /// </summary>
    private float RollPlayerHp(long peerId, int hd, int dieSize, int con)
    {
        // One seeded RNG per HealthSystem lifetime, seeded from WorldSeed.
        // Each peer salts with its own ID before rolling to avoid identical results.
        _hpRng ??= new System.Random((int)(GameSession.WorldSeed ^ 0xD1CE1234u));

        int conMod = CombatResolver.StatModifier(con);
        float total = 0f;
        for (int i = 0; i < hd; i++)
        {
            int roll = _hpRng.Next(1, dieSize + 1); // 1..dieSize inclusive
            total += System.Math.Max(1, roll + conMod);
        }
        return total;
    }

    /// <summary>
    /// Recomputes a peer's MaxHp from baseHp + Athletics bonus, then updates _health
    /// (clamping current HP to the new max) and syncs to the client.
    /// </summary>
    private void RecomputeMaxHp(long peerId)
    {
        if (!_health.TryGetValue(peerId, out var h)) return;

        float baseHp     = _playerBaseHp.TryGetValue(peerId, out float? b) && b.HasValue ? b.Value : 100f;
        int   athBonus   = _playerAthleticsBonus.TryGetValue(peerId, out int ab) ? ab : 0;
        float newMax     = baseHp + athBonus;
        float newCurrent = System.Math.Min(h.current, newMax);

        _health[peerId] = (newCurrent, newMax);
        BroadcastHealth(peerId, newCurrent, newMax);
    }

    // ── Bandage use ───────────────────────────────────────────────────────────

    /// <summary>
    /// Client presses Tab with bandage in active hotbar slot.
    /// Consumes 1 bandage, heals 20 + floor(ForagingLevel / 5) HP, capped at 40.
    /// Does nothing if the player is already at full HP or has no bandage.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestUseBandage()
    {
        if (!Multiplayer.IsServer()) return;

        long sender = Multiplayer.GetRemoteSenderId();
        if (sender == 0) sender = 1L;

        if (!_health.TryGetValue(sender, out var h)) return;
        if (h.current >= h.max) return; // already full — don't consume
        if (!InventorySystem.Instance.HasItems(sender, "item.bandage", 1)) return;

        InventorySystem.Instance.RemoveItems(sender, "item.bandage", 1);

        int foraging = SkillSystem.Instance?.GetSkillLevel(sender, "skill.foraging") ?? 0;
        // Integer division gives floor(foraging/5) — matches "1 HP per 5 skill levels" design.
        float heal   = Mathf.Min(
            SettlementSystem.BANDAGE_MAX_HEAL,
            SettlementSystem.BANDAGE_BASE_HEAL + (foraging / 5) * SettlementSystem.BANDAGE_HEAL_PER_5_SKILL
        );
        ApplyHeal(sender, heal);
        GD.Print($"[Health] peer {sender} used bandage → healed {heal:F1} HP (Foraging {foraging})");
    }

    // ── Server API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Registers a non-player entity (monster). Called by MonsterSystem on spawn.
    /// </summary>
    public void RegisterEntity(long id, float maxHp)
    {
        _health[id] = (maxHp, maxHp);
        GD.Print($"[Health] entity {id} registered ({maxHp}/{maxHp} hp)");
    }

    /// <summary>Removes a non-player entity from tracking. Called by MonsterSystem on despawn.</summary>
    public void UnregisterEntity(long id)
    {
        _health.Remove(id);
    }

    /// <summary>Returns true if the entity exists and has HP > 0.</summary>
    public bool IsAlive(long id) =>
        _health.TryGetValue(id, out var h) && h.current > 0f;

    /// <summary>Returns a health snapshot, or null if the entity is not registered.</summary>
    public HealthData? GetHealth(long id) =>
        _health.TryGetValue(id, out var h) ? new HealthData(h.current, h.max) : null;

    /// <summary>Returns the set of currently registered player peer IDs. Used by SaveSystem.</summary>
    public IReadOnlyCollection<long> GetPlayerIds() => _players;

    /// <summary>
    /// Overwrites a peer's HP from save data and syncs to the client.
    /// Called by SaveSystem.TryLoad().
    /// baseHp defaults to hp if the save predates the BaseHp field.
    /// </summary>
    public void RestoreHpFromSave(long peerId, float hp, float baseHp = 0f)
    {
        if (!_health.ContainsKey(peerId)) return;

        // If baseHp was not in the save (old save), use hp as a reasonable baseline.
        if (baseHp <= 0f) baseHp = hp;
        _playerBaseHp[peerId] = baseHp;

        // Restore Athletics bonus from SkillSystem if it has loaded skills already.
        int athLevel = SkillSystem.Instance?.GetSkillLevel(peerId, "skill.athletics") ?? 0;
        _playerAthleticsBonus[peerId] = athLevel / 2;

        float maxHp  = baseHp + _playerAthleticsBonus[peerId];
        _health[peerId] = (Mathf.Max(1f, hp), maxHp); // clamp current to at least 1
        SendHealthTo(peerId);
        GD.Print($"[Health] peer {peerId} HP restored: {hp:F1}/{maxHp:F1} (base {baseHp:F1}, ath bonus {_playerAthleticsBonus[peerId]})");
    }

    /// <summary>Sends the current HP to a peer. Used for reconnect replay.</summary>
    public void SyncHealthTo(long peerId) => SendHealthTo(peerId);

    /// <summary>
    /// Reduces entity HP by amount. Triggers death if HP reaches 0.
    /// Called by CombatSystem (melee) and ProjectileSystem (ranged).
    /// </summary>
    public void ApplyDamage(long entityId, float amount)
    {
        if (!_health.TryGetValue(entityId, out var h)) return;
        if (h.current <= 0f) return; // already dead — ignore

        // Apply vulnerability multiplier (e.g. Overextended fumble effect).
        // Multiplicative condensation: effective = amount * (1 + Σ factors) — see BuffCalculator.
        float vulnMultiplier = BuffSystem.Instance?.GetMultiplicativeModifier(entityId, BuffStat.IncomingDamage) ?? 1f;
        amount *= vulnMultiplier;

        float newHp = Mathf.Max(0f, h.current - amount);
        _health[entityId] = (newHp, h.max);

        BroadcastHealth(entityId, newHp, h.max);
        GD.Print($"[Health] entity {entityId} took {amount:F1} dmg → {newHp:F1}/{h.max:F1} hp");

        if (newHp <= 0f)
        {
            if (_players.Contains(entityId))
                KillPlayer(entityId);
            else
                KillMonster(entityId);
        }
    }

    /// <summary>Restores HP up to max. Does nothing if entity is dead or unknown.</summary>
    public void ApplyHeal(long entityId, float amount)
    {
        if (!_health.TryGetValue(entityId, out var h)) return;
        if (h.current <= 0f) return;

        float newHp = Mathf.Min(h.max, h.current + amount);
        _health[entityId] = (newHp, h.max);
        BroadcastHealth(entityId, newHp, h.max);
    }

    // ── Death ─────────────────────────────────────────────────────────────────

    private void KillPlayer(long peerId)
    {
        GD.Print($"[Health] peer {peerId} died — dropping inventory, respawning");
        BuffSystem.Instance?.ClearAllBuffs(peerId);

        // Snapshot and clear inventory, then spawn a physical item drop at the death position.
        var dropped = InventorySystem.Instance.TakeAll(peerId);
        if (dropped.Items.Count > 0)
        {
            var deathPos = GetPlayerPosition(peerId);
            if (deathPos.HasValue)
            {
                long deathDropId = _nextDropId; // SpawnItemDrop will consume this ID.
                var loot = new System.Collections.Generic.Dictionary<string, int>();
                foreach (var (itemId, cnt) in dropped.Items)
                    loot[itemId] = cnt;
                SpawnItemDrop(deathPos.Value, loot);

                // Tell only the dying peer where their death drop is so they see it on the map.
                if (peerId == 1)
                    ClientShowDeathMarker(deathDropId, deathPos.Value.X, deathPos.Value.Z);
                else
                    RpcId(peerId, MethodName.ClientShowDeathMarker, deathDropId, deathPos.Value.X, deathPos.Value.Z);
            }
        }

        // Restore HP and needs before sending respawn so the client never sees 0 at the new position.
        // Use the peer's actual rolled maxHp, not a constant.
        float baseHp  = _playerBaseHp.TryGetValue(peerId, out float? b) && b.HasValue ? b.Value : 100f;
        int   athBonus = _playerAthleticsBonus.TryGetValue(peerId, out int ab) ? ab : 0;
        float maxHp   = baseHp + athBonus;
        _health[peerId] = (maxHp, maxHp);
        NeedsSystem.Instance?.ResetNeeds(peerId);

        var respawnPos = SettlementSystem.Instance.GetRespawnPosition(peerId);
        var playerNode = GetNodeOrNull($"{PLAYERS_PATH}/Player_{peerId}");
        if (playerNode != null)
        {
            if (peerId == 1)
                playerNode.Call("ForceRespawn", respawnPos);
            else
                playerNode.RpcId(peerId, "ForceRespawn", respawnPos);
        }

        SendHealthTo(peerId);
    }

    private void KillMonster(long monsterId)
    {
        // MonsterSystem polls IsAlive() each tick and handles loot drop + despawn.
        GD.Print($"[Health] monster {monsterId} died");
        BuffSystem.Instance?.ClearAllBuffs(monsterId);
        // HP stays at 0 until MonsterSystem calls UnregisterEntity after handling the death.
    }

    // ── Item drops ────────────────────────────────────────────────────────────

    /// <summary>
    /// Spawns a physical item drop node at the given position on all clients.
    /// Called by KillPlayer (player death) and MonsterSystem.HandleDeath (monster loot).
    /// Server-only — clients should not call this directly.
    /// </summary>
    public void SpawnItemDrop(
        Vector3 pos,
        System.Collections.Generic.Dictionary<string, int> items)
    {
        if (!Multiplayer.IsServer()) return;
        if (items.Count == 0) return;

        long dropId = _nextDropId++;
        _itemDrops[dropId] = items;

        var godotDict = new Godot.Collections.Dictionary<string, int>();
        foreach (var (id, cnt) in items)
            godotDict[id] = cnt;

        Rpc(MethodName.ClientSpawnItemDrop, dropId, pos, godotDict);
        GD.Print($"[Health] item drop {dropId} spawned at {pos} ({items.Count} unique items)");
    }

    /// <summary>
    /// Client requests to pick up an item drop.
    /// Server validates ownership (first come first served), transfers items, removes drop.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestPickupDrop(long dropId)
    {
        if (!Multiplayer.IsServer()) return;

        long sender = Multiplayer.GetRemoteSenderId();
        if (sender == 0) sender = 1L;

        if (!_itemDrops.TryGetValue(dropId, out var items)) return; // already picked up

        foreach (var (itemId, count) in items)
            InventorySystem.Instance.AddItem(sender, itemId, count);

        _itemDrops.Remove(dropId);
        Rpc(MethodName.ClientRemoveItemDrop, dropId);
        GD.Print($"[Health] peer {sender} picked up drop {dropId}");
    }

    /// <summary>Creates a glowing sphere node for the item drop on all clients.</summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ClientSpawnItemDrop(
        long dropId,
        Vector3 pos,
        Godot.Collections.Dictionary<string, int> _items) // items sent for future tooltip use
    {
        var body = new StaticBody3D
        {
            Name           = $"ItemDrop_{dropId}",
            CollisionLayer = 128u, // Layer 8 — detected by PlayerController E-interact
            CollisionMask  = 0u
        };

        var shape = new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.5f } };
        body.AddChild(shape);

        var mesh = new MeshInstance3D
        {
            Mesh             = new SphereMesh { Radius = 0.3f, Height = 0.6f },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(1f, 0.85f, 0f), // golden yellow
                EmissionEnabled = true,
                Emission        = new Color(0.6f, 0.5f, 0f)
            }
        };
        body.AddChild(mesh);

        GetTree().CurrentScene.AddChild(body);
        body.GlobalPosition = pos + Vector3.Up * 0.4f;
    }

    /// <summary>Removes the item drop node on all clients once it has been picked up.</summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ClientRemoveItemDrop(long dropId)
    {
        var node = GetTree().CurrentScene.GetNodeOrNull($"ItemDrop_{dropId}");
        node?.QueueFree();
        LocalState.ClearDeathDrop(dropId); // removes map marker if this was the player's death drop
    }

    /// <summary>
    /// Tells the dying peer where their inventory drop landed so they can see it on the map.
    /// Only sent to the specific peer whose inventory was dropped — not broadcast to all.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ClientShowDeathMarker(long dropId, float worldX, float worldZ)
    {
        LocalState.SetDeathDrop(dropId, worldX, worldZ);
    }

    /// <summary>
    /// Auto-equips starting items from a class kit into the appropriate equipment slots
    /// (inventory.md §10.2). Inferred from WeaponRegistry / ArmorRegistry:
    ///   weapon → Main Hand, shield → Off-Hand, other armor → Body Armor.
    /// Non-weapon/non-armor items (arrows, food) are left in inventory unequipped.
    /// </summary>
    private static void AutoEquipKitItems(long peerId, ClassKitData kit)
    {
        foreach (var kitItem in kit.StartingItems)
        {
            var weapon = WeaponRegistry.Find(kitItem.ItemId);
            if (weapon != null)
            {
                InventorySystem.Instance.EquipItem(peerId, EquipSlot.MainHand, kitItem.ItemId);
                continue;
            }
            var armor = ArmorRegistry.Find(kitItem.ItemId);
            if (armor != null)
            {
                var slot = armor.ShieldBonus > 0 ? EquipSlot.OffHand : EquipSlot.BodyArmor;
                InventorySystem.Instance.EquipItem(peerId, slot, kitItem.ItemId);
            }
            // Arrows, food, resources — not equippable, stay in inventory.
        }
    }

    /// <summary>Returns the world position of a connected player's node, or null.</summary>
    private Vector3? GetPlayerPosition(long peerId)
    {
        var node = GetNodeOrNull<Node3D>($"{PLAYERS_PATH}/Player_{peerId}");
        return node?.GlobalPosition;
    }

    // ── Sync ──────────────────────────────────────────────────────────────────

    /// <summary>Broadcasts to all peers — used on every damage/heal event.</summary>
    private void BroadcastHealth(long entityId, float currentHp, float maxHp)
    {
        Rpc(MethodName.ApplyHealth, entityId, currentHp, maxHp);
    }

    /// <summary>Sends to one peer only — used on connect and after respawn.</summary>
    private void SendHealthTo(long peerId)
    {
        if (!_health.TryGetValue(peerId, out var h)) return;
        if (peerId == 1)
            ApplyHealth(peerId, h.current, h.max);
        else
            RpcId(peerId, MethodName.ApplyHealth, peerId, h.current, h.max);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ApplyHealth(long entityId, float currentHp, float maxHp)
    {
        // Only the local player cares about its own HP in LocalState.
        // Coop partner HP could drive a party bar here in a future milestone.
        if (entityId == Multiplayer.GetUniqueId())
            LocalState.SetHealth(currentHp, maxHp);
    }
}
