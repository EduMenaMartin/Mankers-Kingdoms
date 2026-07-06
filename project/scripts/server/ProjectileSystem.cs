using System.Collections.Generic;
using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Server;

/// <summary>
/// Server-authoritative projectile physics and hit detection.
///
/// On RequestFireProjectile: validates weapon/ammo ownership, consumes 1 ammo,
/// spawns a live projectile, and broadcasts ClientSpawnArrow to all peers so
/// BowController can show an arrow ghost immediately.
///
/// Each _PhysicsProcess tick (server-only): advances position, applies gravity,
/// sphere-tests vs players and monsters (layers 32+64). On hit → HealthSystem.ApplyDamage
/// + ClientRemoveArrow RPC. Projectiles also expire after MAX_LIFETIME_SEC.
///
/// Extends Node3D (not Node) so GetWorld3D() is available for physics queries.
/// Node must appear in GameWorld.tscn AFTER HealthSystem and InventorySystem.
/// </summary>
public partial class ProjectileSystem : Node
{
	public static ProjectileSystem Instance { get; private set; } = null!;

	private const float GRAVITY         = 9.8f;
	private const float HIT_RADIUS      = 0.55f; // generous to absorb server latency
	private const float MAX_LIFETIME_SEC = 4f;

	// Players (layer 6 = 32) + Monsters (layer 7 = 64). Phase 4 adds monsters.
	private const uint COMBAT_MASK = 32u | 64u;

	private const string PLAYERS_PATH         = "/root/GameWorld/Players";
	private const string MONSTERS_PATH        = "/root/GameWorld/Monsters";
	private const string COMBAT_FEEDBACK_PATH = "/root/GameWorld/CombatFeedbackHUD";

	// Monster IDs start at 10001 — any OriginPeerId below this is a player.
	private const long MONSTER_ID_THRESHOLD = 10001L;

	// Sequential ID; never reused within a session.
	private long _nextId = 1L;

	// Seeded per ADR-0022. Initialised in _Ready() once GameSession.WorldSeed is set.
	private System.Random _projectileRng = null!;

	private sealed class LiveProjectile
	{
		public long   Id;
		public long   OriginPeerId;
		public string WeaponId = "";
		public float  PosX, PosY, PosZ;
		public float  VelX, VelY, VelZ;
		public float  LifetimeRemaining;
	}

	// SortedDictionary: ADR-0011 deterministic iteration.
	private readonly SortedDictionary<long, LiveProjectile> _live    = new();
	private readonly List<long>                             _toRemove = new();

	public override void _Ready()
	{
		Instance = this;
		if (Multiplayer.IsServer())
			_projectileRng = new System.Random((int)(GameSession.WorldSeed ^ 0xAB0570FFu));
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!Multiplayer.IsServer()) return;
		if (_live.Count == 0) return;

		float dt         = (float)delta;
		var   spaceState = GetViewport().GetWorld3D().DirectSpaceState;

		foreach (var (id, proj) in _live)
		{
			// Euler integrate with gravity.
			proj.VelY -= GRAVITY * dt;
			proj.PosX += proj.VelX * dt;
			proj.PosY += proj.VelY * dt;
			proj.PosZ += proj.VelZ * dt;

			proj.LifetimeRemaining -= dt;
			if (proj.LifetimeRemaining <= 0f)
			{
				_toRemove.Add(id);
				continue;
			}

			// Sphere overlap at current position.
			var pos    = new Vector3(proj.PosX, proj.PosY, proj.PosZ);
			var sphere = new SphereShape3D { Radius = HIT_RADIUS };
			var query  = new PhysicsShapeQueryParameters3D
			{
				Shape         = sphere,
				Transform     = new Transform3D(Basis.Identity, pos),
				CollisionMask = COMBAT_MASK,
			};

			// Exclude the shooter so self-hits are impossible.
			// Player path used for player projectiles; monster path for monster projectiles.
			var shooter =
				GetNodeOrNull<CollisionObject3D>($"{PLAYERS_PATH}/Player_{proj.OriginPeerId}")
				?? GetNodeOrNull<CollisionObject3D>($"{MONSTERS_PATH}/Monster_{proj.OriginPeerId}");
			if (shooter != null)
				query.Exclude = new Godot.Collections.Array<Rid> { shooter.GetRid() };

			var hits = spaceState.IntersectShape(query, maxResults: 4);
			if (hits.Count == 0) continue;

			// Prefer entity hits over geometry hits.
			long? targetId = null;
			foreach (var hit in hits)
			{
				var collider = hit["collider"].As<Node>();
				targetId = ParseEntityId(collider);
				if (targetId != null) break;
			}

			// Hit something — entity or geometry.
			if (targetId != null && HealthSystem.Instance.IsAlive(targetId.Value))
			{
				// Gate damage on faction relationship (docs/gdd/factions.md §6, ADR-0024).
				// Replaces the earlier MONSTER_ID_THRESHOLD ad-hoc patch.
				string originFaction = GetEntityFactionId(proj.OriginPeerId);
				string targetFaction = GetEntityFactionId(targetId.Value);
				if (!FactionService.IsHostile(originFaction, targetFaction))
				{
					_toRemove.Add(id);
					continue;
				}

				// combat.md §12.4: active block intercepts projectiles just like melee swings.
				if (CombatSystem.Instance?.IsBlocking(targetId.Value) == true)
				{
					var blockPos = new Vector3(proj.PosX, proj.PosY, proj.PosZ);
					GetNodeOrNull<Node>(COMBAT_FEEDBACK_PATH)
						?.Rpc("ShowCombatResult", blockPos, false, -1, false);
					GD.Print($"[Projectile] proj {id} blocked by entity {targetId.Value}");
					_toRemove.Add(id);
					continue;
				}

				var weapon = WeaponRegistry.Find(proj.WeaponId);
				if (weapon != null)
				{
					// Ranged physical hit: trajectory is the attack roll; just roll damage dice.
					// Player shots add Dex damage modifier; monster shots use 0.
					// combat.md §4: Damage = WeaponDice + StatModifier(Dex for ranged).
					int damageMod = 0;
					if (proj.OriginPeerId < MONSTER_ID_THRESHOLD)
					{
						var pStats = CombatSystem.Instance?.GetPlayerStats(proj.OriginPeerId)
									 ?? new StatBlock(13, 12, 10, 10);
						damageMod = CombatResolver.PlayerDamageMod(proj.WeaponId, pStats.Str, pStats.Dex);
					}
					int dmg = System.Math.Max(1,
						CombatResolver.RollDice(weapon.DamageDice, _projectileRng) + damageMod);
					HealthSystem.Instance.ApplyDamage(targetId.Value, dmg);
					// Only award skill XP for player-fired shots (monster shots have origin ≥ 10001).
					if (proj.OriginPeerId < MONSTER_ID_THRESHOLD)
						SkillSystem.Instance?.NotifyAction(proj.OriginPeerId, "skill.ranged");
					var hitPos = new Vector3(proj.PosX, proj.PosY, proj.PosZ);
					GetNodeOrNull<Node>(COMBAT_FEEDBACK_PATH)
						?.Rpc("ShowCombatResult", hitPos, true, dmg, false);
					GD.Print($"[Projectile] {proj.WeaponId} (id {id}) hit entity {targetId.Value} for {dmg} dmg");
				}
			}

			_toRemove.Add(id);
		}

		foreach (var id in _toRemove)
		{
			_live.Remove(id);
			Rpc(MethodName.ClientRemoveArrow, id);
		}
		_toRemove.Clear();
	}

	// ── RPC (client → server) ─────────────────────────────────────────────────

	/// <summary>
	/// Client requests to fire a ranged weapon. Server validates ownership and ammo,
	/// consumes 1 ammo, creates the authoritative projectile, and tells all clients
	/// to spawn a ghost via ClientSpawnArrow.
	/// </summary>
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void RequestFireProjectile(string weaponId, Vector3 origin, Vector3 direction)
	{
		if (!Multiplayer.IsServer()) return;

		long sender = Multiplayer.GetRemoteSenderId();
		if (sender == 0) sender = 1L;

		var weapon = WeaponRegistry.Find(weaponId);
		if (weapon == null || !weapon.IsRanged)
		{
			GD.PrintErr($"[Projectile] peer {sender}: invalid ranged weapon '{weaponId}'");
			return;
		}

		if (!InventorySystem.Instance.HasItems(sender, weaponId, 1))
		{
			GD.Print($"[Projectile] peer {sender} does not own {weaponId}");
			return;
		}

		if (weapon.AmmoItemId != null && !InventorySystem.Instance.HasItems(sender, weapon.AmmoItemId, 1))
		{
			GD.Print($"[Projectile] peer {sender} has no ammo ({weapon.AmmoItemId})");
			return;
		}

		if (!HealthSystem.Instance.IsAlive(sender))
		{
			GD.Print($"[Projectile] dead peer {sender} cannot fire");
			return;
		}

		// Consume one arrow.
		if (weapon.AmmoItemId != null)
			InventorySystem.Instance.RemoveItems(sender, weapon.AmmoItemId, 1);

		// Create live projectile.
		var dir = direction.Normalized();
		long id = _nextId++;

		_live[id] = new LiveProjectile
		{
			Id                = id,
			OriginPeerId      = sender,
			WeaponId          = weaponId,
			PosX              = origin.X,
			PosY              = origin.Y,
			PosZ              = origin.Z,
			VelX              = dir.X * weapon.ProjectileSpeed,
			VelY              = dir.Y * weapon.ProjectileSpeed,
			VelZ              = dir.Z * weapon.ProjectileSpeed,
			LifetimeRemaining = MAX_LIFETIME_SEC,
		};

		// Broadcast ghost spawn to all clients (including host via CallLocal).
		Rpc(MethodName.ClientSpawnArrow, id, origin, dir, weapon.ProjectileSpeed);
		GD.Print($"[Projectile] peer {sender} fired {weaponId} (proj {id}) " +
				 $"dir ({dir.X:F2}, {dir.Y:F2}, {dir.Z:F2}) speed {weapon.ProjectileSpeed}");
	}

	// ── RPCs (server → all clients, CallLocal = true) ─────────────────────────

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientSpawnArrow(long id, Vector3 origin, Vector3 direction, float speed)
	{
		LocalState.NotifyArrowSpawned(id,
			origin.X, origin.Y, origin.Z,
			direction.X, direction.Y, direction.Z,
			speed);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientRemoveArrow(long id)
	{
		LocalState.NotifyArrowRemoved(id);
	}

	// ── Server API (called by MonsterSystem) ─────────────────────────────────

	/// <summary>
	/// Fires a projectile on behalf of a monster. Bypasses all inventory/ammo checks —
	/// monsters don't have inventories. OriginPeerId is set to the monster ID so the
	/// projectile can't hit itself (though monsters aren't in the hit query anyway).
	/// </summary>
	public void FireFromMonster(long monsterId, string weaponId, Vector3 origin, Vector3 direction)
	{
		var weapon = WeaponRegistry.Find(weaponId);
		if (weapon == null || !weapon.IsRanged)
		{
			GD.PrintErr($"[Projectile] monster {monsterId}: invalid ranged weapon '{weaponId}'");
			return;
		}

		var dir = direction.Normalized();
		long id = _nextId++;

		_live[id] = new LiveProjectile
		{
			Id                = id,
			OriginPeerId      = monsterId,
			WeaponId          = weaponId,
			PosX              = origin.X,
			PosY              = origin.Y,
			PosZ              = origin.Z,
			VelX              = dir.X * weapon.ProjectileSpeed,
			VelY              = dir.Y * weapon.ProjectileSpeed,
			VelZ              = dir.Z * weapon.ProjectileSpeed,
			LifetimeRemaining = MAX_LIFETIME_SEC,
		};

		Rpc(MethodName.ClientSpawnArrow, id, origin, dir, weapon.ProjectileSpeed);
	}

	// ── Utility ───────────────────────────────────────────────────────────────

	/// <summary>
	/// Maps a runtime entity ID to its faction ID for relationship queries.
	/// Players all share FactionService.PLAYER_FACTION_ID in v1.
	/// Monsters look up their faction via MonsterSystem (assigned at spawn from their nest).
	/// Unknown entities (dead monsters, etc.) return PLAYER_FACTION_ID as a safe fallback
	/// — this will cause the hit to be treated as Allied and skipped, which is fail-safe.
	/// </summary>
	private static string GetEntityFactionId(long entityId)
	{
		if (entityId < MONSTER_ID_THRESHOLD)
			return FactionService.PLAYER_FACTION_ID;

		return MonsterSystem.Instance?.GetMonsterFactionId(entityId)
			   ?? FactionService.PLAYER_FACTION_ID;
	}

	private static long? ParseEntityId(Node? node)
	{
		if (node == null) return null;
		var name = node.Name.ToString();

		if (name.StartsWith("Player_") &&
			long.TryParse(name["Player_".Length..], out var peerId))
			return peerId;

		if (name.StartsWith("Monster_") &&
			long.TryParse(name["Monster_".Length..], out var monsterId))
			return monsterId;

		return null;
	}
}
