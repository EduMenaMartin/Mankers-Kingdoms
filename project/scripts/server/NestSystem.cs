using System.Collections.Generic;
using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Server;

/// <summary>
/// Server-authoritative nest manager. Generates 5 nests deterministically from the world seed:
///   2× wolf pack  (2 wolves each,       respawn 45 s)
///   2× goblin group (3 goblins each,    respawn 60 s)
///   1× bandit camp (2 bandits + archer,  respawn 90 s)
///
/// Placement uses WorldSeed XOR 0xDEAD1234 salt — independent of tree/bush scatter.
/// Nests are placed ≥ 15 world-units from the origin and ≥ 25 units apart.
///
/// When all monsters in a nest die the respawn timer starts. After the delay the
/// full wave respawns at the same nest centre ± 3 world-unit scatter.
///
/// Node must appear in GameWorld.tscn AFTER MonsterSystem.
/// </summary>
public partial class NestSystem : Node
{
    public static NestSystem Instance { get; private set; } = null!;

    private const float SPAWN_SCATTER = 3f; // per-monster scatter half-width around nest centre

    // SortedDictionary: ADR-0011 deterministic iteration.
    private readonly List<NestData>                _nests        = new();
    private readonly SortedDictionary<int, int>    _liveCount    = new();
    private readonly SortedDictionary<int, double> _respawnTimer = new();

    private double _elapsed;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        Instance = this;
        if (!Multiplayer.IsServer()) return;

        // Register the player faction once at world start (all players share one faction in v1).
        FactionService.RegisterFaction(FactionService.PLAYER_FACTION_ID, FactionType.PlayerSettlement);

        // Generate nests, assign a unique faction ID to each, and register them.
        var rawNests = NestGenerator.Generate(GameSession.WorldSeed);
        foreach (var nest in rawNests)
        {
            var factionId = $"faction.nest.{nest.Id}";
            FactionService.RegisterFaction(factionId, FactionType.MonsterNest);
            _nests.Add(nest with { FactionId = factionId });
        }

        MonsterSystem.NestMonsterDied += OnNestMonsterDied;

        foreach (var nest in _nests)
            SpawnNest(nest);
    }

    public override void _ExitTree()
    {
        MonsterSystem.NestMonsterDied -= OnNestMonsterDied;
    }

    public override void _Process(double delta)
    {
        if (!Multiplayer.IsServer()) return;
        _elapsed += delta;

        // Collect nests whose respawn timer has elapsed.
        var toRespawn = new List<int>();
        foreach (var (nestId, fireAt) in _respawnTimer)
        {
            if (_elapsed >= fireAt)
                toRespawn.Add(nestId);
        }

        foreach (var nestId in toRespawn)
        {
            _respawnTimer.Remove(nestId);
            var nest = _nests.Find(n => n.Id == nestId);
            if (nest != null) SpawnNest(nest);
        }
    }

    // ── Nest event ────────────────────────────────────────────────────────────

    private void OnNestMonsterDied(int nestId)
    {
        if (!_liveCount.TryGetValue(nestId, out int live)) return;

        live = System.Math.Max(0, live - 1);
        _liveCount[nestId] = live;

        if (live > 0) return;

        var nest = _nests.Find(n => n.Id == nestId);
        if (nest == null) return;

        GD.Print($"[Nest] nest {nestId} cleared — respawning in {nest.RespawnDelaySec}s");
        _respawnTimer[nestId] = _elapsed + nest.RespawnDelaySec;
    }

    // ── Spawning ──────────────────────────────────────────────────────────────

    private void SpawnNest(NestData nest)
    {
        // Each wave uses the same scatter seed so monster positions are reproducible.
        var rng = new System.Random((int)(GameSession.WorldSeed ^ (uint)(0xCAFEBABE + nest.Id)));

        int spawned = 0;
        foreach (var typeId in nest.MonsterTypeIds)
        {
            float ox = (float)(rng.NextDouble() * SPAWN_SCATTER * 2 - SPAWN_SCATTER);
            float oz = (float)(rng.NextDouble() * SPAWN_SCATTER * 2 - SPAWN_SCATTER);
            MonsterSystem.Instance.SpawnMonster(typeId, nest.WorldX + ox, nest.WorldZ + oz,
                nest.Id, nest.FactionId);
            spawned++;
        }

        _liveCount[nest.Id] = spawned;
        GD.Print($"[Nest] nest {nest.Id} ({nest.MonsterTypeIds[0]}) " +
                 $"spawned {spawned} monsters at ({nest.WorldX:F0}, {nest.WorldZ:F0})");
    }

}

