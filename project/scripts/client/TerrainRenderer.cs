using System.Collections.Generic;
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

    // Subdivision factor for bank-region cells: each 4 m cell becomes BANK_SUB×BANK_SUB
    // sub-quads with bilinearly interpolated heights, giving 1 m resolution at the shore.
    private const int BANK_SUB = 4;

    /// <summary>
    /// Heightmap generated during _Ready. Accessible to other client-side systems
    /// (e.g. PlacementController) so they don't need to regenerate it.
    /// </summary>
    public static float[,] CachedHeightmap { get; private set; } = new float[0, 0];

    public override void _Ready()
    {
        var cfg       = TerrainConfig.Default;
        var heightmap = new TerrainGenerator(GameSession.WorldSeed, cfg).GenerateHeightmap();

        // Apply the same river carving pass that TerrainSystem runs server-side.
        // RiverGenerator modifies the heightmap in-place — same seed = identical carved output.
        // Without this the visual mesh is uncarved while collision + the water ribbon sit in
        // a ditch below the terrain surface, causing the river to be invisible.
        // ChannelMask is retained for bank-region subdivision in BuildMesh.
        var riverGen  = new RiverGenerator(GameSession.WorldSeed, cfg);
        var riverData = riverGen.Generate(heightmap);

        CachedHeightmap = heightmap;
        var mesh  = BuildMesh(heightmap, cfg, riverData?.ChannelMask);
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

    private static ArrayMesh BuildMesh(float[,] h, TerrainConfig cfg, bool[,]? channelMask)
    {
        int W = cfg.MapWidth;
        int D = cfg.MapHeight;
        float ts = cfg.TileSize;

        var verts   = new Vector3[W * D];
        var uvs     = new Vector2[W * D];
        var normals = new Vector3[W * D];
        // Use a list: bank-region cells are skipped here and handled in a separate surface.
        var indexList = new List<int>((W - 1) * (D - 1) * 6);

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

        // Triangles (two per quad). Bank-region cells are skipped — they get finer
        // sub-quads in the second surface added by AddBankSurface.
        for (int x = 0; x < W - 1; x++)
        for (int z = 0; z < D - 1; z++)
        {
            if (IsInBankRegion(x, z, channelMask, W, D)) continue;

            int v00 = x * D + z;
            int v10 = (x + 1) * D + z;
            int v01 = x * D + (z + 1);
            int v11 = (x + 1) * D + (z + 1);

            indexList.Add(v00); indexList.Add(v10); indexList.Add(v01);
            indexList.Add(v10); indexList.Add(v11); indexList.Add(v01);
        }

        var arrays = new Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex]  = verts;
        arrays[(int)Mesh.ArrayType.TexUV]   = uvs;
        arrays[(int)Mesh.ArrayType.Normal]  = normals;
        arrays[(int)Mesh.ArrayType.Index]   = indexList.ToArray();

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

        // Second surface: bank-region cells at BANK_SUB×BANK_SUB sub-quad resolution.
        // MaterialOverride on the MeshInstance3D covers all surfaces automatically.
        if (channelMask != null)
            AddBankSurface(mesh, h, cfg, channelMask);

        return mesh;
    }

    // ── Bank-region helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Returns true if cell (cellX, cellZ) is in the bank region: at least one of the
    /// cell's 9-cell neighbourhood (itself + 8 neighbours) is inside the channel mask.
    /// This captures both carved cells and the immediate uncarved bank alongside them.
    /// </summary>
    private static bool IsInBankRegion(int cellX, int cellZ, bool[,]? mask, int W, int D)
    {
        if (mask == null) return false;
        for (int dx = -1; dx <= 1; dx++)
        for (int dz = -1; dz <= 1; dz++)
        {
            int nx = cellX + dx, nz = cellZ + dz;
            if (nx >= 0 && nx < W && nz >= 0 && nz < D && mask[nx, nz])
                return true;
        }
        return false;
    }

    /// <summary>
    /// Bilinearly samples the heightmap at fractional grid coordinates.
    /// Clamps to valid bounds so gx/gz can safely equal W-1 / D-1.
    /// </summary>
    private static float BilerHeight(float[,] h, int W, int D, float gx, float gz)
    {
        int x0 = (int)gx;
        int z0 = (int)gz;
        if (x0 >= W - 1) x0 = W - 2;
        if (z0 >= D - 1) z0 = D - 2;
        if (x0 < 0) x0 = 0;
        if (z0 < 0) z0 = 0;
        float u = gx - x0;
        float v = gz - z0;
        return h[x0,     z0    ] * (1f - u) * (1f - v)
             + h[x0 + 1, z0    ] * u        * (1f - v)
             + h[x0,     z0 + 1] * (1f - u) * v
             + h[x0 + 1, z0 + 1] * u        * v;
    }

    /// <summary>
    /// Adds a second surface to <paramref name="mesh"/> for bank-region cells only.
    /// Each cell is subdivided into BANK_SUB×BANK_SUB sub-quads with bilinearly
    /// interpolated heights, giving 1 m visual resolution along the shore.
    ///
    /// The sub-grid corner heights match the main surface's corner vertices exactly
    /// (bilinear at u/v = 0 or 1 equals the integer-sample value), so there are no
    /// visible gaps at the boundary between the two surfaces.
    /// </summary>
    private static void AddBankSurface(ArrayMesh mesh, float[,] h, TerrainConfig cfg, bool[,] channelMask)
    {
        int   W       = cfg.MapWidth;
        int   D       = cfg.MapHeight;
        float ts      = cfg.TileSize;
        float halfW   = (W - 1) * 0.5f;
        float halfD   = (D - 1) * 0.5f;
        int   sub     = BANK_SUB + 1;  // vertices per sub-grid side (5 for BANK_SUB=4)

        var bankVerts   = new List<Vector3>();
        var bankUvs     = new List<Vector2>();
        var bankNormals = new List<Vector3>();
        var bankIndices = new List<int>();

        for (int cx = 0; cx < W - 1; cx++)
        for (int cz = 0; cz < D - 1; cz++)
        {
            if (!IsInBankRegion(cx, cz, channelMask, W, D)) continue;

            // Build (BANK_SUB+1)×(BANK_SUB+1) position sub-grid via bilinear interpolation.
            var subPos = new Vector3[sub * sub];
            var subUv  = new Vector2[sub * sub];

            for (int si = 0; si < sub; si++)
            for (int sj = 0; sj < sub; sj++)
            {
                float gx = cx + (float)si / BANK_SUB;
                float gz = cz + (float)sj / BANK_SUB;
                float wx = (gx - halfW) * ts;
                float wz = (gz - halfD) * ts;
                float wy = BilerHeight(h, W, D, gx, gz);
                subPos[si * sub + sj] = new Vector3(wx, wy, wz);
                subUv[si * sub + sj]  = new Vector2(gx / (W - 1), gz / (D - 1));
            }

            // Normals via centred differences within the sub-grid (mirrors main mesh logic).
            var subNorm = new Vector3[sub * sub];
            for (int si = 0; si < sub; si++)
            for (int sj = 0; sj < sub; sj++)
            {
                int il   = si > 0       ? (si - 1) * sub + sj : si * sub + sj;
                int ir   = si < sub - 1 ? (si + 1) * sub + sj : si * sub + sj;
                int ibk  = sj > 0       ? si * sub + (sj - 1) : si * sub + sj;
                int ifwd = sj < sub - 1 ? si * sub + (sj + 1) : si * sub + sj;
                subNorm[si * sub + sj] =
                    (subPos[ir] - subPos[il]).Cross(subPos[ifwd] - subPos[ibk]).Normalized();
            }

            // Append sub-grid to the running lists.
            int base0 = bankVerts.Count;
            for (int k = 0; k < sub * sub; k++)
            {
                bankVerts.Add(subPos[k]);
                bankUvs.Add(subUv[k]);
                bankNormals.Add(subNorm[k]);
            }

            // Emit BANK_SUB×BANK_SUB quads (2 triangles each), same winding as main surface.
            for (int si = 0; si < BANK_SUB; si++)
            for (int sj = 0; sj < BANK_SUB; sj++)
            {
                int v00 = base0 + si       * sub + sj;
                int v10 = base0 + (si + 1) * sub + sj;
                int v01 = base0 + si       * sub + (sj + 1);
                int v11 = base0 + (si + 1) * sub + (sj + 1);
                bankIndices.Add(v00); bankIndices.Add(v10); bankIndices.Add(v01);
                bankIndices.Add(v10); bankIndices.Add(v11); bankIndices.Add(v01);
            }
        }

        if (bankVerts.Count == 0) return; // degenerate river — no bank cells found

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex]  = bankVerts.ToArray();
        arrays[(int)Mesh.ArrayType.TexUV]   = bankUvs.ToArray();
        arrays[(int)Mesh.ArrayType.Normal]  = bankNormals.ToArray();
        arrays[(int)Mesh.ArrayType.Index]   = bankIndices.ToArray();

        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
    }
}
