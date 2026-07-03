using System.Collections.Generic;
using Godot;

namespace MankersKingdoms.Server;

/// <summary>
/// Server-side player registry: spawns and despawns Player.tscn nodes as peers connect/disconnect.
/// All peers receive SpawnPlayer / DespawnPlayer RPCs to keep their scene trees in sync.
///
/// Dictionary rule (ARCHITECTURE.md): uses SortedDictionary for deterministic iteration order.
/// See ADR-0002, ADR-0005.
/// </summary>
public partial class PlayerSystem : Node
{
	private PackedScene _playerScene = null!;
	private Node3D _playersContainer = null!;

	// Server-only authoritative registry. SortedDictionary guarantees iteration order.
	private readonly SortedDictionary<long, CharacterBody3D> _players = new();

	// XZ spawn positions; Y is resolved at runtime from the terrain heightmap.
	private static readonly Vector2[] SpawnXZ =
	{
		new( 0f,  0f),
		new( 3f,  0f),
		new(-3f,  0f),
		new( 0f,  3f),
		new( 0f, -3f),
		new( 3f,  3f),
	};

	private static Vector3 SpawnPositionFor(int index)
	{
		var xz = SpawnXZ[index % SpawnXZ.Length];
		float y = TerrainSystem.GetHeightAtWorld(xz.X, xz.Y) + 0.6f;
		return new Vector3(xz.X, y, xz.Y);
	}

	public override void _Ready()
	{
		_playerScene = GD.Load<PackedScene>("res://scenes/Player.tscn");
		_playersContainer = GetNode<Node3D>("/root/GameWorld/Players");

		var net = NetworkManager.Instance;
		net.PlayerConnected += OnPlayerConnected;
		net.PlayerDisconnected += OnPlayerDisconnected;
	}

	private void OnPlayerConnected(long peerId)
	{
		if (!Multiplayer.IsServer()) return;

		var spawnPos = SpawnPositionFor(_players.Count);

		// Tell all peers (server included via CallLocal = true) to spawn the new player.
		Rpc(MethodName.SpawnPlayer, peerId, spawnPos);

		// Catch up the new peer on every player already in the world.
		foreach (var (existingId, existingNode) in _players)
			RpcId(peerId, MethodName.SpawnPlayer, existingId, existingNode.GlobalPosition);
	}

	private void OnPlayerDisconnected(long peerId)
	{
		if (!Multiplayer.IsServer()) return;
		Rpc(MethodName.DespawnPlayer, peerId);
	}

	// Authority → all peers (CallLocal = true so server also runs this).
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void SpawnPlayer(long peerId, Vector3 position)
	{
		if (_players.ContainsKey(peerId)) return; // idempotent guard

		var node = (CharacterBody3D)_playerScene.Instantiate();
		node.Name = $"Player_{peerId}";
		node.SetMultiplayerAuthority((int)peerId);
		_playersContainer.AddChild(node);   // _Ready() fires here; GlobalPosition valid after this
		node.GlobalPosition = position;
		_players[peerId] = node;

		GD.Print($"[PlayerSystem] spawned player {peerId} at {position}");
	}

	// Authority → all peers.
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void DespawnPlayer(long peerId)
	{
		if (!_players.TryGetValue(peerId, out var node)) return;
		node.QueueFree();
		_players.Remove(peerId);
		GD.Print($"[PlayerSystem] despawned player {peerId}");
	}
}
