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
/// Hunger reaching 0 triggers death: inventory is dropped (cleared), peer is
/// teleported back to their shelter (Kingdom Marker position) at full hunger.
///
/// RequestSleep RPC: called via PlayerController when the player presses E near a Shelter.
/// Restores rest to 100 instantly.
///
/// Node must appear in GameWorld.tscn AFTER InventorySystem and SettlementSystem.
/// </summary>
public partial class NeedsSystem : Node
{
    public static NeedsSystem Instance { get; private set; } = null!;

    private const float HUNGER_DRAIN  = 100f / 300f; // empties in 5 minutes
    private const float REST_DRAIN    = 100f / 600f; // empties in 10 minutes
    private const float SYNC_INTERVAL = 2f;

    // Server-only. SortedDictionary: ADR-0011 deterministic iteration.
    private readonly SortedDictionary<long, float> _hunger = new();
    private readonly SortedDictionary<long, float> _rest   = new();
    private float _syncTimer;

    private const string PLAYERS_PATH = "/root/GameWorld/Players";

    public override void _Ready()
    {
        Instance = this;
        var net = NetworkManager.Instance;
        net.PlayerConnected    += OnPlayerConnected;
        net.PlayerDisconnected += OnPlayerDisconnected;
    }

    private void OnPlayerConnected(long peerId)
    {
        _hunger[peerId] = 100f;
        _rest[peerId]   = 100f;
        GD.Print($"[Needs] peer {peerId} registered (hunger=100, rest=100)");
    }

    private void OnPlayerDisconnected(long peerId)
    {
        _hunger.Remove(peerId);
        _rest.Remove(peerId);
    }

    public override void _Process(double delta)
    {
        if (!Multiplayer.IsServer()) return;

        float dt = (float)delta;

        // Snapshot keys to avoid modifying collection during iteration.
        var peers = new List<long>(_hunger.Keys);
        var toKill = new List<long>();

        foreach (var peerId in peers)
        {
            _hunger[peerId] = Mathf.Max(0f, _hunger[peerId] - HUNGER_DRAIN * dt);
            _rest[peerId]   = Mathf.Max(0f, _rest[peerId]   - REST_DRAIN   * dt);

            if (_hunger[peerId] <= 0f)
                toKill.Add(peerId);
        }

        foreach (var peerId in toKill)
            KillPlayer(peerId);

        _syncTimer += dt;
        if (_syncTimer >= SYNC_INTERVAL)
        {
            _syncTimer = 0f;
            SyncAll();
        }
    }

    private void KillPlayer(long peerId)
    {
        GD.Print($"[Needs] peer {peerId} starved — clearing inventory and respawning");
        InventorySystem.Instance.TakeAll(peerId);

        _hunger[peerId] = 100f;
        _rest[peerId]   = 100f;

        var respawnPos = SettlementSystem.Instance.GetRespawnPosition(peerId);
        var playerNode = GetNodeOrNull($"{PLAYERS_PATH}/Player_{peerId}");
        if (playerNode == null) return;

        // Host/solo (peerId == 1): Call() bypasses RPC and runs directly on this peer.
        // Remote clients: RpcId sends the ForceRespawn call to that peer only.
        if (peerId == 1)
            playerNode.Call("ForceRespawn", respawnPos);
        else
            playerNode.RpcId(peerId, "ForceRespawn", respawnPos);

        // Immediate sync so the client sees full bars right away.
        if (peerId == 1)
            ApplyNeeds(100f, 100f);
        else
            RpcId(peerId, MethodName.ApplyNeeds, 100f, 100f);
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

    // ── Server API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Called by HealthSystem on combat death — resets both hunger and rest to full
    /// so the player respawns with a clean state, matching starvation-death behaviour.
    /// </summary>
    public void ResetNeeds(long peerId)
    {
        if (!_hunger.ContainsKey(peerId)) return;
        _hunger[peerId] = 100f;
        _rest[peerId]   = 100f;

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
    /// Called by SaveSystem.TryLoad().
    /// </summary>
    public void RestoreNeedsFromSave(long peerId, float hunger, float rest)
    {
        if (!_hunger.ContainsKey(peerId)) return;
        _hunger[peerId] = Mathf.Clamp(hunger, 1f, 100f);
        _rest[peerId]   = Mathf.Clamp(rest,   0f, 100f);

        if (peerId == 1)
            ApplyNeeds(_hunger[peerId], _rest[peerId]);
        else
            RpcId(peerId, MethodName.ApplyNeeds, _hunger[peerId], _rest[peerId]);

        GD.Print($"[Needs] peer {peerId} needs restored (hunger={hunger:F1}, rest={rest:F1})");
    }

    // ── RPCs ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by PlayerController when the player sleeps in a Shelter.
    /// Restores rest to 100 instantly.
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
        GD.Print($"[Needs] peer {sender} slept — rest restored");

        if (sender == 1)
            ApplyNeeds(_hunger[sender], 100f);
        else
            RpcId(sender, MethodName.ApplyNeeds, _hunger[sender], 100f);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ApplyNeeds(float hunger, float rest)
    {
        LocalState.SetNeeds(hunger, rest);
    }
}
