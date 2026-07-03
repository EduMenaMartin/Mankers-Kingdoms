using Godot;
using Godot.Collections;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Client;

/// <summary>
/// Generates the terrain visual mesh at runtime from the world seed.
/// Calls TerrainGenerator directly (same seed = identical output to TerrainSystem's heightmap).
/// No server import needed — deterministic generation means no sync RPC is required.
/// </summary>
public partial class TerrainRenderer : Node
{
    private static readonly Color GrassColor = new(0.28f, 0.45f, 0.22f);

    /// <summary>
    /// Heightmap generated during _Ready. Accessible to other client-side systems
    /// (e.g. PlacementController) so they don't need to regenerate it.
    /// </summary>
    public static float[,] CachedHeightmap { get; private set; } = new float[0, 0];

    public override void _Ready()
    {
        var cfg       = TerrainConfig.Default;
        var heightmap = new TerrainGenerator(GameSession.WorldSeed, cfg).GenerateHeightmap();
        CachedHeightmap = heightmap;
        var mesh  = BuildMesh(heightmap, cfg);
        var mi    = new MeshInstance3D { Mesh = mesh };

        var mat = new StandardMaterial3D
        {
            AlbedoColor = GrassColor,
            RoughnessTextureChannel = BaseMaterial3D.TextureChannel.Red
        };
        mi.MaterialOverride = mat;

        AddChild(mi);
        GD.Print("[TerrainRenderer] terrain mesh ready");
    }

    private static ArrayMesh BuildMesh(float[,] h, TerrainConfig cfg)
    {
        int W = cfg.MapWidth;
        int D = cfg.MapHeight;
        float ts = cfg.TileSize;

        var verts   = new Vector3[W * D];
        var uvs     = new Vector2[W * D];
        var normals = new Vector3[W * D];
        var indices = new int[(W - 1) * (D - 1) * 6];

        // Vertices
        for (int x = 0; x < W; x++)
        for (int z = 0; z < D; z++)
        {
            int i = x * D + z;
            float wx = (x - (W - 1) * 0.5f) * ts;
            float wz = (z - (D - 1) * 0.5f) * ts;
            verts[i] = new Vector3(wx, h[x, z], wz);
            uvs[i]   = new Vector2((float)x / (W - 1), (float)z / (D - 1));
        }

        // Normals (cross-product of neighbours)
        for (int x = 0; x < W; x++)
        for (int z = 0; z < D; z++)
        {
            int i = x * D + z;
            var left  = x > 0     ? verts[(x - 1) * D + z] : verts[i];
            var right = x < W - 1 ? verts[(x + 1) * D + z] : verts[i];
            var back  = z > 0     ? verts[x * D + (z - 1)] : verts[i];
            var fwd   = z < D - 1 ? verts[x * D + (z + 1)] : verts[i];
            normals[i] = (right - left).Cross(fwd - back).Normalized();
        }

        // Triangles (two per quad)
        int idx = 0;
        for (int x = 0; x < W - 1; x++)
        for (int z = 0; z < D - 1; z++)
        {
            int v00 = x * D + z;
            int v10 = (x + 1) * D + z;
            int v01 = x * D + (z + 1);
            int v11 = (x + 1) * D + (z + 1);

            indices[idx++] = v00; indices[idx++] = v10; indices[idx++] = v01;
            indices[idx++] = v10; indices[idx++] = v11; indices[idx++] = v01;
        }

        var arrays = new Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex]  = verts;
        arrays[(int)Mesh.ArrayType.TexUV]   = uvs;
        arrays[(int)Mesh.ArrayType.Normal]  = normals;
        arrays[(int)Mesh.ArrayType.Index]   = indices;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }
}
