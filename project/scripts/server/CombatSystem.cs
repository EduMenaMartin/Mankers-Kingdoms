using System.Collections.Generic;
using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Server;

/// <summary>
/// Server-authoritative melee and block handler.
///
/// RequestMeleeAttack: validates weapon ownership, swing cooldown, target liveness,
/// and attacker-to-target distance. If all gates pass:
///   1. Block gate (combat.md §2.5): if the target is actively blocking with a shield,
///      the attack never reaches the dice roll — server drops it entirely.
///   2. Attack roll: CombatResolver.ResolveAttack (1d20 + attackBonus vs targetNumber).
///   3. Damage application on hit.
///
/// RequestSetBlocking: records the blocking state of a peer. Shield ownership confirmed
///   server-side so the client cannot spoof block state without holding a shield.
///
/// BowController and ProjectileSystem handle ranged combat (Phase 3).
///
/// Seeded RNG: seeded from GameSession.WorldSeed ^ 0xC0MBAT01u in _Ready().
/// All rolls go through _combatRng per ADR-0022.
///
/// Node must appear in GameWorld.tscn AFTER HealthSystem and InventorySystem.
/// </summary>
public partial class CombatSystem : Node
{
    public static CombatSystem Instance { get; private set; } = null!;

    // Latency tolerance added to weapon range on server-side distance checks.
    private const float RANGE_TOLERANCE = 0.6f;

    private const string PLAYERS_PATH          = "/root/GameWorld/Players";
    private const string COMBAT_FEEDBACK_PATH  = "/root/GameWorld/CombatFeedbackHUD";

    // Server-only. SortedDictionary: ADR-0011 deterministic iteration.
    // Value = elapsed time at which the next swing is allowed.
    private readonly SortedDictionary<long, double> _swingReady = new();
    private readonly SortedDictionary<long, bool>   _blocking   = new();
    private double _elapsed;

    // Seeded per ADR-0022. Initialised in _Ready() once GameSession.WorldSeed is set.
    private System.Random _combatRng = null!;

    public override void _Ready()
    {
        Instance = this;
        if (Multiplayer.IsServer())
            _combatRng = new System.Random((int)(GameSession.WorldSeed ^ 0xC0BA7001u));

        var net = NetworkManager.Instance;
        net.PlayerConnected    += OnPlayerConnected;
        net.PlayerDisconnected += OnPlayerDisconnected;
    }

    public override void _Process(double delta)
    {
        if (!Multiplayer.IsServer()) return;
        _elapsed += delta;
    }

    private void OnPlayerConnected(long peerId)
    {
        _swingReady[peerId] = 0.0;
        _blocking[peerId]   = false;
    }

    private void OnPlayerDisconnected(long peerId)
    {
        _swingReady.Remove(peerId);
        _blocking.Remove(peerId);
    }

    // ── Blocking state ────────────────────────────────────────────────────────

    /// <summary>True if the peer is actively blocking with a shield.</summary>
    public bool IsBlocking(long peerId) =>
        _blocking.TryGetValue(peerId, out bool b) && b;

    // ── RPCs ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Client requests a melee swing at a target entity.
    /// Server validates: weapon owned, cooldown clear, target alive, distance ≤ range.
    /// Block gate (combat.md §2.5): if target is blocking with a shield, attack is nullified.
    /// Attack roll: CombatResolver.ResolveAttack — 1d20 + attackBonus vs target's TargetNumber.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestMeleeAttack(long targetEntityId, string weaponId)
    {
        if (!Multiplayer.IsServer()) return;

        long sender = Multiplayer.GetRemoteSenderId();
        if (sender == 0) sender = 1L;

        var weapon = WeaponRegistry.Find(weaponId);
        if (weapon == null || weapon.IsRanged)
        {
            GD.PrintErr($"[Combat] peer {sender}: invalid melee weapon '{weaponId}'");
            return;
        }

        if (!InventorySystem.Instance.HasItems(sender, weaponId, 1))
        {
            GD.Print($"[Combat] peer {sender} has no {weaponId} in inventory");
            return;
        }

        if (_swingReady.TryGetValue(sender, out double readyAt) && _elapsed < readyAt)
        {
            GD.Print($"[Combat] peer {sender} swung too fast (cooldown active)");
            return;
        }

        if (!HealthSystem.Instance.IsAlive(sender))
        {
            GD.Print($"[Combat] dead peer {sender} tried to attack");
            return;
        }

        if (!HealthSystem.Instance.IsAlive(targetEntityId))
        {
            GD.Print($"[Combat] peer {sender} attacked dead/unknown entity {targetEntityId}");
            return;
        }

        var attackerPos = GetEntityPosition(sender);
        var targetPos   = GetEntityPosition(targetEntityId);
        if (!attackerPos.HasValue || !targetPos.HasValue) return;

        float dist = attackerPos.Value.DistanceTo(targetPos.Value);
        if (dist > weapon.Range + RANGE_TOLERANCE)
        {
            GD.Print($"[Combat] peer {sender} out of melee range ({dist:F1} > {weapon.Range + RANGE_TOLERANCE:F1})");
            return;
        }

        // Commit cooldown before any early-returns below — the swing has happened.
        _swingReady[sender] = _elapsed + weapon.SwingCooldown;

        // ── Block gate (combat.md §2.5): blocking with a shield nullifies the attack ──
        if (_blocking.TryGetValue(targetEntityId, out bool isBlocking) && isBlocking
            && InventorySystem.Instance.HasItems(targetEntityId, "item.armor.shield", 1))
        {
            GD.Print($"[Combat] entity {targetEntityId} blocked attack from peer {sender}");
            GetNodeOrNull<Node>(COMBAT_FEEDBACK_PATH)
                ?.Rpc("ShowCombatResult", targetPos.Value, false, -1, false);
            return;
        }

        // ── Attack roll (combat.md §2.2) ──────────────────────────────────────
        int attackBonus  = CombatResolver.PlayerAttackBonus(weaponId);
        int targetNumber = GetEntityTargetNumber(targetEntityId);
        int damageMod    = CombatResolver.PlayerDamageMod(weaponId);

        var (hit, damage, isCrit) = CombatResolver.ResolveAttack(
            attackBonus, targetNumber, weapon.DamageDice, damageMod, _combatRng);

        if (!hit)
        {
            GD.Print($"[Combat] peer {sender} missed entity {targetEntityId} " +
                     $"(roll+{attackBonus} vs TN {targetNumber})");
            GetNodeOrNull<Node>(COMBAT_FEEDBACK_PATH)
                ?.Rpc("ShowCombatResult", targetPos.Value, false, 0, false);
            return;
        }

        HealthSystem.Instance.ApplyDamage(targetEntityId, damage);
        GetNodeOrNull<Node>(COMBAT_FEEDBACK_PATH)
            ?.Rpc("ShowCombatResult", targetPos.Value, true, damage, isCrit);
        GD.Print($"[Combat] peer {sender} hit entity {targetEntityId} with {weaponId} " +
                 $"for {damage}{(isCrit ? " (CRIT)" : "")} (TN {targetNumber}, AB {attackBonus})");
    }

    /// <summary>
    /// Client tells the server it started or stopped blocking.
    /// Ignored if the peer has no shield in inventory.
    /// Uses Unreliable — occasional packet loss is acceptable; next input corrects state.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    public void RequestSetBlocking(bool isBlocking)
    {
        if (!Multiplayer.IsServer()) return;

        long sender = Multiplayer.GetRemoteSenderId();
        if (sender == 0) sender = 1L;

        if (!_blocking.ContainsKey(sender)) return;

        bool hasShield = InventorySystem.Instance.HasItems(sender, "item.armor.shield", 1);
        _blocking[sender] = isBlocking && hasShield;
    }

    // ── Crafting ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Client requests arrow crafting at a Workbench.
    /// Cost: 3 wood → 5 arrows. Server validates inventory before making changes.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestCraftArrows()
    {
        if (!Multiplayer.IsServer()) return;

        long sender = Multiplayer.GetRemoteSenderId();
        if (sender == 0) sender = 1L;

        if (!InventorySystem.Instance.HasItems(sender, "resource.wood", 3))
        {
            GD.Print($"[Combat] peer {sender}: not enough wood to craft arrows (need 3)");
            if (sender == 1)
                ClientNotifyCraftRejection("resource.wood");
            else
                RpcId(sender, MethodName.ClientNotifyCraftRejection, "resource.wood");
            return;
        }

        InventorySystem.Instance.RemoveItems(sender, "resource.wood", 3);
        InventorySystem.Instance.AddItem(sender, "item.arrow", 5);
        GD.Print($"[Combat] peer {sender} crafted 5 arrows (consumed 3 wood)");
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ClientNotifyCraftRejection(string missingItemId)
    {
        LocalState.NotifyRejection(missingItemId);
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Target Number for attack resolution. Uses the monster's authored TargetNumber
    /// for monster targets, or CombatResolver.PlayerTargetNumber() for player targets.
    /// </summary>
    private static int GetEntityTargetNumber(long entityId)
    {
        var monsterData = MonsterSystem.Instance?.GetMonsterData(entityId);
        if (monsterData != null) return monsterData.TargetNumber;
        return CombatResolver.PlayerTargetNumber();
    }

    private Vector3? GetEntityPosition(long entityId)
    {
        var playerNode = GetNodeOrNull<Node3D>($"{PLAYERS_PATH}/Player_{entityId}");
        if (playerNode != null) return playerNode.GlobalPosition;

        return MonsterSystem.Instance?.GetMonsterPosition(entityId);
    }
}
