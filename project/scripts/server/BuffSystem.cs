using System.Collections.Generic;
using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Server;

/// <summary>
/// Server-authoritative buff and debuff tracker.
///
/// Manages temporary stat modifiers applied by critical hits (combat.md §5.4) and fumbles.
/// Each effect is an ActiveBuff: (stat, amount, amountType, expiresAt).
///
/// Querying always reads the live buff list through BuffCalculator, which filters expired
/// entries at query time — no background sweep is needed for correctness.
/// Expired entries are swept lazily in _Process once per second to bound memory growth.
///
/// Bleed DoT (BleedDamage stat) is ticked here every BLEED_INTERVAL_SEC via HealthSystem.
///
/// Condensation rules (see BuffCalculator for the correctness guarantee):
///   Additive:       effective = base + Σ(amounts)
///   Multiplicative: effective = base × (1 + Σ(amounts))   ← sum BEFORE multiplying
///
/// Node must appear in GameWorld.tscn AFTER HealthSystem and CombatSystem.
/// </summary>
public partial class BuffSystem : Node
{
    public static BuffSystem Instance { get; private set; } = null!;

    // ── Authored durations and magnitudes (tune during balancing pass) ────────

    // Critical hit effects — applied to the defender.
    private const double STUN_DURATION_SEC      = 0.75;  // PreciseStrike
    private const double BLEED_DURATION_SEC     = 5.0;   // BleedingWound
    private const float  BLEED_DAMAGE_PER_TICK  = 2f;    // BleedingWound (per 1s tick)
    private const double ARMOR_REDUCE_DURATION  = 5.0;   // SunderingHit
    private const float  ARMOR_REDUCE_AMOUNT    = -2f;   // SunderingHit
    private const double SPEED_REDUCE_DURATION  = 1.5;   // StaggeringBlow

    // Critical fumble effects — applied to the attacker.
    private const double ACCURACY_REDUCE_DURATION = 3.0; // OffBalance
    private const float  ACCURACY_REDUCE_AMOUNT   = -2f; // OffBalance
    private const double DISARM_DURATION_SEC      = 1.0; // WeaponSlip
    private const double VULN_DURATION_SEC        = 2.0; // Overextended
    private const float  VULN_AMOUNT              = 0.5f;// Overextended (+50% incoming damage)
    private const double STUMBLE_DURATION_SEC     = 2.0; // Stumble

    // How often bleed ticks and how often the expired-entry sweep runs.
    private const double BLEED_INTERVAL_SEC  = 1.0;
    private const double SWEEP_INTERVAL_SEC  = 1.0;

    // Per-entity buff lists. SortedDictionary: ADR-0011 deterministic iteration.
    private readonly SortedDictionary<long, List<ActiveBuff>> _buffs = new();

    // Last bleed-tick time per entity (absolute elapsed seconds).
    private readonly SortedDictionary<long, double> _lastBleedTick = new();

    private double _elapsed;
    private double _lastSweep;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        Instance = this;
    }

    public override void _Process(double delta)
    {
        if (!Multiplayer.IsServer()) return;
        _elapsed += delta;
        TickBleeds();
        if (_elapsed - _lastSweep >= SWEEP_INTERVAL_SEC)
        {
            SweepExpired();
            _lastSweep = _elapsed;
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds a buff or debuff to an entity.
    /// If the entity has no entry yet, one is created automatically.
    /// Multiple buffs of the same stat stack according to BuffCalculator's condensation rules.
    /// </summary>
    public void AddBuff(long entityId, BuffStat stat, float amount, BuffAmountType amountType, double durationSec)
    {
        if (!_buffs.TryGetValue(entityId, out var list))
        {
            list = new List<ActiveBuff>();
            _buffs[entityId] = list;
        }
        list.Add(new ActiveBuff(stat, amount, amountType, _elapsed + durationSec));

        if (stat == BuffStat.BleedDamage && !_lastBleedTick.ContainsKey(entityId))
            _lastBleedTick[entityId] = _elapsed; // start bleed clock
    }

    /// <summary>
    /// Immediately removes all non-expired buffs of a given stat from an entity.
    /// Used for manual recovery (e.g. a future "cure" item or respawn).
    /// </summary>
    public void RemoveBuffs(long entityId, BuffStat stat)
    {
        if (!_buffs.TryGetValue(entityId, out var list)) return;
        list.RemoveAll(b => b.Stat == stat);
    }

    /// <summary>Clears all buffs for an entity (e.g. on death or respawn).</summary>
    public void ClearAllBuffs(long entityId)
    {
        _buffs.Remove(entityId);
        _lastBleedTick.Remove(entityId);
    }

    // ── Query API (delegated to BuffCalculator) ───────────────────────────────

    /// <summary>
    /// Returns the additive modifier to add to a base stat value.
    /// Example: armorValue + GetAdditiveModifier(peerId, BuffStat.ArmorValue).
    /// Negative values reduce the stat (debuffs).
    /// </summary>
    public float GetAdditiveModifier(long entityId, BuffStat stat)
    {
        if (!_buffs.TryGetValue(entityId, out var list)) return 0f;
        return BuffCalculator.GetAdditiveModifier(list, stat, _elapsed);
    }

    /// <summary>
    /// Returns the combined multiplier to apply to a base value.
    /// Example: damage * GetMultiplicativeModifier(entityId, BuffStat.IncomingDamage).
    /// Returns 1.0 when no multiplicative buffs are active.
    /// </summary>
    public float GetMultiplicativeModifier(long entityId, BuffStat stat)
    {
        if (!_buffs.TryGetValue(entityId, out var list)) return 1f;
        return BuffCalculator.GetMultiplicativeModifier(list, stat, _elapsed);
    }

    /// <summary>
    /// Returns true if the entity has at least one non-expired buff for the given stat.
    /// Use this for boolean gates: Stun (prevents attacking), Disarm (prevents weapon use).
    /// </summary>
    public bool IsBuffActive(long entityId, BuffStat stat)
    {
        if (!_buffs.TryGetValue(entityId, out var list)) return false;
        return BuffCalculator.IsActive(list, stat, _elapsed);
    }

    // ── Crit / fumble effect application ─────────────────────────────────────

    /// <summary>
    /// Applies the buff corresponding to the rolled crit effect on the defender.
    /// Called by CombatSystem and MonsterSystem after a confirmed critical hit.
    /// </summary>
    public void ApplyCritEffect(CritEffect effect, long defenderEntityId)
    {
        switch (effect)
        {
            case CritEffect.DevastatingBlow:
                // No extra buff — double damage is already applied by ResolveAttack.
                break;
            case CritEffect.PreciseStrike:
                AddBuff(defenderEntityId, BuffStat.Stun, 1f, BuffAmountType.Additive, STUN_DURATION_SEC);
                GD.Print($"[Buff] Stun → entity {defenderEntityId} ({STUN_DURATION_SEC:F2}s)");
                break;
            case CritEffect.BleedingWound:
                AddBuff(defenderEntityId, BuffStat.BleedDamage, BLEED_DAMAGE_PER_TICK, BuffAmountType.Additive, BLEED_DURATION_SEC);
                GD.Print($"[Buff] Bleed {BLEED_DAMAGE_PER_TICK}/s → entity {defenderEntityId} ({BLEED_DURATION_SEC:F0}s)");
                break;
            case CritEffect.SunderingHit:
                AddBuff(defenderEntityId, BuffStat.ArmorValue, ARMOR_REDUCE_AMOUNT, BuffAmountType.Additive, ARMOR_REDUCE_DURATION);
                GD.Print($"[Buff] ArmorValue {ARMOR_REDUCE_AMOUNT} → entity {defenderEntityId} ({ARMOR_REDUCE_DURATION:F0}s)");
                break;
            case CritEffect.StaggeringBlow:
                AddBuff(defenderEntityId, BuffStat.MoveSpeed, -0.5f, BuffAmountType.Multiplicative, SPEED_REDUCE_DURATION);
                GD.Print($"[Buff] MoveSpeed ×0.5 → entity {defenderEntityId} ({SPEED_REDUCE_DURATION:F1}s)");
                // Notify the defender's client only if the defender is a player (peer IDs ≤ 10000).
                NotifyMoveSpeedClient(defenderEntityId, 0.5f, (int)(SPEED_REDUCE_DURATION * 1000));
                break;
        }
    }

    /// <summary>
    /// Applies the buff corresponding to the rolled fumble effect on the attacker.
    /// Called by CombatSystem and MonsterSystem after a confirmed critical fumble.
    /// </summary>
    public void ApplyFumbleEffect(FumbleEffect effect, long attackerEntityId)
    {
        switch (effect)
        {
            case FumbleEffect.OffBalance:
                AddBuff(attackerEntityId, BuffStat.AttackBonus, ACCURACY_REDUCE_AMOUNT, BuffAmountType.Additive, ACCURACY_REDUCE_DURATION);
                GD.Print($"[Buff] AttackBonus {ACCURACY_REDUCE_AMOUNT} → entity {attackerEntityId} ({ACCURACY_REDUCE_DURATION:F0}s)");
                break;
            case FumbleEffect.WeaponSlip:
                AddBuff(attackerEntityId, BuffStat.Disarm, 1f, BuffAmountType.Additive, DISARM_DURATION_SEC);
                GD.Print($"[Buff] Disarm → entity {attackerEntityId} ({DISARM_DURATION_SEC:F1}s)");
                break;
            case FumbleEffect.Overextended:
                AddBuff(attackerEntityId, BuffStat.IncomingDamage, VULN_AMOUNT, BuffAmountType.Multiplicative, VULN_DURATION_SEC);
                GD.Print($"[Buff] IncomingDamage ×{1f + VULN_AMOUNT:F1} → entity {attackerEntityId} ({VULN_DURATION_SEC:F0}s)");
                break;
            case FumbleEffect.Stumble:
                AddBuff(attackerEntityId, BuffStat.MoveSpeed, -0.5f, BuffAmountType.Multiplicative, STUMBLE_DURATION_SEC);
                GD.Print($"[Buff] MoveSpeed ×0.5 → entity {attackerEntityId} ({STUMBLE_DURATION_SEC:F0}s)");
                // Stumble is always an attacker fumble — attackerEntityId is always a player peer.
                NotifyMoveSpeedClient(attackerEntityId, 0.5f, (int)(STUMBLE_DURATION_SEC * 1000));
                break;
        }
    }

    // ── MoveSpeed client notification ─────────────────────────────────────────

    /// <summary>
    /// Adds a MoveSpeed debuff and notifies the affected peer's client in one call.
    /// Used by NeedsSystem for sustained exhaustion (multiplier=0.5) and stumble
    /// pulses (multiplier=0, brief freeze). Converts multiplier to buff amount internally:
    /// amount = multiplier − 1 (e.g. 0.5 → −0.5, 0.0 → −1.0).
    ///
    /// Note: multiple concurrent MoveSpeed buffs stack additively on the server side
    /// (BuffCalculator sum). The client receives independent notifications and applies
    /// the last-received multiplier for its timer window. Since movement is client-predicted
    /// and the server does not gate movement via MoveSpeed queries, server-side stacking
    /// does not affect gameplay — only the client notification drives actual movement speed.
    /// </summary>
    public void ApplyMoveSpeedWithClientSync(long entityId, float multiplier, double durationSec)
    {
        AddBuff(entityId, BuffStat.MoveSpeed, multiplier - 1.0f, BuffAmountType.Multiplicative, durationSec);
        NotifyMoveSpeedClient(entityId, multiplier, (int)(durationSec * 1000));
    }

    /// <summary>
    /// Notifies the affected player peer's client of a MoveSpeed debuff.
    /// Skipped for monster entities (entityId > 10000) — they have no client to notify.
    /// multiplier: the combined speed factor (e.g. 0.5 = half speed).
    /// durationMs: buff duration in milliseconds (matches server buff duration).
    /// </summary>
    private void NotifyMoveSpeedClient(long entityId, float multiplier, int durationMs)
    {
        if (entityId > 10000) return; // monster — no client to notify
        if (entityId == 1)
            ClientApplyMoveSpeedBuff(multiplier, durationMs);
        else
            RpcId(entityId, MethodName.ClientApplyMoveSpeedBuff, multiplier, durationMs);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ClientApplyMoveSpeedBuff(float multiplier, int durationMs)
    {
        LocalState.SetMoveSpeedBuff(multiplier, durationMs);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void TickBleeds()
    {
        foreach (var (entityId, list) in _buffs)
        {
            float bleedPerTick = BuffCalculator.GetBleedDamagePerTick(list, _elapsed);
            if (bleedPerTick <= 0f) continue;

            if (!_lastBleedTick.TryGetValue(entityId, out double lastTick)) continue;
            if (_elapsed - lastTick < BLEED_INTERVAL_SEC) continue;

            _lastBleedTick[entityId] = _elapsed;

            if (HealthSystem.Instance != null && HealthSystem.Instance.IsAlive(entityId))
            {
                HealthSystem.Instance.ApplyDamage(entityId, bleedPerTick);
                GD.Print($"[Buff] Bleed tick: {bleedPerTick} dmg → entity {entityId}");
            }
        }
    }

    private void SweepExpired()
    {
        foreach (var (_, list) in _buffs)
            list.RemoveAll(b => b.ExpiresAt <= _elapsed);

        // Clean up bleed clocks for entities that no longer have bleed buffs.
        var toRemove = new List<long>();
        foreach (var (entityId, _) in _lastBleedTick)
        {
            if (!_buffs.TryGetValue(entityId, out var list) ||
                !BuffCalculator.IsActive(list, BuffStat.BleedDamage, _elapsed))
                toRemove.Add(entityId);
        }
        foreach (var id in toRemove)
            _lastBleedTick.Remove(id);
    }
}
