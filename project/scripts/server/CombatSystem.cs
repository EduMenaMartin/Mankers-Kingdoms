using System.Collections.Generic;
using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Server;

/// <summary>
/// Server-authoritative melee and block handler.
///
/// RequestMeleeAttack: validates weapon ownership, swing cooldown, target liveness,
/// and attacker-to-target distance. If all gates pass:
///   1. Mutual exclusivity gate (combat.md §15): attacker cannot swing while blocking.
///   2. Attack roll: CombatResolver.ResolveAttack (1d20 + attackBonus vs targetNumber).
///   3. Damage application on hit.
///   Active blocking no longer hard-negates incoming attacks (§2.5 superseded).
///   Instead, blocking grants the defender a +4 TN bonus (GetPlayerTargetNumber).
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
	private readonly SortedDictionary<long, double>      _swingReady   = new();
	private readonly SortedDictionary<long, bool>        _blocking     = new();
	private readonly SortedDictionary<long, StatBlock> _playerStats = new();
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
		_swingReady[peerId]   = 0.0;
		_blocking[peerId]     = false;
		_playerStats[peerId]  = new StatBlock(13, 12, 10, 10); // overwritten by RequestSetStats when client announces stats
	}

	private void OnPlayerDisconnected(long peerId)
	{
		_swingReady.Remove(peerId);
		_blocking.Remove(peerId);
		_playerStats.Remove(peerId);
	}

	// ── Blocking state ────────────────────────────────────────────────────────

	/// <summary>True if the peer is actively blocking with a shield.</summary>
	public bool IsBlocking(long peerId) =>
		_blocking.TryGetValue(peerId, out bool b) && b;

	// ── Player stats API (called by HealthSystem, MonsterSystem, ProjectileSystem) ──

	private static readonly StatBlock _defaultStats = new(13, 12, 10, 10);

	/// <summary>
	/// Stores the full stat block for a peer. Called by RequestSetStats RPC when a
	/// client announces their rolled+race-modified stats after character creation.
	/// </summary>
	public void SetPlayerStats(long peerId, StatBlock stats)
	{
		_playerStats[peerId] = stats;
	}

	/// <summary>
	/// Returns the Target Number for a player peer (combat.md §2.2 + inventory.md §10.2).
	/// Reads ArmorValue, ShieldBonus, and ArmorCategory from the player's equipped slots.
	/// </summary>
	public int GetPlayerTargetNumber(long peerId)
	{
		var s   = _playerStats.TryGetValue(peerId, out var st) ? st : _defaultStats;
		var inv = InventorySystem.Instance?.GetInventory(peerId);

		int           armorValue    = 0;
		int           shieldBonus   = 0;
		ArmorCategory armorCategory = ArmorCategory.Light;

		if (inv != null)
		{
			if (inv.EquippedBodyArmor != null)
			{
				var armor = ArmorRegistry.Find(inv.EquippedBodyArmor);
				if (armor != null)
				{
					armorValue    = armor.ArmorValue;
					armorCategory = armor.ArmorCategory;
				}
			}
			if (inv.EquippedOffHand == "item.armor.shield")
			{
				var shield = ArmorRegistry.Find("item.armor.shield");
				if (shield != null) shieldBonus = shield.ShieldBonus;
			}
		}

		// Apply armor debuff (e.g. SunderingHit crit effect).
		armorValue += (int)(BuffSystem.Instance?.GetAdditiveModifier(peerId, BuffStat.ArmorValue) ?? 0f);

		// §15: active block grants +4 TN bonus while blocking with a shield.
		// shieldBonus > 0 confirms the shield is still equipped at hit time (re-verification).
		int activeBlockBonus = (IsBlocking(peerId) && shieldBonus > 0) ? 4 : 0;

		return CombatResolver.PlayerTargetNumber(s.Dex, armorValue, shieldBonus, armorCategory)
			   + activeBlockBonus;
	}

	/// <summary>Returns the stored StatBlock for a player peer, or safe defaults.</summary>
	public StatBlock GetPlayerStats(long peerId) =>
		_playerStats.TryGetValue(peerId, out var s) ? s : _defaultStats;

	/// <summary>
	/// RPC: client announces their rolled+race-modified stats after character creation.
	/// Server stores the StatBlock and uses it for all subsequent combat calculations.
	/// </summary>
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void RequestSetStats(int str, int dex, int con, int wis)
	{
		if (!Multiplayer.IsServer()) return;
		long sender = Multiplayer.GetRemoteSenderId();
		if (sender == 0) sender = 1L;
		SetPlayerStats(sender, new StatBlock(str, dex, con, wis));
		// Roll HP from Constitution (only executes if not yet rolled for this peer).
		HealthSystem.Instance?.ApplyConstitution(sender, con);
		GD.Print($"[Combat] peer {sender} stats set: Str={str} Dex={dex} Con={con} Wis={wis}");
	}

	// ── RPCs ──────────────────────────────────────────────────────────────────

	/// <summary>
	/// Client requests a melee swing at a target entity.
	/// Server validates: weapon owned, cooldown clear, target alive, distance ≤ range.
	/// Mutual exclusivity gate (combat.md §15): attacker cannot swing while blocking.
	/// Attack roll: CombatResolver.ResolveAttack — 1d20 + attackBonus vs target's TargetNumber.
	/// Active blocking by the defender raises TN by +4 (see GetPlayerTargetNumber) rather than
	/// hard-negating the attack (§2.5 superseded by §15).
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

		// ── Stun gate: stunned attacker cannot swing (no cooldown consumed) ─────
		if (BuffSystem.Instance?.IsBuffActive(sender, BuffStat.Stun) == true)
		{
			GD.Print($"[Combat] peer {sender} is stunned — attack blocked");
			return;
		}

		// ── Disarm gate: disarmed attacker cannot use their weapon ────────────
		if (BuffSystem.Instance?.IsBuffActive(sender, BuffStat.Disarm) == true)
		{
			GD.Print($"[Combat] peer {sender} is disarmed — attack blocked");
			return;
		}

		// ── Mutual exclusivity gate (combat.md §15): cannot attack while blocking ──
		if (IsBlocking(sender))
		{
			GD.Print($"[Combat] peer {sender} is blocking — cannot attack (§15 mutual exclusivity)");
			return;
		}

		// Commit cooldown before any early-returns below — the swing has happened.
		_swingReady[sender] = _elapsed + weapon.SwingCooldown;

		// ── Attack roll (combat.md §2.2) ─────────────────────────────────────
		// §15: defender's active block raises their TN by +4 (see GetPlayerTargetNumber).
		// No hard negation gate — the dice roll always happens.
		var stats        = GetPlayerStats(sender);
		int meleeLevel   = SkillSystem.Instance?.GetSkillLevel(sender, "skill.melee") ?? 0;
		int attackBonus  = CombatResolver.PlayerAttackBonus(weaponId, stats.Str, stats.Dex, meleeLevel);
		// Apply attack bonus debuff (e.g. OffBalance fumble effect).
		attackBonus += (int)(BuffSystem.Instance?.GetAdditiveModifier(sender, BuffStat.AttackBonus) ?? 0f);
		int targetNumber = GetEntityTargetNumber(targetEntityId);
		int damageMod    = CombatResolver.PlayerDamageMod(weaponId, stats.Str, stats.Dex);

		var (hit, damage, isCrit, isFumble) = CombatResolver.ResolveAttack(
			attackBonus, targetNumber, weapon.DamageDice, damageMod, _combatRng);

		if (!hit)
		{
			GD.Print($"[Combat] peer {sender} missed entity {targetEntityId} " +
					 $"(roll+{attackBonus} vs TN {targetNumber}){(isFumble ? " FUMBLE" : "")}");
			GetNodeOrNull<Node>(COMBAT_FEEDBACK_PATH)
				?.Rpc("ShowCombatResult", targetPos.Value, false, 0, false);

			// Apply fumble effect to the attacker.
			if (isFumble && BuffSystem.Instance != null)
			{
				var fumble = CombatResolver.RollFumbleEffect(_combatRng);
				BuffSystem.Instance.ApplyFumbleEffect(fumble, sender);
			}
			return;
		}

		HealthSystem.Instance.ApplyDamage(targetEntityId, damage);
		SkillSystem.Instance?.NotifyAction(sender, "skill.melee");
		GetNodeOrNull<Node>(COMBAT_FEEDBACK_PATH)
			?.Rpc("ShowCombatResult", targetPos.Value, true, damage, isCrit);
		GD.Print($"[Combat] peer {sender} hit entity {targetEntityId} with {weaponId} " +
				 $"for {damage}{(isCrit ? " (CRIT)" : "")} (TN {targetNumber}, AB {attackBonus})");

		// Apply crit effect to the defender.
		if (isCrit && BuffSystem.Instance != null)
		{
			var crit = CombatResolver.RollCritEffect(_combatRng);
			BuffSystem.Instance.ApplyCritEffect(crit, targetEntityId);
		}
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

		// Shield must be equipped in Off-Hand (inventory.md §10.3).
		// For saves predating the equipment slot system (EquippedOffHand == null),
		// fall back to inventory ownership so legacy saves don't silently lose blocking.
		var playerInv  = InventorySystem.Instance?.GetInventory(sender);
		bool hasShield = playerInv?.EquippedOffHand == "item.armor.shield"
					  || (playerInv?.EquippedOffHand == null
						  && InventorySystem.Instance.HasItems(sender, "item.armor.shield", 1));
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
	private int GetEntityTargetNumber(long entityId)
	{
		var monsterData = MonsterSystem.Instance?.GetMonsterData(entityId);
		if (monsterData != null) return monsterData.TargetNumber;
		return GetPlayerTargetNumber(entityId);
	}

	private Vector3? GetEntityPosition(long entityId)
	{
		var playerNode = GetNodeOrNull<Node3D>($"{PLAYERS_PATH}/Player_{entityId}");
		if (playerNode != null) return playerNode.GlobalPosition;

		return MonsterSystem.Instance?.GetMonsterPosition(entityId);
	}
}
