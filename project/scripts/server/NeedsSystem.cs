using System.Collections.Generic;
using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Server;

/// <summary>
/// Server-authoritative hunger and rest simulation.
///
/// Drain rates:
///   Hunger: 100 / 300 s  → fully empty in 5 minutes.
///   Rest:   100 / 600 s  → fully empty in 10 minutes.
///
/// Snapshots are pushed to each peer every SYNC_INTERVAL seconds.
///
/// HUNGER at 0 (VERTICAL_SLICE.md §3.9 — "reaches 0 → gradual health loss"):
///   HP drains at 2 HP per 60 s via HealthSystem.ApplyDamage. When HP reaches 0,
///   HealthSystem.KillPlayer handles death — the single authoritative path for all
///   player deaths (item drop, death marker, configurable penalty, buff clear, respawn).
///   The old NeedsSystem-local kill path has been removed.
///
/// REST at 0 — three-phase escalation (differentiated FEEL from hunger):
///   Phase 1 (0 s):   MoveSpeed ×0.5 debuff, re-applied every 5 s (8 s duration window).
///                    Client notified via BuffSystem.ApplyMoveSpeedWithClientSync.
///   Phase 2 (60 s):  AttackBonus −2 additive debuff (same cadence). Stumble pulse fires
///                    every 60 s of continuous restAtZero time: a 3-second MoveSpeed=0 burst —
///                    brief movement freeze approximating forced sleep. No input lock, no
///                    sleep animation — purely a periodic movement-zero notification.
///   Phase 3 (300 s): HP drain at 1 HP per 60 s, same routing as hunger.
///
/// On any rest recovery (sleep or death reset): all exhaustion buffs are cleared immediately
/// and timers reset, so the player returns to normal without waiting for debuff windows to expire.
///
/// Node must appear in GameWorld.tscn AFTER InventorySystem, SettlementSystem,
/// BuffSystem, and HealthSystem.
/// </summary>
public partial class NeedsSystem : Node
{
    public static NeedsSystem Instance { get; private set; } = null!;

    // ── Drain rates ───────────────────────────────────────────────────────────

    private const float HUNGER_DRAIN  = 100f / 300f; // empties in 5 minutes
    private const float REST_DRAIN    = 100f / 600f; // empties in 10 minutes
    private const float SYNC_INTERVAL = 2f;

    // ── HP drain while need stays at 0 ───────────────────────────────────────

    /// <summary>2 HP per 60 s while Hunger = 0. Applied per-frame as (rate × delta).</summary>
    private const float HUNGER_HP_DRAIN = 2f / 60f;
    /// <summary>1 HP per 60 s while Rest = 0 AND in Phase 3 (≥ 300 s exhausted).</summary>
    private const float REST_HP_DRAIN   = 1f / 60f;

    // ── Rest exhaustion escalation thresholds (seconds at rest = 0) ──────────

    private const double REST_PHASE2_SEC = 60.0;  // movement penalty → attack penalty + stumble
    private const double REST_PHASE3_SEC = 300.0; // HP drain begins

    // ── Exhaustion debuff parameters ──────────────────────────────────────────

    private const float  EXHAUSTION_SPEED_MULT     = 0.5f;   // Phase 1+: half speed
    private const float  EXHAUSTION_AB_AMOUNT      = -2f;    // Phase 2+: AB penalty
    private const double DEBUFF_DURATION_SEC        = 8.0;   // each re-application lasts 8 s
    private const float  DEBUFF_REAPPLY_INTERVAL    = 5f;    // re-applied every 5 s

    // Stumble pulse (Phase 2): MoveSpeed=0 for 3 s, no input lock, no animation.
    private const double STUMBLE_INTERVAL_SEC  = 60.0;
    private const double STUMBLE_DURATION_SEC  = 3.0;

    // ── Server-only state. SortedDictionary: ADR-0011 deterministic iteration ─

    private readonly SortedDictionary<long, float>  _hunger        = new();
    private readonly SortedDictionary<long, float>  _rest          = new();

    // Seconds each peer has been continuously at rest = 0 (resets on any recovery).
    private readonly SortedDictionary<long, double> _restAtZeroSec = new();

    // Value of _restAtZeroSec[peerId] when the last stumble was fired.
    // Tracks stumble interval in exhaustion-time rather than wall-clock time,
    // so brief recoveries (sleep) reset the stumble clock correctly.
    private readonly SortedDictionary<long, double> _lastStumbleAt = new();

    private float _syncTimer;
    private float _debuffTimer;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        Instance = this;
        var net = NetworkManager.Instance;
        net.PlayerConnected    += OnPlayerConnected;
        net.PlayerDisconnected += OnPlayerDisconnected;
    }

    private void OnPlayerConnected(long peerId)
    {
        _hunger[peerId]        = 100f;
        _rest[peerId]          = 100f;
        _restAtZeroSec[peerId] = 0.0;
        GD.Print($"[Needs] peer {peerId} registered (hunger=100, rest=100)");
    }

    private void OnPlayerDisconnected(long peerId)
    {
        _hunger.Remove(peerId);
        _rest.Remove(peerId);
        _restAtZeroSec.Remove(peerId);
        _lastStumbleAt.Remove(peerId);
    }

    // ── Simulation ────────────────────────────────────────────────────────────

    public override void _Process(double delta)
    {
        if (!Multiplayer.IsServer()) return;

        float dt    = (float)delta;
        var   peers = new List<long>(_hunger.Keys);

        // ── Per-peer needs drain and HP consequences ────────────────────────────
        foreach (var peerId in peers)
        {
            _hunger[peerId] = Mathf.Max(0f, _hunger[peerId] - HUNGER_DRAIN * dt);
            _rest[peerId]   = Mathf.Max(0f, _rest[peerId]   - REST_DRAIN   * dt);

            // Hunger at 0: gradual HP drain (VERTICAL_SLICE.md §3.9).
            // HealthSystem.KillPlayer is the sole death handler — no local kill path.
            if (_hunger[peerId] <= 0f)
                HealthSystem.Instance?.ApplyDamage(peerId, HUNGER_HP_DRAIN * dt);

            // Rest exhaustion: track duration and apply phase effects.
            if (_rest[peerId] <= 0f)
            {
                _restAtZeroSec[peerId] += delta;
                TickRestPhases(peerId, dt);
            }
            else if (_restAtZeroSec[peerId] > 0.0)
            {
                // Rest just recovered above 0 — end exhaustion episode immediately.
                ClearExhaustionState(peerId);
            }
        }

        // ── Exhaustion debuff re-application (shared 5-second cadence) ─────────
        // Short-window buffs (8 s) are re-applied every 5 s so they remain active
        // without requiring a permanent duration. Expire naturally when we stop
        // re-applying (i.e. when rest recovers and ClearExhaustionState fires).
        _debuffTimer += dt;
        if (_debuffTimer >= DEBUFF_REAPPLY_INTERVAL)
        {
            _debuffTimer = 0f;
            foreach (var peerId in peers)
            {
                if (_rest[peerId] > 0f) continue;

                // Phase 1+: movement penalty.
                BuffSystem.Instance?.ApplyMoveSpeedWithClientSync(
                    peerId, EXHAUSTION_SPEED_MULT, DEBUFF_DURATION_SEC);

                // Phase 2+: attack penalty.
                if (_restAtZeroSec[peerId] >= REST_PHASE2_SEC)
                    BuffSystem.Instance?.AddBuff(
                        peerId, BuffStat.AttackBonus, EXHAUSTION_AB_AMOUNT,
                        BuffAmountType.Additive, DEBUFF_DURATION_SEC);
            }
        }

        // ── 2-second sync pulse ─────────────────────────────────────────────────
        _syncTimer += dt;
        if (_syncTimer >= SYNC_INTERVAL)
        {
            _syncTimer = 0f;
            SyncAll();
        }
    }

    /// <summary>
    /// Phase 2 stumble pulse and Phase 3 HP drain.
    /// Called per-frame only while rest = 0.
    /// </summary>
    private void TickRestPhases(long peerId, float dt)
    {
        double restAtZero = _restAtZeroSec[peerId];

        // Phase 3 (300 s): HP drain via HealthSystem.
        if (restAtZero >= REST_PHASE3_SEC)
            HealthSystem.Instance?.ApplyDamage(peerId, REST_HP_DRAIN * dt);

        // Phase 2 (60 s): stumble pulse every STUMBLE_INTERVAL_SEC of exhaustion time.
        if (restAtZero < REST_PHASE2_SEC) return;

        // First stumble fires STUMBLE_INTERVAL_SEC after Phase 2 starts (at restAtZero=120),
        // then every 60 s after that. Tracked in exhaustion-time so recovery resets it.
        double lastStumble = _lastStumbleAt.TryGetValue(peerId, out double ls)
            ? ls
            : REST_PHASE2_SEC; // sentinel: first interval counts from phase 2 start

        if (restAtZero >= lastStumble + STUMBLE_INTERVAL_SEC)
        {
            _lastStumbleAt[peerId] = restAtZero;
            // Brief MoveSpeed=0 burst — movement freeze only, no input lock, no animation.
            BuffSystem.Instance?.ApplyMoveSpeedWithClientSync(peerId, 0f, STUMBLE_DURATION_SEC);
            GD.Print($"[Needs] peer {peerId} stumble pulse — {restAtZero:F0}s at rest=0");
        }
    }

    /// <summary>
    /// Clears all rest-exhaustion buffs and resets timers for a peer.
    /// Called when rest recovers above 0 (sleep or death reset).
    /// </summary>
    private void ClearExhaustionState(long peerId)
    {
        _restAtZeroSec[peerId] = 0.0;
        _lastStumbleAt.Remove(peerId);
        // RemoveBuffs clears all active buffs of these stats including any from combat.
        // Overlap is rare (you can't sleep mid-combat) and the reset is intentional.
        BuffSystem.Instance?.RemoveBuffs(peerId, BuffStat.MoveSpeed);
        BuffSystem.Instance?.RemoveBuffs(peerId, BuffStat.AttackBonus);
        GD.Print($"[Needs] peer {peerId} exhaustion cleared — rest recovered");
    }

    // ── Server API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by HealthSystem on any player death — resets both needs to full
    /// and clears exhaustion state. HealthSystem.KillPlayer already called
    /// BuffSystem.ClearAllBuffs before this, so buff removal is redundant but safe.
    /// </summary>
    public void ResetNeeds(long peerId)
    {
        if (!_hunger.ContainsKey(peerId)) return;
        _hunger[peerId]        = 100f;
        _rest[peerId]          = 100f;
        _restAtZeroSec[peerId] = 0.0;
        _lastStumbleAt.Remove(peerId);

        if (peerId == 1)
            ApplyNeeds(100f, 100f);
        else
            RpcId(peerId, MethodName.ApplyNeeds, 100f, 100f);
    }

    /// <summary>Called by BushSystem when a player eats food.</summary>
    public void RestoreHunger(long peerId, float amount)
    {
        if (!_hunger.ContainsKey(peerId)) return;
        _hunger[peerId] = Mathf.Min(100f, _hunger[peerId] + amount);

        if (peerId == 1)
            ApplyNeeds(_hunger[peerId], _rest[peerId]);
        else
            RpcId(peerId, MethodName.ApplyNeeds, _hunger[peerId], _rest[peerId]);
    }

    // ── Save / Load (M8) ─────────────────────────────────────────────────────

    /// <summary>Returns the current hunger and rest values for a peer.</summary>
    public (float hunger, float rest) GetNeeds(long peerId)
    {
        float h = _hunger.TryGetValue(peerId, out var hv) ? hv : 100f;
        float r = _rest.TryGetValue(peerId, out var rv) ? rv : 100f;
        return (h, r);
    }

    /// <summary>
    /// Overwrites a peer's needs from save data and syncs to the client.
    /// Resets exhaustion state — loaded peers are never mid-exhaustion-episode.
    /// Called by SaveSystem.TryLoad().
    /// </summary>
    public void RestoreNeedsFromSave(long peerId, float hunger, float rest)
    {
        if (!_hunger.ContainsKey(peerId)) return;
        _hunger[peerId]        = Mathf.Clamp(hunger, 1f, 100f);
        _rest[peerId]          = Mathf.Clamp(rest,   0f, 100f);
        _restAtZeroSec[peerId] = 0.0;
        _lastStumbleAt.Remove(peerId);

        if (peerId == 1)
            ApplyNeeds(_hunger[peerId], _rest[peerId]);
        else
            RpcId(peerId, MethodName.ApplyNeeds, _hunger[peerId], _rest[peerId]);

        GD.Print($"[Needs] peer {peerId} needs restored (hunger={hunger:F1}, rest={rest:F1})");
    }

    // ── RPCs ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by PlayerController when the player presses E near a Shelter.
    /// Restores rest to 100 and immediately clears all exhaustion state.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestSleep()
    {
        if (!Multiplayer.IsServer()) return;

        long sender = Multiplayer.GetRemoteSenderId();
        if (sender == 0) sender = 1L; // host/solo direct call

        if (!_rest.ContainsKey(sender)) return;

        _rest[sender] = 100f;
        ClearExhaustionState(sender);
        GD.Print($"[Needs] peer {sender} slept — rest restored, exhaustion cleared");

        if (sender == 1)
            ApplyNeeds(_hunger[sender], 100f);
        else
            RpcId(sender, MethodName.ApplyNeeds, _hunger[sender], 100f);
    }

    private void SyncAll()
    {
        var peers = new List<long>(_hunger.Keys);
        foreach (var peerId in peers)
        {
            float h = _hunger[peerId];
            float r = _rest[peerId];

            if (peerId == 1)
                ApplyNeeds(h, r);
            else
                RpcId(peerId, MethodName.ApplyNeeds, h, r);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ApplyNeeds(float hunger, float rest)
    {
        LocalState.SetNeeds(hunger, rest);
    }
}
