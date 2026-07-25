using System.Collections.Generic;
using System.Text.Json;
using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Server;

/// <summary>
/// Spawns and manages all village NPCs (villagers).
///
/// Both server and client run Generate() in _Ready() with the same world seed —
/// identical output means no position sync RPC is needed for initial spawn.
///
/// NPC states (server-only):
///   Idle     — spawned, waiting to be recruited (not in any state dict)
///   Following — recruited by a player; follows them home (_followTargets)
///   Working   — assigned to a station; runs its job loop (_workAssignments)
///
/// Node must appear in GameWorld.tscn AFTER TerrainSystem and TreeSystem.
/// </summary>
public partial class VillageSystem : Node
{
    public static VillageSystem Instance { get; private set; } = null!;

    // ── Shared data (all peers) ───────────────────────────────────────────────
    private VillageData?                                    _village;
    private readonly SortedDictionary<string, VillagerData> _villagers = new();
    private readonly SortedDictionary<string, Node3D>       _npcNodes  = new();

    // ── Server-only: positions ────────────────────────────────────────────────
    private readonly SortedDictionary<string, Vector3> _positions = new();

    // ── Server-only: Settlement roster ───────────────────────────────────────
    // NPCs that have been recruited into the player's settlement (persist beyond follow state).
    private readonly SortedSet<string>               _settlementNpcs = new(); // villagerId
    private readonly SortedDictionary<string, long>  _npcFounder     = new(); // villagerId → founderPeerId

    // ── Server-only: Following state ──────────────────────────────────────────
    private readonly SortedDictionary<string, long>   _followTargets  = new(); // villagerId → peerId
    private readonly SortedDictionary<long,   string> _followerByPeer = new(); // peerId → villagerId

    // ── Server-only: Working state ────────────────────────────────────────────
    private readonly SortedDictionary<string, string> _workAssignments = new(); // villagerId → stationNodeName
    private readonly SortedDictionary<string, long>   _workFounder     = new(); // villagerId → founderPeerId
    private readonly SortedDictionary<string, string> _jobTargetTree   = new(); // villagerId → target treeId (or "")
    private readonly SortedDictionary<string, double> _lastChopTime    = new(); // villagerId → elapsed time at last chop

    // ── Server-only: Needs state ──────────────────────────────────────────────
    private readonly SortedDictionary<string, float>   _npcHunger          = new(); // 0–100
    private readonly SortedDictionary<string, float>   _npcRest            = new(); // 0–100
    private readonly SortedDictionary<string, Vector3> _walkingToShelter   = new(); // villagerId → shelter pos
    private readonly SortedDictionary<string, double>  _sleeping           = new(); // villagerId → wake time (_elapsed)
    // Remembers what station the NPC was assigned to so it can return after sleeping.
    private readonly SortedDictionary<string, string>  _suspendedStation   = new(); // villagerId → stationNodeName
    private readonly SortedDictionary<string, long>    _suspendedFounder   = new(); // villagerId → founderPeerId

    // ── Server-only: Haul state ───────────────────────────────────────────────
    private readonly SortedDictionary<string, int>    _npcCarried       = new(); // villagerId → wood carried
    private readonly SortedDictionary<string, string> _walkingToDeposit = new(); // villagerId → stockpile node name
    private const int   NPC_CARRY_CAPACITY = 6;  // 2 trees × 3 wood/tree
    private const float DEPOSIT_RANGE      = 2f;

    // ── Server-only: Forager state ────────────────────────────────────────────
    // Movement-based loop: NPC finds nearest herb patch or berry bush, walks to it,
    // harvests, carries items back to stockpile when at capacity.
    private readonly SortedDictionary<string, string> _foragerTarget        = new(); // villagerId → target node name
    private readonly SortedDictionary<string, int>    _foragerCarriedHerbs  = new(); // villagerId → herbs carried
    private readonly SortedDictionary<string, int>    _foragerCarriedBerries= new(); // villagerId → berries carried
    private readonly SortedDictionary<string, string> _foragerWalkToDeposit = new(); // villagerId → stockpile node name

    private const int   FORAGER_CARRY_CAPACITY  = 6;
    private const float FORAGE_RANGE            = 1.5f;
    private const float MAX_FORAGE_SEARCH_RANGE = 200f;

    // ── Warning throttle ──────────────────────────────────────────────────────
    private readonly SortedDictionary<string, double> _lastWarnTime = new(); // villagerId → _elapsed at last warn
    private const float WARN_THROTTLE_SEC = 10f;

    private float _needsTimer;
    private const float NEEDS_UPDATE_INTERVAL  = 1f;
    private const float HUNGER_DRAIN_PER_SEC   = 0.5f / 60f;
    private const float REST_DRAIN_PER_SEC     = 1.0f / 60f;
    private const float HUNGER_RESTORE_PER_SEC = 0.5f;
    private const float REST_LOW_THRESHOLD     = 20f;
    private const float SLEEP_DURATION_SEC     = 30f;
    private const float SLEEP_ARRIVE_RANGE     = 2f;

    private PackedScene _villagerScene = null!;
    private int   _tick;
    private double _elapsed; // seconds since _Ready, used for cooldown comparison

    private const float  FOLLOW_SPEED          = 3f;
    private const float  FOLLOW_STOP_XZ        = 4f;
    private const float  CHOP_RANGE            = 1.5f;  // distance to tree that triggers a chop
    private const float  MAX_CHOP_RANGE        = 200f;  // NPC searches for trees within this radius
    private const float  CHOP_COOLDOWN         = 1.0f;  // seconds between chops
    private const int    BROADCAST_EVERY       = 3;     // physics ticks between position broadcasts
    private const string PLAYERS_PATH          = "/root/GameWorld/Players";
    private const int    MAX_NPCS_PER_SHELTER  = 2;     // recruitment gate: each Shelter holds max 2 settlers

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        Instance = this;

        _villagerScene = GD.Load<PackedScene>("res://scenes/VillagerNode.tscn");
        if (_villagerScene == null)
        {
            GD.PrintErr("[VillageSystem] res://scenes/VillagerNode.tscn not found — editor task pending");
            return;
        }

        var namePool = LoadNamePool();
        var (village, villagers) = VillageGenerator.Generate(GameSession.WorldSeed, namePool);
        _village = village;

        int i = 0;
        foreach (var data in villagers)
        {
            _villagers[data.Id] = data;

            float  y        = TerrainSystem.GetHeightAtWorld(data.WorldX, data.WorldZ) + 0.9f;
            var    spawnPos = new Vector3(data.WorldX, y, data.WorldZ);

            var node = _villagerScene.Instantiate<Node3D>();
            node.SetMeta("villager_id",        data.Id);
            node.SetMeta("villager_name",      data.Name);
            node.SetMeta("archetype_tag",      data.ArchetypeTag);
            node.SetMeta("archetype_name_key", data.ArchetypeNameKey);
            node.SetMeta("stat_str",           data.Stats.Str);
            node.SetMeta("stat_dex",           data.Stats.Dex);
            node.SetMeta("stat_con",           data.Stats.Con);
            node.SetMeta("stat_wis",           data.Stats.Wis);
            node.Name = $"npc_{i}";
            AddChild(node);
            node.GlobalPosition = spawnPos;

            _npcNodes[data.Id] = node;
            if (Multiplayer.IsServer())
            {
                _positions[data.Id]  = spawnPos;
                _npcHunger[data.Id]  = 100f;
                _npcRest[data.Id]    = 100f;
                _npcCarried[data.Id] = 0;
            }

            i++;
        }

        GD.Print($"[VillageSystem] spawned {villagers.Count} villagers at " +
                 $"({village.WorldX:F1}, {village.WorldZ:F1})");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Multiplayer.IsServer()) return;

        _tick++;
        _elapsed += delta;

        _needsTimer += (float)delta;
        if (_needsTimer >= NEEDS_UPDATE_INTERVAL)
        {
            TickNeeds(_needsTimer);
            _needsTimer = 0f;
        }

        TickResting((float)delta);
        TickDeposit((float)delta);
        TickForagerDeposit((float)delta);
        TickFollow((float)delta);
        TickJobs((float)delta);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public VillagerData? GetVillager(string villagerId) =>
        _villagers.TryGetValue(villagerId, out var d) ? d : null;

    public VillageData? GetVillage() => _village;

    public bool HasFollower(long peerId)  => _followerByPeer.ContainsKey(peerId);
    public string GetFollowerOf(long peerId) =>
        _followerByPeer.TryGetValue(peerId, out var id) ? id : "";

    // ── Save / Load (M8) ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns all settlement NPC records for serialization, including idle and sleeping NPCs.
    /// Previously only iterated _workAssignments, which caused sleeping NPCs (_suspendedStation)
    /// and idle settlement members to be lost on save → load.
    /// Called by SaveSystem.Save().
    /// </summary>
    public List<NpcAssignSave> GetAssignmentsForSave()
    {
        var result = new List<NpcAssignSave>();
        foreach (var npcId in _settlementNpcs)
        {
            _npcFounder.TryGetValue(npcId, out long founderPeerId);

            // Active assignment wins; fall back to suspended station for sleeping NPCs.
            // Empty string = idle settlement member (recruited but not yet assigned).
            string station = "";
            if (_workAssignments.TryGetValue(npcId, out var activeStation))
                station = activeStation;
            else if (_suspendedStation.TryGetValue(npcId, out var suspendedStation))
                station = suspendedStation;

            result.Add(new NpcAssignSave
            {
                NpcId           = npcId,
                StationNodeName = station,
                FounderPeerId   = founderPeerId
            });
        }
        return result;
    }

    /// <summary>
    /// Restores settlement NPC roster from save data, including idle and sleeping NPCs.
    /// Empty StationNodeName = idle settlement member (recruited but not assigned a station).
    /// NPCs must already exist in _villagers (seeded from the same world seed in _Ready).
    /// Called by SaveSystem.TryLoad().
    /// </summary>
    public void RestoreAssignmentsFromSave(List<NpcAssignSave> assignments)
    {
        if (!Multiplayer.IsServer()) return;

        foreach (var a in assignments)
        {
            if (!_villagers.ContainsKey(a.NpcId))
            {
                GD.PrintErr($"[VillageSystem] restore: NPC '{a.NpcId}' not found — skipping");
                continue;
            }

            _settlementNpcs.Add(a.NpcId);
            _npcFounder[a.NpcId] = a.FounderPeerId;

            if (!string.IsNullOrEmpty(a.StationNodeName))
            {
                _workAssignments[a.NpcId] = a.StationNodeName;
                _workFounder[a.NpcId]     = a.FounderPeerId;
                _jobTargetTree[a.NpcId]   = ""; // let the NPC find a new target on next tick
                GD.Print($"[VillageSystem] restored assignment: {a.NpcId} → {a.StationNodeName}");
            }
            else
            {
                GD.Print($"[VillageSystem] restored idle settler: {a.NpcId}");
            }
        }

        if (assignments.Count > 0)
            BroadcastVillageRoster(1L); // host is always peer 1; clients sync on reconnect
    }

    /// <summary>Sends the village roster to all connected peers. Used for reconnect replay.</summary>
    public void BroadcastRosterToAll() => BroadcastVillageRoster(1L);

    // ── Recruitment RPCs ──────────────────────────────────────────────────────

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestRecruit(string villagerId)
    {
        if (!Multiplayer.IsServer()) return;

        long sender = Multiplayer.GetRemoteSenderId();
        if (sender == 0) sender = 1L;

        if (!_villagers.TryGetValue(villagerId, out var data)) return;
        if (_followTargets.ContainsKey(villagerId))      { GD.Print($"[VillageSystem] {data.Name} already following someone"); return; }
        if (_workAssignments.ContainsKey(villagerId))    { GD.Print($"[VillageSystem] {data.Name} already working"); return; }
        if (_walkingToShelter.ContainsKey(villagerId))   { GD.Print($"[VillageSystem] {data.Name} is heading to rest"); return; }
        if (_sleeping.ContainsKey(villagerId))           { GD.Print($"[VillageSystem] {data.Name} is sleeping"); return; }
        if (_followerByPeer.ContainsKey(sender))
        {
            GD.Print($"[VillageSystem] peer {sender} already has a follower");
            SendWarningToPeer(sender, Loc.T("reject.already_has_follower"));
            return;
        }

        if (!_positions.TryGetValue(villagerId, out var npcPos)) return;
        var playerNode = GetNodeOrNull<Node3D>($"{PLAYERS_PATH}/Player_{sender}");
        if (playerNode == null) return;
        if (npcPos.DistanceTo(playerNode.GlobalPosition) > 3f)
        {
            GD.Print($"[VillageSystem] peer {sender} too far from {data.Name} to recruit");
            SendWarningToPeer(sender, Loc.T("reject.too_far_to_recruit"));
            return;
        }

        // Shelter capacity gate: each Shelter holds MAX_NPCS_PER_SHELTER settlers.
        // Zero shelters means zero capacity — player must build a Shelter first.
        int shelterCount   = CountBuildingsOfType("shelter");
        int shelterCapacity = shelterCount * MAX_NPCS_PER_SHELTER;
        if (_settlementNpcs.Count >= shelterCapacity)
        {
            SendWarningToPeer(sender, Loc.T("reject.no_shelter_capacity"));
            GD.Print($"[VillageSystem] recruit rejected — {_settlementNpcs.Count}/{shelterCapacity} capacity ({shelterCount} shelters)");
            return;
        }

        _followTargets[villagerId] = sender;
        _followerByPeer[sender]    = villagerId;
        _settlementNpcs.Add(villagerId);
        _npcFounder[villagerId] = sender;
        GD.Print($"[VillageSystem] {data.Name} now follows peer {sender}, added to settlement");

        if (sender == Multiplayer.GetUniqueId())
            LocalState.SetFollower(villagerId);
        else
            RpcId(sender, MethodName.ClientSetFollower, villagerId);

        BroadcastVillageRoster(sender);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestLeave(string villagerId)
    {
        if (!Multiplayer.IsServer()) return;

        long sender = Multiplayer.GetRemoteSenderId();
        if (sender == 0) sender = 1L;

        if (!_followTargets.TryGetValue(villagerId, out long followPeer)) return;
        if (followPeer != sender) return;

        _followTargets.Remove(villagerId);
        _followerByPeer.Remove(sender);
        // NPC stays in _settlementNpcs — they become idle in the settlement.
        GD.Print($"[VillageSystem] {villagerId} stopped following peer {sender} — now idle in settlement");

        if (sender == Multiplayer.GetUniqueId())
            LocalState.ClearFollower();
        else
            RpcId(sender, MethodName.ClientClearFollower);

        BroadcastVillageRoster(sender);
    }

    // ── Station assignment RPCs ───────────────────────────────────────────────

    /// <summary>
    /// Assigns any settlement NPC to any station without proximity or follower checks.
    /// Called from BuildingAssignmentPanel — the founder selects NPC + station via UI.
    /// NPC must be in _settlementNpcs (recruited to this settlement).
    /// Station must exist under SettlementSystem.
    /// If NPC was following, stop the follow so they can walk to the station.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestAssignNpcToStation(string npcId, string stationNodeName)
    {
        if (!Multiplayer.IsServer()) return;

        long sender = Multiplayer.GetRemoteSenderId();
        if (sender == 0) sender = 1L;

        if (!IsFounderPeer(sender))
        {
            GD.PrintErr($"[VillageSystem] peer {sender} tried to assign NPC — not a founder");
            return;
        }

        if (!_settlementNpcs.Contains(npcId))
        {
            GD.PrintErr($"[VillageSystem] NPC {npcId} is not in the settlement roster");
            return;
        }

        // Gate: settlement must have at least one Shelter for the worker to rest in.
        if (!HasBuildingOfType("shelter"))
        {
            SendWarningToPeer(sender, Loc.T("warning.assign.no_shelter"));
            return;
        }

        var stationNode = GetNodeOrNull($"/root/GameWorld/SettlementSystem/{stationNodeName}");
        if (stationNode == null)
        {
            GD.PrintErr($"[VillageSystem] station '{stationNodeName}' not found");
            return;
        }

        // If NPC was following someone, stop the follow.
        if (_followTargets.TryGetValue(npcId, out long followPeer))
        {
            _followTargets.Remove(npcId);
            _followerByPeer.Remove(followPeer);
            if (followPeer == Multiplayer.GetUniqueId())
                LocalState.ClearFollower();
            else
                RpcId(followPeer, MethodName.ClientClearFollower);
        }

        _workAssignments[npcId] = stationNodeName;
        _workFounder[npcId]     = sender;
        _jobTargetTree[npcId]   = "";

        if (_villagers.TryGetValue(npcId, out var data))
            GD.Print($"[VillageSystem] {data.Name} assigned to {stationNodeName}");

        BroadcastVillageRoster(sender);
    }

    /// <summary>
    /// Removes a settlement NPC from their current station. NPC becomes idle.
    /// Called from BuildingAssignmentPanel.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestUnassignNpc(string npcId)
    {
        if (!Multiplayer.IsServer()) return;

        long sender = Multiplayer.GetRemoteSenderId();
        if (sender == 0) sender = 1L;

        if (!IsFounderPeer(sender)) return;
        if (!_settlementNpcs.Contains(npcId)) return;

        _workAssignments.Remove(npcId);
        _workFounder.Remove(npcId);
        _jobTargetTree.Remove(npcId);
        _lastChopTime.Remove(npcId);
        _walkingToDeposit.Remove(npcId);
        // Forager state
        _foragerTarget.Remove(npcId);
        _foragerCarriedHerbs.Remove(npcId);
        _foragerCarriedBerries.Remove(npcId);
        _foragerWalkToDeposit.Remove(npcId);

        if (_villagers.TryGetValue(npcId, out var data))
            GD.Print($"[VillageSystem] {data.Name} unassigned — now idle");

        BroadcastVillageRoster(sender);
    }

    // Helper: is this peer the settlement founder (has planted a marker)?
    private static bool IsFounderPeer(long peerId) =>
        SettlementSystem.Instance?.IsFounder(peerId) ?? false;

    // ── Client RPCs ───────────────────────────────────────────────────────────

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ClientSetFollower(string villagerId) => LocalState.SetFollower(villagerId);

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ClientClearFollower() => LocalState.ClearFollower();

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
         TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void ClientMoveVillager(string villagerId, Vector3 pos)
    {
        if (Multiplayer.IsServer()) return; // server already moved it directly
        if (_npcNodes.TryGetValue(villagerId, out var node))
            node.Call("SetTarget", pos);
    }

    // ── Follow tick ───────────────────────────────────────────────────────────

    private void TickFollow(float delta)
    {
        foreach (var (villagerId, followerPeerId) in _followTargets)
        {
            if (_walkingToShelter.ContainsKey(villagerId)) continue;
            if (_sleeping.ContainsKey(villagerId))         continue;

            var playerNode = GetNodeOrNull<Node3D>($"{PLAYERS_PATH}/Player_{followerPeerId}");
            if (playerNode == null) continue;
            if (!_positions.TryGetValue(villagerId, out var pos)) continue;

            float dx = playerNode.GlobalPosition.X - pos.X;
            float dz = playerNode.GlobalPosition.Z - pos.Z;
            if (dx * dx + dz * dz <= FOLLOW_STOP_XZ * FOLLOW_STOP_XZ) continue;

            MoveNpcToward(villagerId, playerNode.GlobalPosition, delta);
        }
    }

    // ── Job tick ──────────────────────────────────────────────────────────────

    private void TickJobs(float delta)
    {
        var ts = TreeSystem.Instance;

        foreach (var (villagerId, stationNodeName) in _workAssignments)
        {
            if (_walkingToShelter.ContainsKey(villagerId))        continue;
            if (_sleeping.ContainsKey(villagerId))                 continue;
            if (_walkingToDeposit.ContainsKey(villagerId))         continue;
            if (_foragerWalkToDeposit.ContainsKey(villagerId))     continue;
            if (!_positions.TryGetValue(villagerId, out var npcPos)) continue;

            // Route to forager loop when assigned to a Herbalist's Hut.
            // Note: Godot normalises dots to underscores in node names.
            if (stationNodeName.StartsWith("building_herbalists_hut", System.StringComparison.Ordinal))
            {
                TickForagerJob(villagerId, delta);
                continue;
            }

            // Woodcutter loop — requires TreeSystem.
            if (ts == null) continue;

            // If already at carry capacity (e.g. just woke from sleep), head to deposit first.
            _npcCarried.TryGetValue(villagerId, out int alreadyCarried);
            if (alreadyCarried >= NPC_CARRY_CAPACITY)
            {
                string sp = FindNearestStockpile(npcPos);
                if (!string.IsNullOrEmpty(sp))
                    _walkingToDeposit[villagerId] = sp;
                continue;
            }

            // Validate or acquire target tree.
            string targetId = _jobTargetTree.TryGetValue(villagerId, out var t) ? t : "";
            if (!string.IsNullOrEmpty(targetId) && !ts.GetAvailableTreeIds().Contains(targetId))
                targetId = ""; // tree was felled

            if (string.IsNullOrEmpty(targetId))
            {
                targetId = FindNearestTree(npcPos, ts);
                _jobTargetTree[villagerId] = targetId;
                if (string.IsNullOrEmpty(targetId)) continue; // no trees in range
            }

            var treeNode = ts.GetNodeOrNull<Node3D>(targetId);
            if (treeNode == null) { _jobTargetTree[villagerId] = ""; continue; }

            float distToTree = npcPos.DistanceTo(treeNode.GlobalPosition);
            if (distToTree > CHOP_RANGE)
            {
                MoveNpcToward(villagerId, treeNode.GlobalPosition, delta);
            }
            else
            {
                double lastChop = _lastChopTime.TryGetValue(villagerId, out var lc) ? lc : -CHOP_COOLDOWN;
                if (_elapsed - lastChop < CHOP_COOLDOWN) continue;

                int yielded = ts.ServerChopTree(targetId);
                _lastChopTime[villagerId]  = _elapsed;
                _jobTargetTree[villagerId] = "";
                GD.Print($"[VillageSystem] NPC {villagerId} chopped {targetId}");

                if (yielded > 0)
                {
                    _npcCarried.TryGetValue(villagerId, out int carried);
                    carried += yielded;
                    _npcCarried[villagerId] = carried;
                    GD.Print($"[VillageSystem] NPC {villagerId} carrying {carried}/{NPC_CARRY_CAPACITY} wood");

                    if (carried >= NPC_CARRY_CAPACITY)
                    {
                        string sp = FindNearestStockpile(npcPos);
                        if (!string.IsNullOrEmpty(sp))
                        {
                            _walkingToDeposit[villagerId] = sp;
                            GD.Print($"[VillageSystem] NPC {villagerId} heading to deposit at {sp}");
                        }
                        else
                        {
                            // Warn the founder once per WARN_THROTTLE_SEC — NPC keeps carrying
                            // until a Stockpile Drop is placed.
                            _lastWarnTime.TryGetValue(villagerId, out double lastWarn);
                            if (_elapsed - lastWarn >= WARN_THROTTLE_SEC)
                            {
                                _lastWarnTime[villagerId] = _elapsed;
                                long founder = _workFounder.TryGetValue(villagerId, out long f) ? f : 0L;
                                if (founder != 0L)
                                    SendWarningToPeer(founder, Loc.T("warning.job.no_stockpile"));
                            }
                        }
                    }
                }
            }
        }
    }

    // ── Forager job tick ──────────────────────────────────────────────────────

    /// <summary>
    /// Movement-based forager loop. NPC walks to the nearest available herb patch or
    /// berry bush, harvests it, carries items back to the stockpile when at capacity.
    /// Mirrors the woodcutter loop — find target → move → harvest → haul → deposit.
    /// </summary>
    private void TickForagerJob(string villagerId, float delta)
    {
        if (!_positions.TryGetValue(villagerId, out var npcPos)) return;

        // Check carry capacity first.
        _foragerCarriedHerbs.TryGetValue(villagerId, out int herbs);
        _foragerCarriedBerries.TryGetValue(villagerId, out int berries);
        int totalCarried = herbs + berries;

        if (totalCarried >= FORAGER_CARRY_CAPACITY)
        {
            string sp = FindNearestStockpile(npcPos);
            if (!string.IsNullOrEmpty(sp))
            {
                _foragerWalkToDeposit[villagerId] = sp;
                GD.Print($"[VillageSystem] NPC {villagerId} carrying {totalCarried} — heading to stockpile {sp}");
            }
            else
            {
                // No stockpile built yet — warn founder.
                _lastWarnTime.TryGetValue(villagerId, out double lastWarn);
                if (_elapsed - lastWarn >= WARN_THROTTLE_SEC)
                {
                    _lastWarnTime[villagerId] = _elapsed;
                    long founder = _workFounder.TryGetValue(villagerId, out long f) ? f : 0L;
                    if (founder != 0L)
                        SendWarningToPeer(founder, Loc.T("warning.job.no_stockpile"));
                }
            }
            return;
        }

        // Validate or acquire a forage target.
        string targetId = _foragerTarget.TryGetValue(villagerId, out var t) ? t : "";
        if (!string.IsNullOrEmpty(targetId) && !IsForageTargetAvailable(targetId))
            targetId = ""; // target was harvested by someone else

        if (string.IsNullOrEmpty(targetId))
        {
            targetId = FindNearestForageTarget(npcPos);
            _foragerTarget[villagerId] = targetId;
            if (string.IsNullOrEmpty(targetId)) return; // nothing in range
        }

        Vector3 targetPos = GetForageTargetPosition(targetId);
        float   distSq    = (targetPos.X - npcPos.X) * (targetPos.X - npcPos.X)
                          + (targetPos.Z - npcPos.Z) * (targetPos.Z - npcPos.Z);

        if (distSq > FORAGE_RANGE * FORAGE_RANGE)
        {
            MoveNpcToward(villagerId, targetPos, delta);
        }
        else
        {
            int yielded = HarvestForageTarget(targetId);
            _foragerTarget[villagerId] = ""; // clear so we find a new target next tick

            if (yielded <= 0) return;

            if (targetId.StartsWith("bush_", System.StringComparison.Ordinal))
            {
                _foragerCarriedBerries.TryGetValue(villagerId, out int b);
                _foragerCarriedBerries[villagerId] = b + yielded;
                GD.Print($"[VillageSystem] NPC {villagerId} picked berry at {targetId} ({b + yielded} berries carried)");
            }
            else
            {
                _foragerCarriedHerbs.TryGetValue(villagerId, out int h);
                _foragerCarriedHerbs[villagerId] = h + yielded;
                GD.Print($"[VillageSystem] NPC {villagerId} picked herb at {targetId} ({h + yielded} herbs carried)");
            }
        }
    }

    // ── Forager deposit tick ──────────────────────────────────────────────────

    private void TickForagerDeposit(float delta)
    {
        var arrived = new List<string>();

        foreach (var (villagerId, stockpileNodeName) in _foragerWalkToDeposit)
        {
            if (!_positions.TryGetValue(villagerId, out var pos)) continue;

            var stockpileNode = GetNodeOrNull<Node3D>($"/root/GameWorld/SettlementSystem/{stockpileNodeName}");
            if (stockpileNode == null)
            {
                arrived.Add(villagerId); // stockpile removed
                continue;
            }

            float dx = stockpileNode.GlobalPosition.X - pos.X;
            float dz = stockpileNode.GlobalPosition.Z - pos.Z;
            if (dx * dx + dz * dz <= DEPOSIT_RANGE * DEPOSIT_RANGE)
                arrived.Add(villagerId);
            else
                MoveNpcToward(villagerId, stockpileNode.GlobalPosition, delta);
        }

        foreach (var id in arrived)
        {
            _foragerWalkToDeposit.Remove(id);

            _foragerCarriedHerbs.TryGetValue(id, out int herbs);
            _foragerCarriedBerries.TryGetValue(id, out int berries);

            if (herbs > 0)
            {
                SettlementSystem.Instance?.AddToStockpile("item.herb", herbs);
                _foragerCarriedHerbs[id] = 0;
                GD.Print($"[VillageSystem] NPC {id} deposited {herbs} herb(s)");
            }
            if (berries > 0)
            {
                SettlementSystem.Instance?.AddToStockpile("item.berry", berries);
                _foragerCarriedBerries[id] = 0;
                GD.Print($"[VillageSystem] NPC {id} deposited {berries} berry/ies");
            }
        }
    }

    // ── Forage target helpers ─────────────────────────────────────────────────

    private bool IsForageTargetAvailable(string targetId)
    {
        if (targetId.StartsWith("bush_", System.StringComparison.Ordinal))
            return BushSystem.Instance?.IsAvailable(targetId) ?? false;
        return HerbSystem.Instance?.IsAvailable(targetId) ?? false;
    }

    private string FindNearestForageTarget(Vector3 fromPos)
    {
        string bestId     = "";
        float  bestDistSq = MAX_FORAGE_SEARCH_RANGE * MAX_FORAGE_SEARCH_RANGE;

        // Herb patches first — herbs are more valuable (bandage ingredient).
        var hs = HerbSystem.Instance;
        if (hs != null)
        {
            foreach (var hId in hs.GetAvailableHerbPatchIds())
            {
                var pos = hs.GetHerbPosition(hId);
                float dx = pos.X - fromPos.X, dz = pos.Z - fromPos.Z;
                float distSq = dx * dx + dz * dz;
                if (distSq < bestDistSq) { bestDistSq = distSq; bestId = hId; }
            }
        }

        // Berry bushes as secondary targets.
        var bs = BushSystem.Instance;
        if (bs != null)
        {
            foreach (var bId in bs.GetAvailableBushIds())
            {
                var pos = bs.GetBushPosition(bId);
                float dx = pos.X - fromPos.X, dz = pos.Z - fromPos.Z;
                float distSq = dx * dx + dz * dz;
                if (distSq < bestDistSq) { bestDistSq = distSq; bestId = bId; }
            }
        }

        return bestId;
    }

    private static Vector3 GetForageTargetPosition(string targetId)
    {
        if (targetId.StartsWith("bush_", System.StringComparison.Ordinal))
            return BushSystem.Instance?.GetBushPosition(targetId) ?? Vector3.Zero;
        return HerbSystem.Instance?.GetHerbPosition(targetId) ?? Vector3.Zero;
    }

    private static int HarvestForageTarget(string targetId)
    {
        if (targetId.StartsWith("bush_", System.StringComparison.Ordinal))
            return BushSystem.Instance?.ForagerHarvestBush(targetId) ?? 0;
        return HerbSystem.Instance?.ForagerHarvestHerb(targetId) ?? 0;
    }

    // ── Deposit tick ──────────────────────────────────────────────────────────

    private void TickDeposit(float delta)
    {
        var arrived = new List<string>();

        foreach (var (villagerId, stockpileNodeName) in _walkingToDeposit)
        {
            if (!_positions.TryGetValue(villagerId, out var pos)) continue;

            var stockpileNode = GetNodeOrNull<Node3D>($"/root/GameWorld/SettlementSystem/{stockpileNodeName}");
            if (stockpileNode == null)
            {
                arrived.Add(villagerId); // stockpile was removed; clear haul state
                continue;
            }

            float dx = stockpileNode.GlobalPosition.X - pos.X;
            float dz = stockpileNode.GlobalPosition.Z - pos.Z;
            if (dx * dx + dz * dz <= DEPOSIT_RANGE * DEPOSIT_RANGE)
                arrived.Add(villagerId);
            else
                MoveNpcToward(villagerId, stockpileNode.GlobalPosition, delta);
        }

        foreach (var id in arrived)
        {
            _walkingToDeposit.Remove(id);
            _npcCarried.TryGetValue(id, out int carried);
            if (carried > 0)
            {
                SettlementSystem.Instance?.AddToStockpile("resource.wood", carried);
                _npcCarried[id] = 0;
                GD.Print($"[VillageSystem] NPC {id} deposited {carried} wood at stockpile");
            }
        }
    }

    // ── Warning helpers ───────────────────────────────────────────────────────

    private void SendWarningToPeer(long peerId, string message)
    {
        if (peerId == Multiplayer.GetUniqueId())
            LocalState.ShowWarning(message);
        else
            RpcId(peerId, MethodName.ClientShowWarning, message);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ClientShowWarning(string message) => LocalState.ShowWarning(message);

    /// <summary>Returns true if the settlement has at least one placed building whose
    /// node name contains <paramref name="nameContains"/>.</summary>
    private bool HasBuildingOfType(string nameContains)
    {
        const string SETTLEMENT_PATH = "/root/GameWorld/SettlementSystem";
        var settlement = GetNodeOrNull(SETTLEMENT_PATH);
        if (settlement == null) return false;

        foreach (Node child in settlement.GetChildren())
        {
            if (child.Name.ToString().Contains(nameContains))
                return true;
        }
        return false;
    }

    /// <summary>Returns the count of placed buildings whose node name contains
    /// <paramref name="nameContains"/>.</summary>
    private int CountBuildingsOfType(string nameContains)
    {
        const string SETTLEMENT_PATH = "/root/GameWorld/SettlementSystem";
        var settlement = GetNodeOrNull(SETTLEMENT_PATH);
        if (settlement == null) return 0;

        int count = 0;
        foreach (Node child in settlement.GetChildren())
        {
            if (child.Name.ToString().Contains(nameContains))
                count++;
        }
        return count;
    }

    private string FindNearestStockpile(Vector3 fromPos)
    {
        const string SETTLEMENT_PATH = "/root/GameWorld/SettlementSystem";
        var settlement = GetNodeOrNull(SETTLEMENT_PATH);
        if (settlement == null) return "";

        float  bestDistSq = float.MaxValue;
        string bestName   = "";

        foreach (Node child in settlement.GetChildren())
        {
            if (!child.Name.ToString().Contains("stockpile")) continue;
            if (child is not Node3D node3d) continue;

            float dx     = node3d.GlobalPosition.X - fromPos.X;
            float dz     = node3d.GlobalPosition.Z - fromPos.Z;
            float distSq = dx * dx + dz * dz;
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                bestName   = child.Name.ToString();
            }
        }

        return bestName;
    }

    // ── Movement helper ───────────────────────────────────────────────────────

    private void MoveNpcToward(string villagerId, Vector3 targetPos, float delta)
    {
        if (!_positions.TryGetValue(villagerId, out var pos)) return;

        float dx   = targetPos.X - pos.X;
        float dz   = targetPos.Z - pos.Z;
        float dist = Mathf.Sqrt(dx * dx + dz * dz);
        if (dist < 0.01f) return;

        float step = Mathf.Min(FOLLOW_SPEED * delta, dist);
        pos.X += dx / dist * step;
        pos.Z += dz / dist * step;
        pos.Y  = TerrainSystem.GetHeightAtWorld(pos.X, pos.Z) + 0.9f;
        _positions[villagerId] = pos;

        if (_npcNodes.TryGetValue(villagerId, out var node))
            node.GlobalPosition = pos;

        if (_tick % BROADCAST_EVERY == 0)
            Rpc(MethodName.ClientMoveVillager, villagerId, pos);
    }

    // ── Needs tick ────────────────────────────────────────────────────────────

    private void TickNeeds(float dt)
    {
        // Snapshot keys — SortedDictionary increments its version counter even when
        // setting an existing key's value, which throws InvalidOperationException if
        // we write to _npcHunger or _npcRest while iterating either of them.
        var ids       = new List<string>(_npcHunger.Keys);
        var toSuspend = new List<string>();

        foreach (var villagerId in ids)
        {
            if (_walkingToShelter.ContainsKey(villagerId)) continue;
            if (_sleeping.ContainsKey(villagerId))         continue;

            float rest = _npcRest[villagerId] - REST_DRAIN_PER_SEC * dt;
            _npcRest[villagerId] = Mathf.Max(0f, rest);

            float hunger = _npcHunger[villagerId] + HUNGER_RESTORE_PER_SEC * dt;
            _npcHunger[villagerId] = Mathf.Min(100f, hunger);

            if (_npcRest[villagerId] < REST_LOW_THRESHOLD)
                toSuspend.Add(villagerId);
        }

        foreach (var id in toSuspend)
            SuspendForRest(id);
    }

    private void TickResting(float delta)
    {
        // Walk NPCs toward their shelter target
        var arrivedIds = new List<string>();
        foreach (var (villagerId, shelterPos) in _walkingToShelter)
        {
            if (!_positions.TryGetValue(villagerId, out var pos)) continue;

            float dx = shelterPos.X - pos.X;
            float dz = shelterPos.Z - pos.Z;
            if (dx * dx + dz * dz <= SLEEP_ARRIVE_RANGE * SLEEP_ARRIVE_RANGE)
            {
                arrivedIds.Add(villagerId);
            }
            else
            {
                MoveNpcToward(villagerId, shelterPos, delta);
            }
        }

        foreach (var id in arrivedIds)
        {
            _walkingToShelter.Remove(id);
            _sleeping[id]  = _elapsed + SLEEP_DURATION_SEC;
            _npcRest[id]   = 100f;
            GD.Print($"[VillageSystem] NPC {id} sleeping until _elapsed={_sleeping[id]:F1}");
        }

        // Wake up NPCs whose sleep timer expired
        var wokenIds = new List<string>();
        foreach (var (villagerId, wakeTime) in _sleeping)
        {
            if (_elapsed >= wakeTime)
                wokenIds.Add(villagerId);
        }

        foreach (var id in wokenIds)
        {
            _sleeping.Remove(id);

            // Resume work assignment if the station still exists.
            if (_suspendedStation.TryGetValue(id, out string resumeStation))
            {
                _suspendedStation.Remove(id);
                var stationNode = GetNodeOrNull($"/root/GameWorld/SettlementSystem/{resumeStation}");
                if (stationNode != null)
                {
                    long resumeFounder   = _suspendedFounder.TryGetValue(id, out long f) ? f : 0L;
                    _workAssignments[id] = resumeStation;
                    _workFounder[id]     = resumeFounder;
                    _jobTargetTree[id]   = "";
                    GD.Print($"[VillageSystem] NPC {id} woke up, resuming work at {resumeStation}");
                }
                else
                {
                    GD.Print($"[VillageSystem] NPC {id} woke up — station {resumeStation} gone, going idle");
                }
                _suspendedFounder.Remove(id);
            }
            else
            {
                GD.Print($"[VillageSystem] NPC {id} woke up and is now idle");
            }
        }
    }

    private void SuspendForRest(string villagerId)
    {
        // Remove from any active state
        if (_followTargets.TryGetValue(villagerId, out long peer))
        {
            _followTargets.Remove(villagerId);
            _followerByPeer.Remove(peer);
            if (peer == Multiplayer.GetUniqueId())
                LocalState.ClearFollower();
            else
                RpcId(peer, MethodName.ClientClearFollower);
        }

        // Stop any in-progress haul walk; carried items persist so the NPC deposits them
        // when they resume work after sleeping.
        _walkingToDeposit.Remove(villagerId);
        _foragerWalkToDeposit.Remove(villagerId);

        long suspendedFounder = 0L;
        if (_workAssignments.TryGetValue(villagerId, out string stationToResume))
        {
            _suspendedStation[villagerId] = stationToResume;
            suspendedFounder = _workFounder.TryGetValue(villagerId, out long f) ? f : 0L;
            _suspendedFounder[villagerId] = suspendedFounder;
            _workAssignments.Remove(villagerId);
            _workFounder.Remove(villagerId);
            _jobTargetTree.Remove(villagerId);
            _lastChopTime.Remove(villagerId);
            _foragerTarget.Remove(villagerId);
        }

        Vector3 shelterPos = FindNearestShelterPosition(
            _positions.TryGetValue(villagerId, out var p) ? p : Vector3.Zero);
        _walkingToShelter[villagerId] = shelterPos;
        GD.Print($"[VillageSystem] NPC {villagerId} rest low — heading to shelter at {shelterPos}");
    }

    private Vector3 FindNearestShelterPosition(Vector3 fromPos)
    {
        const string SETTLEMENT_PATH = "/root/GameWorld/SettlementSystem";
        var settlement = GetNodeOrNull(SETTLEMENT_PATH);
        if (settlement == null) return fromPos;

        float   bestDistSq = float.MaxValue;
        Vector3 bestPos    = fromPos;

        foreach (Node child in settlement.GetChildren())
        {
            if (!child.Name.ToString().StartsWith("shelter", System.StringComparison.OrdinalIgnoreCase))
                continue;
            if (child is not Node3D node3d) continue;

            float dx     = node3d.GlobalPosition.X - fromPos.X;
            float dz     = node3d.GlobalPosition.Z - fromPos.Z;
            float distSq = dx * dx + dz * dz;
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                bestPos    = node3d.GlobalPosition;
            }
        }

        return bestPos;
    }

    // ── Tree search ───────────────────────────────────────────────────────────

    private string FindNearestTree(Vector3 fromPos, TreeSystem ts)
    {
        string bestId     = "";
        float  bestDistSq = MAX_CHOP_RANGE * MAX_CHOP_RANGE;

        foreach (var treeId in ts.GetAvailableTreeIds())
        {
            var node = ts.GetNodeOrNull<Node3D>(treeId);
            if (node == null) continue;

            float dx     = node.GlobalPosition.X - fromPos.X;
            float dz     = node.GlobalPosition.Z - fromPos.Z;
            float distSq = dx * dx + dz * dz;

            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                bestId     = treeId;
            }
        }
        return bestId;
    }

    // ── Village roster broadcast ──────────────────────────────────────────────

    /// <summary>
    /// Sends the settlement NPC roster (names, archetypes, current stations) to the founder.
    /// Called on recruit, leave, assign, and unassign so the client panel always reflects
    /// current state without a request/response round-trip.
    /// JSON format: [{"id":"v0","name":"Alice","archetypeKey":"archetype.forager.name","station":"building_herbalists_hut_10_20"}]
    /// </summary>
    private void BroadcastVillageRoster(long founderPeerId)
    {
        var sb = new System.Text.StringBuilder("[");
        bool first = true;
        foreach (var npcId in _settlementNpcs)
        {
            if (!_villagers.TryGetValue(npcId, out var data)) continue;
            string station = _workAssignments.TryGetValue(npcId, out var s) ? s : "";
            if (!first) sb.Append(',');
            sb.Append($"{{\"id\":\"{npcId}\",\"name\":\"{EscapeJson(data.Name)}\",");
            sb.Append($"\"archetypeKey\":\"{data.ArchetypeNameKey}\",\"station\":\"{station}\"}}");
            first = false;
        }
        sb.Append(']');
        string json = sb.ToString();

        if (founderPeerId == Multiplayer.GetUniqueId())
            ClientSetVillageRoster(json);
        else
            RpcId(founderPeerId, MethodName.ClientSetVillageRoster, json);
    }

    private static string EscapeJson(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ClientSetVillageRoster(string json) => LocalState.SetVillageRoster(json);

    // ── Data loading ──────────────────────────────────────────────────────────

    private static IReadOnlyList<string> LoadNamePool()
    {
        const string PATH = "res://data/base/villages/names.json";
        using var file = FileAccess.Open(PATH, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PrintErr($"[VillageSystem] could not open {PATH}");
            return new[] { "Villager" };
        }

        try
        {
            var doc   = JsonDocument.Parse(file.GetAsText());
            var arr   = doc.RootElement.GetProperty("names");
            var names = new List<string>(arr.GetArrayLength());
            foreach (var el in arr.EnumerateArray())
            {
                var s = el.GetString();
                if (!string.IsNullOrEmpty(s)) names.Add(s);
            }
            return names;
        }
        catch (JsonException ex)
        {
            GD.PrintErr($"[VillageSystem] failed to parse {PATH}: {ex.Message}");
            return new[] { "Villager" };
        }
    }
}
