using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Client;

/// <summary>
/// Attached to every Player node in the GameWorld.
/// Three operating modes depending on who is running this frame:
///
///   1. Local player (IsMultiplayerAuthority() == true, any peer):
///      Reads WASD input, predicts movement immediately (no lag), sends input RPC to server,
///      reconciles if server correction diverges beyond CORRECTION_THRESHOLD.
///      Also handles E-key tree chopping via raycast → TreeSystem.ReceiveChop RPC.
///
///   2. Remote player simulated on the server (Multiplayer.IsServer() == true, !IsMultiplayerAuthority()):
///      Buffers input received via ReceiveInput RPC, simulates movement authoritatively each
///      physics tick, broadcasts position snapshots to all peers via UpdateState RPC.
///
///   3. Remote player on a non-authority client:
///      Lerps toward the latest position received via UpdateState RPC.
///      No input processing, no physics simulation.
///
/// See ADR-0005: server-authoritative with client prediction.
/// </summary>
public partial class PlayerController : CharacterBody3D
{
	private const float SPEED              = 5f;
	private const float GRAVITY            = 9.8f;
	private const float CORRECTION_THRESHOLD = 0.5f;
	private const float CORRECTION_LERP    = 0.3f;
	private const float REMOTE_LERP        = 0.15f;
	private const int   BROADCAST_INTERVAL = 9;

	// Raycast length for tree interaction (E key).
	private const float INTERACT_RANGE = 2.5f;

	private bool _isLocalPlayer;
	private Camera3D _camera = null!;
	private int _tickCount;

	private Vector2 _bufferedInput;
	private Vector3 _serverPosition;
	private bool _hasServerCorrection;
	private Vector3 _remoteTarget;

	// Path to TreeSystem node — used for untyped RPC to avoid client→server import.
	private const string TREE_SYSTEM_PATH = "/root/GameWorld/TreeSystem";

	// Path to SettlementSystem — used for untyped RPC to avoid client→server import.
	private const string SETTLEMENT_SYSTEM_PATH = "/root/GameWorld/SettlementSystem";

	// Path to NeedsSystem — used for untyped RPC to avoid client→server import.
	private const string NEEDS_SYSTEM_PATH = "/root/GameWorld/NeedsSystem";

	// Path to BushSystem — used for untyped RPC to avoid client→server import.
	private const string BUSH_SYSTEM_PATH = "/root/GameWorld/BushSystem";

	// Path to CombatSystem — used for untyped RPC (arrow crafting at workbench).
	private const string COMBAT_SYSTEM_PATH = "/root/GameWorld/CombatSystem";

	// Path to HealthSystem — used for untyped RPC (item drop pickup).
	private const string HEALTH_SYSTEM_PATH = "/root/GameWorld/HealthSystem";

	// Path to ProjectileSystem — used for untyped RPC (not used directly; BowController owns fire).
	private const string PROJECTILE_SYSTEM_PATH = "/root/GameWorld/ProjectileSystem";

	public override void _Ready()
	{
		_camera = GetNode<Camera3D>("Camera3D");
		_isLocalPlayer = IsMultiplayerAuthority();
		_remoteTarget = GlobalPosition;

		// Layer 2 = trees, Layer 4 (bitmask 8) = buildings.
		CollisionMask |= 2u | 8u;

		// Layer 6 (bitmask 32) = player combat layer — makes this node detectable
		// by MeleeController's sphere query on all peers (local and remote).
		CollisionLayer |= 32u;

		if (_isLocalPlayer)
		{
			// Defer so GlobalPosition is set by PlayerSystem before we activate this camera.
			Callable.From(() => { _camera.MakeCurrent(); }).CallDeferred();
			// PlacementController handles building ghost preview for the local player.
			AddChild(new PlacementController());
			// MeleeController handles LMB swing and RMB block for the local player.
			AddChild(new MeleeController());
			// BowController handles LMB fire (ranged) and arrow ghost management.
			AddChild(new BowController());
			// Tell the server our chosen class so it distributes the right kit and stats.
			// Deferred so the node tree is fully ready before the RPC fires.
			Callable.From(AnnounceClass).CallDeferred();
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		_tickCount++;

		if (_isLocalPlayer)
			ProcessLocalPlayer((float)delta);
		else if (Multiplayer.IsServer())
			ProcessRemoteOnServer((float)delta);
		else
			ProcessRemoteOnClient();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!_isLocalPlayer) return;
		if (@event.IsActionPressed("interact"))
			TryInteract();
		if (@event.IsActionPressed("plant_marker"))
			TryPlantMarker();
		if (@event.IsActionPressed("eat_food"))
			EatFood();
		if (@event.IsActionPressed("toggle_weapon"))
			LocalState.ToggleWeaponMode();
		if (@event.IsActionPressed("toggle_combat"))
			LocalState.ToggleCombatMode();
	}

	// ── Class announcement ────────────────────────────────────────────────────

	private void AnnounceClass()
	{
		var hs = GetNodeOrNull(HEALTH_SYSTEM_PATH);
		if (hs == null) return;
		// Host (IsServer) calls directly; remote client sends to peer 1 (server).
		if (Multiplayer.IsServer())
			hs.Call("RequestSetClass", GameSession.ChosenClassId);
		else
			hs.RpcId(1, "RequestSetClass", GameSession.ChosenClassId);
	}

	// ── Movement ─────────────────────────────────────────────────────────────

	private void ProcessLocalPlayer(float delta)
	{
		ApplyGravity(delta);
		var input = GetInputVector();
		Velocity = new Vector3(input.X * SPEED, Velocity.Y, input.Y * SPEED);
		MoveAndSlide();

		if (_hasServerCorrection)
		{
			var drift = GlobalPosition.DistanceTo(_serverPosition);
			if (drift > CORRECTION_THRESHOLD)
				GlobalPosition = GlobalPosition.Lerp(_serverPosition, CORRECTION_LERP);
			_hasServerCorrection = false;
		}

		if (_tickCount % BROADCAST_INTERVAL != 0 || Multiplayer.MultiplayerPeer == null) return;

		if (Multiplayer.IsServer())
			Rpc(MethodName.UpdateState, GlobalPosition);
		else
			RpcId(1, MethodName.ReceiveInput, input);
	}

	private void ProcessRemoteOnServer(float delta)
	{
		ApplyGravity(delta);
		Velocity = new Vector3(_bufferedInput.X * SPEED, Velocity.Y, _bufferedInput.Y * SPEED);
		MoveAndSlide();

		if (_tickCount % BROADCAST_INTERVAL == 0 && Multiplayer.MultiplayerPeer != null)
			Rpc(MethodName.UpdateState, GlobalPosition);
	}

	private void ProcessRemoteOnClient()
	{
		GlobalPosition = GlobalPosition.Lerp(_remoteTarget, REMOTE_LERP);
	}

	private void ApplyGravity(float delta)
	{
		if (!IsOnFloor())
			Velocity = new Vector3(Velocity.X, Velocity.Y - GRAVITY * delta, Velocity.Z);
	}

	private static Vector2 GetInputVector() =>
		Input.GetVector("move_left", "move_right", "move_forward", "move_back");

	// ── Settlement interaction ────────────────────────────────────────────────

	private void TryPlantMarker()
	{
		var ss = GetNodeOrNull(SETTLEMENT_SYSTEM_PATH);
		if (ss == null) return;

		if (Multiplayer.IsServer())
			ss.Call("RequestPlantMarker", GlobalPosition);
		else
			ss.RpcId(1, "RequestPlantMarker", GlobalPosition);
	}

	// ── Interact (E key) — shelter sleep or tree chop ────────────────────────

	private void TryInteract()
	{
		// Sphere overlap: mask covers terrain (1), trees (2), buildings (8),
		// berry bushes (16), and item drops (128). We distinguish by name/parent-walk.
		var spaceState = GetWorld3D().DirectSpaceState;
		var sphere     = new SphereShape3D { Radius = INTERACT_RANGE };
		var shapeQuery = new PhysicsShapeQueryParameters3D
		{
			Shape         = sphere,
			Transform     = new Transform3D(Basis.Identity, GlobalPosition),
			Exclude       = new Godot.Collections.Array<Rid> { GetRid() },
			CollisionMask = 1u | 2u | 8u | 16u | 128u
		};

		var hits = spaceState.IntersectShape(shapeQuery);

		// Priority 0: Item drop pickup — always first so death loot is recoverable.
		foreach (var hit in hits)
		{
			var collider = hit["collider"].As<Node>();
			if (collider == null) continue;
			var name = collider.Name.ToString();
			if (!name.StartsWith("ItemDrop_")) continue;
			if (!long.TryParse(name["ItemDrop_".Length..], out var dropId)) continue;
			RequestPickupDrop(dropId);
			return;
		}

		// Priority 1: Shelter → sleep.
		foreach (var hit in hits)
		{
			var collider    = hit["collider"].As<Node>();
			var buildingNode = FindBuildingNode(collider);
			if (buildingNode != null && buildingNode.Name.ToString().Contains("shelter"))
			{
				TrySleep();
				return;
			}
		}

		// Priority 2: Berry bush → harvest.
		foreach (var hit in hits)
		{
			var collider = hit["collider"].As<Node>();
			if (collider != null && collider.Name.ToString().StartsWith("bush_"))
			{
				TryHarvestBush(collider.Name);
				return;
			}
		}

		// Priority 3: Cooking Fire → cook berries.
		foreach (var hit in hits)
		{
			var collider    = hit["collider"].As<Node>();
			var buildingNode = FindBuildingNode(collider);
			if (buildingNode != null && buildingNode.Name.ToString().Contains("cooking_fire"))
			{
				TryCook();
				return;
			}
		}

		// Priority 4: Workbench → craft arrows.
		foreach (var hit in hits)
		{
			var collider    = hit["collider"].As<Node>();
			var buildingNode = FindBuildingNode(collider);
			if (buildingNode != null && buildingNode.Name.ToString().Contains("workbench"))
			{
				TryCraftArrows();
				return;
			}
		}

		// Priority 5: Tree → chop (nearest).
		Node? nearestTree = null;
		float nearestDist = float.MaxValue;
		foreach (var hit in hits)
		{
			var collider = hit["collider"].As<Node>();
			var treeNode = FindTreeNode(collider);
			if (treeNode == null) continue;

			float dist = GlobalPosition.DistanceTo(((Node3D)treeNode).GlobalPosition);
			if (dist < nearestDist)
			{
				nearestDist = dist;
				nearestTree = treeNode;
			}
		}

		if (nearestTree == null) return;

		var treeId     = nearestTree.Name;
		var treeSystem = GetNodeOrNull(TREE_SYSTEM_PATH);
		if (treeSystem == null) return;

		if (Multiplayer.IsServer())
			treeSystem.Call("ReceiveChop", treeId);
		else
			treeSystem.RpcId(1, "ReceiveChop", treeId);
	}

	private void TryHarvestBush(StringName bushId)
	{
		var bushSystem = GetNodeOrNull(BUSH_SYSTEM_PATH);
		if (bushSystem == null) return;

		if (Multiplayer.IsServer())
			bushSystem.Call("ReceiveHarvest", bushId.ToString());
		else
			bushSystem.RpcId(1, "ReceiveHarvest", bushId.ToString());
	}

	private void TryCook()
	{
		var bushSystem = GetNodeOrNull(BUSH_SYSTEM_PATH);
		if (bushSystem == null) return;

		if (Multiplayer.IsServer())
			bushSystem.Call("RequestCook");
		else
			bushSystem.RpcId(1, "RequestCook");
	}

	private void TryCraftArrows()
	{
		var combatSystem = GetNodeOrNull(COMBAT_SYSTEM_PATH);
		if (combatSystem == null) return;

		if (Multiplayer.IsServer())
			combatSystem.Call("RequestCraftArrows");
		else
			combatSystem.RpcId(1, "RequestCraftArrows");
	}

	private void RequestPickupDrop(long dropId)
	{
		var healthSystem = GetNodeOrNull(HEALTH_SYSTEM_PATH);
		if (healthSystem == null) return;

		if (Multiplayer.IsServer())
			healthSystem.Call("RequestPickupDrop", dropId);
		else
			healthSystem.RpcId(1, "RequestPickupDrop", dropId);
	}

	private void EatFood()
	{
		// Quick client-side guard: skip the RPC if we have nothing edible.
		// Mirrors FoodRegistry priority: cooked first, raw fallback.
		bool hasFood = false;
		foreach (var food in FoodRegistry.All)
		{
			if ((food.CookedItemId is not null && LocalState.Inventory.Has(food.CookedItemId)) ||
				LocalState.Inventory.Has(food.RawItemId))
			{
				hasFood = true;
				break;
			}
		}
		if (!hasFood) return;

		var bushSystem = GetNodeOrNull(BUSH_SYSTEM_PATH);
		if (bushSystem == null) return;

		if (Multiplayer.IsServer())
			bushSystem.Call("RequestEat");
		else
			bushSystem.RpcId(1, "RequestEat");
	}

	/// <summary>
	/// Walks the parent chain to find the building root node spawned by SettlementSystem.
	/// Buildings are direct children of SettlementSystem, so their parent path is
	/// /root/GameWorld/SettlementSystem/<building_node>.
	/// </summary>
	private static Node? FindBuildingNode(Node? node)
	{
		while (node != null)
		{
			var parentName = node.GetParent()?.Name.ToString() ?? "";
			if (parentName == "SettlementSystem")
				return node;
			node = node.GetParent();
		}
		return null;
	}

	private void TrySleep()
	{
		var needsSystem = GetNodeOrNull(NEEDS_SYSTEM_PATH);
		if (needsSystem == null) return;

		if (Multiplayer.IsServer())
			needsSystem.Call("RequestSleep");
		else
			needsSystem.RpcId(1, "RequestSleep");
	}

	private static Node? FindTreeNode(Node? node)
	{
		while (node != null)
		{
			if (node is TreeNode) return node;
			node = node.GetParent();
		}
		return null;
	}

	// ── RPCs ─────────────────────────────────────────────────────────────────

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
		 TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
	private void ReceiveInput(Vector2 input)
	{
		if (!Multiplayer.IsServer()) return;
		if (Multiplayer.GetRemoteSenderId() != GetMultiplayerAuthority()) return;
		_bufferedInput = input;
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false,
		 TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
	private void UpdateState(Vector3 position)
	{
		if (_isLocalPlayer)
		{
			_serverPosition = position;
			_hasServerCorrection = true;
		}
		else
		{
			_remoteTarget = position;
		}
	}

	/// <summary>
	/// Called by NeedsSystem on the server when this player dies (starved).
	/// Teleports the local player to the given respawn position.
	/// Uses AnyPeer so the server (not the authority) can invoke it remotely.
	/// </summary>
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void ForceRespawn(Vector3 position)
	{
		if (!_isLocalPlayer) return; // only execute on the owning client
		GlobalPosition       = position;
		Velocity             = Vector3.Zero;
		_hasServerCorrection = false;
	}
}
