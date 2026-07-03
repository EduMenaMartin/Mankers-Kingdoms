using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Server;

/// <summary>
/// Generates the authoritative HeightMapShape3D collision mesh at runtime from the world seed.
/// Runs on ALL peers (server and client both load GameWorld.tscn), but collision is only
/// meaningful on the server side — Godot physics runs on both, so having it on clients too
/// lets CharacterBody3D walk on the terrain locally without a physics RPC round-trip.
///
/// Both TerrainSystem and TerrainRenderer call TerrainGenerator with the same seed so their
/// outputs are identical. No terrain data is sent over the network.
/// </summary>
public partial class TerrainSystem : Node
{
	private static TerrainConfig _cfg = TerrainConfig.Default;

	// Exposed so TerrainRenderer and TreeSystem can read the generated data.
	public static float[,] Heightmap { get; private set; } = new float[0, 0];

	public override void _Ready()
	{
		_cfg = TerrainConfig.Default;
		Heightmap = new TerrainGenerator(GameSession.WorldSeed, _cfg).GenerateHeightmap();

		// In Godot 4 C# bindings, HeightMapShape3D.MapData is float[], not PackedFloat32Array.
		var mapData = new float[_cfg.MapWidth * _cfg.MapHeight];
		for (int z = 0; z < _cfg.MapHeight; z++)
		for (int x = 0; x < _cfg.MapWidth;  x++)
			mapData[z * _cfg.MapWidth + x] = Heightmap[x, z];

		var shape = new HeightMapShape3D
		{
			MapWidth  = _cfg.MapWidth,
			MapDepth  = _cfg.MapHeight,
			MapData   = mapData
		};

		var collision = new CollisionShape3D { Shape = shape };

		// Scale X/Z so grid spacing matches TileSize world units.
		// Y scale stays 1 so height values are already in world units.
		// Layer 1 = terrain. Trees use layer 2, buildings layer 4.
		// PlacementController raycasts on layer 1 only to find terrain height.
		var body = new StaticBody3D
		{
			Scale          = new Vector3(_cfg.TileSize, 1f, _cfg.TileSize),
			CollisionLayer = 1,
			CollisionMask  = 1
		};
		body.AddChild(collision);
		AddChild(body);

		GD.Print($"[TerrainSystem] heightmap ready {_cfg.MapWidth}×{_cfg.MapHeight}, " +
				 $"tileSize={_cfg.TileSize}");
	}

	/// <summary>World-space Y at grid cell (gridX, gridZ). Clamps to map bounds.</summary>
	public static float GetHeightAt(int gridX, int gridZ)
	{
		if (Heightmap.Length == 0) return 0f;
		int x = System.Math.Clamp(gridX, 0, _cfg.MapWidth  - 1);
		int z = System.Math.Clamp(gridZ, 0, _cfg.MapHeight - 1);
		return Heightmap[x, z];
	}

	/// <summary>Converts world XZ position to approximate terrain height via bilinear sample.</summary>
	public static float GetHeightAtWorld(float worldX, float worldZ)
	{
		if (Heightmap.Length == 0) return 0f;
		float halfW = (_cfg.MapWidth  - 1) * _cfg.TileSize * 0.5f;
		float halfH = (_cfg.MapHeight - 1) * _cfg.TileSize * 0.5f;
		float gx = (worldX + halfW) / _cfg.TileSize;
		float gz = (worldZ + halfH) / _cfg.TileSize;

		int x0 = System.Math.Clamp((int)gx, 0, _cfg.MapWidth  - 2);
		int z0 = System.Math.Clamp((int)gz, 0, _cfg.MapHeight - 2);
		float fx = gx - x0;
		float fz = gz - z0;

		float h00 = Heightmap[x0,     z0];
		float h10 = Heightmap[x0 + 1, z0];
		float h01 = Heightmap[x0,     z0 + 1];
		float h11 = Heightmap[x0 + 1, z0 + 1];

		return h00 + (h10 - h00) * fx + (h01 - h00) * fz + (h11 - h10 - h01 + h00) * fx * fz;
	}
}
