using System.Collections.Generic;
using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Server;

/// <summary>
/// Server-authoritative skill progression tracker (M5).
///
/// Tracks raw XP and class bumps per peer per skill.
/// Effective level = min(statCap, rawXp / XpPerLevel + classBump).
///
/// NotifyAction — called by other server systems when a skill-relevant action succeeds:
///   CombatSystem   melee hit  → NotifyAction(peerId, "skill.melee")
///   ProjectileSystem ranged hit → NotifyAction(peerId, "skill.ranged")
///   BushSystem     harvest    → NotifyAction(peerId, "skill.foraging")
///   BushSystem     cook       → NotifyAction(peerId, "skill.cooking")
///   TreeSystem     fell tree  → NotifyAction(peerId, "skill.woodcutting")
///
/// ApplyBump — called by HealthSystem.RequestSetClass after kit distribution.
///
/// Node must appear in GameWorld.tscn AFTER InventorySystem and CombatSystem.
/// </summary>
public partial class SkillSystem : Node
{
    public static SkillSystem Instance { get; private set; } = null!;

    // Outer key: peerId. Inner key: skillId. SortedDictionary: ADR-0011.
    private readonly SortedDictionary<long, SortedDictionary<string, int>> _xp    = new();
    private readonly SortedDictionary<long, SortedDictionary<string, int>> _bumps = new();

    public override void _Ready()
    {
        Instance = this;

        var net = NetworkManager.Instance;
        net.PlayerConnected    += OnPlayerConnected;
        net.PlayerDisconnected += OnPlayerDisconnected;
    }

    private void OnPlayerConnected(long peerId)
    {
        _xp[peerId]    = new SortedDictionary<string, int>();
        _bumps[peerId] = new SortedDictionary<string, int>();

        // Send starting levels (all 0 before class bump) to the new peer.
        BroadcastAllLevels(peerId);
        GD.Print($"[Skill] peer {peerId} registered");
    }

    private void OnPlayerDisconnected(long peerId)
    {
        _xp.Remove(peerId);
        _bumps.Remove(peerId);
    }

    // ── Server API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by game systems when a skill-relevant action succeeds.
    /// Awards XP, levels up if threshold crossed, grants tool tier items on unlock.
    /// </summary>
    public void NotifyAction(long peerId, string skillId)
    {
        if (!Multiplayer.IsServer()) return;

        var skill = SkillRegistry.Find(skillId);
        if (skill == null) return;

        EnsurePeerEntries(peerId);

        var xpMap   = _xp[peerId];
        var bumpMap = _bumps[peerId];

        xpMap.TryGetValue(skillId, out int rawXp);
        bumpMap.TryGetValue(skillId, out int bump);

        int oldLevel = ComputeLevel(peerId, skill, rawXp, bump);

        rawXp += skill.XpPerAction;
        xpMap[skillId] = rawXp;

        int newLevel = ComputeLevel(peerId, skill, rawXp, bump);

        if (newLevel > oldLevel)
        {
            GD.Print($"[Skill] peer {peerId} {skillId} → level {newLevel} (xp {rawXp})");
            SendLevelTo(peerId, skillId, newLevel);
            CheckToolTierGrants(peerId, skill, oldLevel, newLevel);
        }
    }

    /// <summary>
    /// Applies class starting skill bonuses for a peer.
    /// Called by HealthSystem.RequestSetClass after distributing the kit.
    /// Overwrites any previous bump (safe to call on class re-selection).
    /// </summary>
    public void ApplyBump(long peerId, ClassSkillBump[] bumps)
    {
        if (!Multiplayer.IsServer()) return;

        EnsurePeerEntries(peerId);

        // Clear existing bumps so a re-selection doesn't stack.
        _bumps[peerId].Clear();

        foreach (var b in bumps)
            _bumps[peerId][b.SkillId] = b.Amount;

        // Rebroadcast all affected skills with the new effective levels.
        BroadcastAllLevels(peerId);
        GD.Print($"[Skill] peer {peerId} class bumps applied ({bumps.Length} skills)");
    }

    /// <summary>
    /// Returns the current effective skill level for a peer.
    /// Called by CombatSystem and ProjectileSystem to compute attack bonuses.
    /// </summary>
    public int GetSkillLevel(long peerId, string skillId)
    {
        var skill = SkillRegistry.Find(skillId);
        if (skill == null) return 0;
        EnsurePeerEntries(peerId);
        _xp[peerId].TryGetValue(skillId, out int rawXp);
        _bumps[peerId].TryGetValue(skillId, out int bump);
        return ComputeLevel(peerId, skill, rawXp, bump);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void EnsurePeerEntries(long peerId)
    {
        if (!_xp.ContainsKey(peerId))
            _xp[peerId] = new SortedDictionary<string, int>();
        if (!_bumps.ContainsKey(peerId))
            _bumps[peerId] = new SortedDictionary<string, int>();
    }

    /// <summary>
    /// effectiveLevel = min(statCap, rawXp / XpPerLevel + classBump).
    /// StatBlock is fetched from CombatSystem; falls back to all-10s if not yet set.
    /// </summary>
    private int ComputeLevel(long peerId, SkillData skill, int rawXp, int bump)
    {
        var stats = CombatSystem.Instance?.GetPlayerStats(peerId)
                    ?? new StatBlock(10, 10, 10, 10);
        int cap      = skill.GetCap(stats);
        int rawLevel = rawXp / skill.XpPerLevel;
        return System.Math.Min(cap, rawLevel + bump);
    }

    private void CheckToolTierGrants(long peerId, SkillData skill, int oldLevel, int newLevel)
    {
        foreach (var tier in skill.ToolTiers)
        {
            if (tier.MinLevel > oldLevel && tier.MinLevel <= newLevel)
            {
                InventorySystem.Instance.AddItem(peerId, tier.GrantedItemId, 1);
                GD.Print($"[Skill] peer {peerId} unlocked tier item {tier.GrantedItemId} " +
                         $"at {skill.Id} level {newLevel}");
            }
        }
    }

    private void BroadcastAllLevels(long peerId)
    {
        _xp.TryGetValue(peerId, out var xpMap);
        _bumps.TryGetValue(peerId, out var bumpMap);

        foreach (var skill in SkillRegistry.All)
        {
            int rawXp = xpMap != null && xpMap.TryGetValue(skill.Id, out int x) ? x : 0;
            int bump  = bumpMap != null && bumpMap.TryGetValue(skill.Id, out int b) ? b : 0;
            int level = ComputeLevel(peerId, skill, rawXp, bump);
            SendLevelTo(peerId, skill.Id, level);
        }
    }

    private void SendLevelTo(long peerId, string skillId, int level)
    {
        if (peerId == 1)
            ClientApplySkillLevel(skillId, level);
        else
            RpcId(peerId, MethodName.ClientApplySkillLevel, skillId, level);
    }

    // ── Save / Load (M8) ─────────────────────────────────────────────────────

    /// <summary>Returns the raw XP map for a peer (before bump and stat cap).</summary>
    public Dictionary<string, int> GetXpForSave(long peerId)
    {
        _xp.TryGetValue(peerId, out var map);
        return map != null
            ? new Dictionary<string, int>(map)
            : new Dictionary<string, int>();
    }

    /// <summary>Returns the class bump map for a peer.</summary>
    public Dictionary<string, int> GetBumpsForSave(long peerId)
    {
        _bumps.TryGetValue(peerId, out var map);
        return map != null
            ? new Dictionary<string, int>(map)
            : new Dictionary<string, int>();
    }

    /// <summary>
    /// Overwrites XP and bumps for a peer from save data, then rebroadcasts effective levels.
    /// Called by SaveSystem.TryLoad().
    /// </summary>
    public void RestoreSkillsFromSave(
        long peerId,
        Dictionary<string, int> xp,
        Dictionary<string, int> bumps)
    {
        if (!Multiplayer.IsServer()) return;

        EnsurePeerEntries(peerId);
        _xp[peerId].Clear();
        foreach (var (skillId, rawXp) in xp)
            _xp[peerId][skillId] = rawXp;

        _bumps[peerId].Clear();
        foreach (var (skillId, bump) in bumps)
            _bumps[peerId][skillId] = bump;

        BroadcastAllLevels(peerId);
        GD.Print($"[Skill] peer {peerId} skills restored from save");
    }

    /// <summary>Re-sends all effective skill levels to a peer. Used for reconnect replay.</summary>
    public void BroadcastLevelsTo(long peerId) => BroadcastAllLevels(peerId);

    // ── Client RPC ────────────────────────────────────────────────────────────

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ClientApplySkillLevel(string skillId, int level)
    {
        LocalState.SetSkillLevel(skillId, level);
    }
}
