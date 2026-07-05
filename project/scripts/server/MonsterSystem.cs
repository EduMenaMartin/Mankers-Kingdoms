using System.Collections.Generic;
using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Server;

/// <summary>
/// Server-authoritative monster AI, spawning, and death handling.
///
/// Spawns a fixed set of monsters at startup (Phase 5 NestSystem replaces this with
/// seeded, nest-driven spawning). Each monster runs a three-state AI each _PhysicsProcess:
///
///   Idle   — circles its spawn point at half speed; aggros on players within AggroRange.
///   Aggro  — moves toward target at full speed; enters Attack when within AttackRange.
///   Attack — deals melee damage or fires via ProjectileSystem; returns to Aggro if
///            target moves out of range, to Idle if target dies/disconnects.
///
/// Positions are authoritative on the server. Clients receive position pushes every
/// BROADCAST_INTERVAL ticks and lerp MonsterNode toward them.
///
/// Death: loot item given directly to nearest alive player (Phase 5 replaces with
/// physical ItemDrop pickup per VERTICAL_SLICE.md §3.4). Node hidden via RPC, then
/// freed after DEATH_HIDE_SEC so clients see the mesh disappear smoothly.
///
/// Node must appear in GameWorld.tscn AFTER HealthSystem, InventorySystem, ProjectileSystem.
/// Requires a `Monsters` Node child of GameWorld (editor task).
/// </summary>
public partial class MonsterSystem : Node
{
    public static MonsterSystem Instance { get; private set; } = null!;

    private const string MONSTERS_PATH         = "/root/GameWorld/Monsters";
    private const string PLAYERS_PATH          = "/root/GameWorld/Players";
    private const string COMBAT_FEEDBACK_PATH  = "/root/GameWorld/CombatFeedbackHUD";

    // Broadcast position to clients every N physics ticks (~10 Hz at 60 Hz physics).
    private const int   BROADCAST_INTERVAL = 6;
    // Idle: radius of the circular wander path around spawn point.
    private const float WANDER_RADIUS      = 5f;
    // Idle: angular speed (rad/s) — completes one circle in ~21 s.
    private const float WANDER_SPEED       = 0.3f;
    // Dead node remains in tree briefly so clients see the death animation.
    private const float DEATH_HIDE_SEC     = 0.5f;
    // How far outside AttackRange before transitioning back to Aggro.
    private const float ATTACK_LEASH       = 0.8f;

    /// <summary>
    /// Fired on the server when a nest monster dies. Parameter = NestData.Id.
    /// NestSystem subscribes to decrement live counts and start the respawn timer.
    /// Static event: NestSystem may not be in the scene tree when this fires during cleanup.
    /// </summary>
    public static event System.Action<int>? NestMonsterDied;

    private enum MonsterAiState { Idle, Aggro, Attack }

    private sealed class MutableMonster
    {
        public long            Id;
        public string          TypeId      = "";
        public int             NestId      = -1;  // -1 = not from a nest
        public string          FactionId   = "";  // e.g. "faction.nest.2"; set at spawn
        public Vector3         Position;
        public Vector3         SpawnPoint;
        public float           WanderAngle;
        public MonsterAiState  State       = MonsterAiState.Idle;
        public long            TargetPeer  = -1L;
        public double          AttackReady;  // elapsed seconds when next attack is allowed
        public float           LastHp;
        public bool            DeathHandled;
        public float           DeathTimer;   // counts down; node freed when <= 0
    }

    // SortedDictionary: ADR-0011 deterministic iteration.
    private readonly SortedDictionary<long, MutableMonster> _monsters     = new();
    private readonly SortedDictionary<long, Node3D>         _monsterNodes = new();
    private readonly List<long>                              _toRemove     = new();

    private long   _nextId  = 10001L; // avoids collision with player peer IDs
    private int    _tick;
    private double _elapsed;

    // Seeded per ADR-0022. Initialised in _Ready() once GameSession.WorldSeed is set.
    private System.Random _monsterRng = null!;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        Instance = this;
        if (Multiplayer.IsServer())
            _monsterRng = new System.Random((int)(GameSession.WorldSeed ^ 0xD1CE5EEDu));

        NetworkManager.Instance.PlayerConnected    += OnPlayerConnected;
        NetworkManager.Instance.PlayerDisconnected += OnPlayerDisconnected;
        // NestSystem drives all monster spawning in Phase 5.
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Multiplayer.IsServer()) return;

        _elapsed += delta;
        _tick++;
        float dt = (float)delta;

        foreach (var (id, m) in _monsters)
        {
            // ── Death cleanup ─────────────────────────────────────────────────
            if (m.DeathHandled)
            {
                m.DeathTimer -= dt;
                if (m.DeathTimer <= 0f) _toRemove.Add(id);
                continue;
            }

            if (!HealthSystem.Instance.IsAlive(id))
            {
                HandleDeath(m);
                continue;
            }

            // ── Hit flash detection ───────────────────────────────────────────
            var hp = HealthSystem.Instance.GetHealth(id)?.CurrentHp ?? m.LastHp;
            if (hp < m.LastHp)
            {
                m.LastHp = hp;
                Rpc(MethodName.ClientMonsterHit, id);
            }

            var data = MonsterRegistry.Find(m.TypeId)!;

            // ── AI state machine ─────────────────────────────────────────────
            switch (m.State)
            {
                case MonsterAiState.Idle:
                    TickIdle(m, data, dt);
                    break;
                case MonsterAiState.Aggro:
                    TickAggro(m, data, dt);
                    break;
                case MonsterAiState.Attack:
                    TickAttack(m, data, dt);
                    break;
            }

            // ── Snap to terrain surface ───────────────────────────────────────
            m.Position = new Vector3(
                m.Position.X,
                TerrainSystem.GetHeightAtWorld(m.Position.X, m.Position.Z) + 0.6f,
                m.Position.Z);

            // ── Sync node position on server ──────────────────────────────────
            if (_monsterNodes.TryGetValue(id, out var node))
                node.GlobalPosition = m.Position;

            // ── Broadcast to clients ──────────────────────────────────────────
            if (_tick % BROADCAST_INTERVAL == 0)
                Rpc(MethodName.ClientUpdateMonsterPosition, id, m.Position);
        }

        // Free fully-dead monsters.
        foreach (var id in _toRemove)
        {
            if (_monsterNodes.TryGetValue(id, out var deadNode))
            {
                deadNode.QueueFree();
                _monsterNodes.Remove(id);
            }
            _monsters.Remove(id);
        }
        _toRemove.Clear();
    }

    // ── AI ticks ──────────────────────────────────────────────────────────────

    private void TickIdle(MutableMonster m, MonsterData data, float dt)
    {
        // Circle the spawn point slowly.
        m.WanderAngle += WANDER_SPEED * dt;
        var wx = m.SpawnPoint.X + Mathf.Cos(m.WanderAngle) * WANDER_RADIUS;
        var wz = m.SpawnPoint.Z + Mathf.Sin(m.WanderAngle) * WANDER_RADIUS;
        MoveToward(m, new Vector3(wx, m.Position.Y, wz), data.MoveSpeed * 0.5f, dt);

        // Check for nearby players — only aggro if the player faction is Hostile to this monster.
        // Satisfies docs/gdd/factions.md §6: faction check is the first gate before AI targeting.
        if (FactionService.IsHostile(m.FactionId, FactionService.PLAYER_FACTION_ID))
        {
            var target = FindNearestPlayerInRange(m.Position, data.AggroRange);
            if (target.HasValue)
            {
                m.State      = MonsterAiState.Aggro;
                m.TargetPeer = target.Value.peerId;
                GD.Print($"[Monster] {m.TypeId} {m.Id} aggros on peer {m.TargetPeer}");
            }
        }
    }

    private void TickAggro(MutableMonster m, MonsterData data, float dt)
    {
        if (!HealthSystem.Instance.IsAlive(m.TargetPeer))
        {
            ReturnToIdle(m);
            return;
        }

        var targetPos = GetPlayerPosition(m.TargetPeer);
        if (!targetPos.HasValue)
        {
            ReturnToIdle(m);
            return;
        }

        float dist = m.Position.DistanceTo(targetPos.Value);

        // Re-aggro check: if target ran very far away, disengage.
        if (dist > data.AggroRange * 1.5f)
        {
            ReturnToIdle(m);
            return;
        }

        if (dist <= data.AttackRange)
        {
            m.State = MonsterAiState.Attack;
            return;
        }

        MoveToward(m, targetPos.Value, data.MoveSpeed, dt);
    }

    private void TickAttack(MutableMonster m, MonsterData data, float dt)
    {
        if (!HealthSystem.Instance.IsAlive(m.TargetPeer))
        {
            ReturnToIdle(m);
            return;
        }

        var targetPos = GetPlayerPosition(m.TargetPeer);
        if (!targetPos.HasValue)
        {
            ReturnToIdle(m);
            return;
        }

        float dist = m.Position.DistanceTo(targetPos.Value);

        // Chase if target stepped back outside attack range.
        if (dist > data.AttackRange + ATTACK_LEASH)
        {
            m.State = MonsterAiState.Aggro;
            return;
        }

        // Attack on cooldown.
        if (_elapsed < m.AttackReady) return;
        m.AttackReady = _elapsed + data.AttackCooldown;

        if (data.IsRanged && data.RangedWeaponId != null)
        {
            // Fire an arrow toward the target via ProjectileSystem.
            // Damage is rolled by ProjectileSystem on physical hit (trajectory IS the attack roll).
            var dir = (targetPos.Value - m.Position).Normalized();
            // Slight upward arc so the projectile doesn't clip into terrain.
            dir = new Vector3(dir.X, dir.Y + 0.05f, dir.Z).Normalized();
            ProjectileSystem.Instance.FireFromMonster(m.Id, data.RangedWeaponId,
                m.Position + Vector3.Up, dir);
            GD.Print($"[Monster] {m.TypeId} {m.Id} fires at peer {m.TargetPeer}");
        }
        else
        {
            // Block gate (combat.md §2.5): if the target is blocking with a shield, nullify.
            if (CombatSystem.Instance?.IsBlocking(m.TargetPeer) == true)
            {
                GD.Print($"[Monster] {m.TypeId} {m.Id} attack blocked by peer {m.TargetPeer}");
                GetNodeOrNull<Node>(COMBAT_FEEDBACK_PATH)
                    ?.Rpc("ShowCombatResult", targetPos.Value, false, -1, false);
                return;
            }

            // Melee attack roll (combat.md §2.2): 1d20 + AttackBonus vs player's TargetNumber.
            int targetNumber = CombatSystem.Instance?.GetPlayerTargetNumber(m.TargetPeer)
                               ?? CombatResolver.PlayerTargetNumber(12);
            var (hit, damage, isCrit) = CombatResolver.ResolveAttack(
                data.AttackBonus, targetNumber, data.DamageDice, damageMod: 0, _monsterRng);

            if (!hit)
            {
                GD.Print($"[Monster] {m.TypeId} {m.Id} missed peer {m.TargetPeer} " +
                         $"(AB {data.AttackBonus} vs TN {targetNumber})");
                GetNodeOrNull<Node>(COMBAT_FEEDBACK_PATH)
                    ?.Rpc("ShowCombatResult", targetPos.Value, false, 0, false);
                return;
            }

            HealthSystem.Instance.ApplyDamage(m.TargetPeer, damage);
            GetNodeOrNull<Node>(COMBAT_FEEDBACK_PATH)
                ?.Rpc("ShowCombatResult", targetPos.Value, true, damage, isCrit);
            GD.Print($"[Monster] {m.TypeId} {m.Id} melee hits peer {m.TargetPeer} " +
                     $"for {damage}{(isCrit ? " (CRIT)" : "")} (AB {data.AttackBonus} vs TN {targetNumber})");
        }
    }

    // ── Spawning ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Spawns a monster of the given type at world position (x, z).
    /// Y is sampled from TerrainSystem. Returns the new monster's unique ID.
    /// nestId = -1 for monsters not associated with a nest.
    /// Called by NestSystem.
    /// </summary>
    public long SpawnMonster(string typeId, float x, float z, int nestId = -1,
                             string factionId = "")
    {
        var data = MonsterRegistry.Find(typeId);
        if (data == null)
        {
            GD.PrintErr($"[Monster] unknown typeId '{typeId}'");
            return -1L;
        }

        float y   = TerrainSystem.GetHeightAtWorld(x, z) + 0.6f;
        var   pos = new Vector3(x, y, z);
        long  id  = _nextId++;

        _monsters[id] = new MutableMonster
        {
            Id          = id,
            TypeId      = typeId,
            NestId      = nestId,
            FactionId   = factionId,
            Position    = pos,
            SpawnPoint  = pos,
            WanderAngle = id * 0.7f, // stagger start angles so the pack doesn't move as one
            LastHp      = data.MaxHp,
        };

        HealthSystem.Instance.RegisterEntity(id, data.MaxHp);

        // Spawn node on all peers (CallLocal = true so server creates its own node too).
        Rpc(MethodName.ClientSpawnMonster, id, typeId, pos);
        GD.Print($"[Monster] spawned {typeId} {id} (nest {nestId}) at {pos}");
        return id;
    }

    // ── Death ─────────────────────────────────────────────────────────────────

    private void HandleDeath(MutableMonster m)
    {
        m.DeathHandled = true;
        m.DeathTimer   = DEATH_HIDE_SEC;

        GD.Print($"[Monster] {m.TypeId} {m.Id} died");

        // Spawn a physical item drop at the monster's position (Phase 5).
        var data = MonsterRegistry.Find(m.TypeId);
        if (data != null && data.LootTable.Length > 0)
        {
            var loot = new System.Collections.Generic.Dictionary<string, int>();
            foreach (var itemId in data.LootTable)
            {
                loot.TryGetValue(itemId, out int cnt);
                loot[itemId] = cnt + 1;
            }
            HealthSystem.Instance.SpawnItemDrop(m.Position, loot);
        }

        HealthSystem.Instance.UnregisterEntity(m.Id);
        Rpc(MethodName.ClientMonsterDied, m.Id);

        // Notify NestSystem so it can track live count and start respawn timer.
        if (m.NestId >= 0)
            NestMonsterDied?.Invoke(m.NestId);
    }

    // ── Public server API ─────────────────────────────────────────────────────

    /// <summary>Returns the authoritative position of a live monster, or null if unknown.</summary>
    public Vector3? GetMonsterPosition(long id) =>
        _monsters.TryGetValue(id, out var m) && !m.DeathHandled ? m.Position : null;

    /// <summary>
    /// Returns the MonsterData for a live monster by its runtime ID.
    /// Used by CombatSystem to look up TargetNumber for attack resolution.
    /// Returns null if the entity ID is not a known live monster.
    /// </summary>
    public MonsterData? GetMonsterData(long id)
    {
        if (!_monsters.TryGetValue(id, out var m) || m.DeathHandled) return null;
        return MonsterRegistry.Find(m.TypeId);
    }

    /// <summary>
    /// Returns the faction ID of a live monster (e.g. "faction.nest.2").
    /// Used by ProjectileSystem to resolve faction relationships for hit gating.
    /// Returns null if the entity ID is not a known live monster.
    /// </summary>
    public string? GetMonsterFactionId(long id) =>
        _monsters.TryGetValue(id, out var m) && !m.DeathHandled ? m.FactionId : null;

    // ── Player lifecycle ──────────────────────────────────────────────────────

    private void OnPlayerConnected(long peerId)
    {
        if (!Multiplayer.IsServer()) return;
        // Send current monster roster to the new peer.
        foreach (var (id, m) in _monsters)
        {
            if (!m.DeathHandled)
                RpcId(peerId, MethodName.ClientSpawnMonster, id, m.TypeId, m.Position);
        }
    }

    private void OnPlayerDisconnected(long peerId)
    {
        foreach (var m in _monsters.Values)
        {
            if (m.TargetPeer == peerId)
                ReturnToIdle(m);
        }
    }

    // ── RPCs (server → all clients) ───────────────────────────────────────────

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ClientSpawnMonster(long id, string typeId, Vector3 position)
    {
        if (GetNodeOrNull($"{MONSTERS_PATH}/Monster_{id}") != null) return; // idempotent

        var scene = GD.Load<PackedScene>("res://scenes/Monster.tscn");
        var node  = scene.Instantiate<Node3D>();
        node.Name = $"Monster_{id}";
        node.SetMeta("type_id", typeId);
        GetNode(MONSTERS_PATH).AddChild(node);
        node.GlobalPosition = position;

        if (Multiplayer.IsServer())
            _monsterNodes[id] = node;
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void ClientUpdateMonsterPosition(long id, Vector3 pos)
    {
        var node = GetNodeOrNull($"{MONSTERS_PATH}/Monster_{id}");
        node?.Call("SetTarget", pos);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ClientMonsterHit(long id)
    {
        var node = GetNodeOrNull($"{MONSTERS_PATH}/Monster_{id}");
        node?.Call("TriggerHitFlash");
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ClientMonsterDied(long id)
    {
        var node = GetNodeOrNull($"{MONSTERS_PATH}/Monster_{id}");
        node?.Call("HandleDeath");
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    private static void MoveToward(MutableMonster m, Vector3 target, float speed, float dt)
    {
        var diff = target - m.Position;
        diff.Y = 0f; // ignore vertical difference — monsters move on the XZ plane
        if (diff.LengthSquared() < 0.01f) return;
        var step = diff.Normalized() * speed * dt;
        if (step.LengthSquared() >= diff.LengthSquared())
            m.Position = new Vector3(target.X, m.Position.Y, target.Z);
        else
            m.Position += new Vector3(step.X, 0f, step.Z);
    }

    private static void ReturnToIdle(MutableMonster m)
    {
        m.State      = MonsterAiState.Idle;
        m.TargetPeer = -1L;
    }

    private (long peerId, Vector3 pos)? FindNearestPlayerInRange(Vector3 from, float range)
    {
        var playersNode = GetNodeOrNull(PLAYERS_PATH);
        if (playersNode == null) return null;

        long? nearest    = null;
        Vector3 nearestPos = default;
        float nearestDist = float.MaxValue;

        foreach (var child in playersNode.GetChildren())
        {
            var name = child.Name.ToString();
            if (!name.StartsWith("Player_")) continue;
            if (!long.TryParse(name["Player_".Length..], out var peerId)) continue;
            if (!HealthSystem.Instance.IsAlive(peerId)) continue;

            if (child is not Node3D node3d) continue;
            float dist = from.DistanceTo(node3d.GlobalPosition);
            if (dist < range && dist < nearestDist)
            {
                nearestDist = dist;
                nearest     = peerId;
                nearestPos  = node3d.GlobalPosition;
            }
        }

        return nearest.HasValue ? (nearest.Value, nearestPos) : null;
    }

    private long? FindNearestAlivePeer(Vector3 from)
    {
        var result = FindNearestPlayerInRange(from, float.MaxValue);
        return result?.peerId;
    }

    private Vector3? GetPlayerPosition(long peerId)
    {
        var node = GetNodeOrNull<Node3D>($"{PLAYERS_PATH}/Player_{peerId}");
        return node?.GlobalPosition;
    }
}
