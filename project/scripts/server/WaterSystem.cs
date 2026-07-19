using System;
using System.Collections.Generic;
using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Server;

/// <summary>
/// Builds the river ribbon mesh and "player is in water" Area3D from the RiverData generated
/// by TerrainSystem. Runs on ALL peers — no RPC, same deterministic data on every client.
///
/// Ribbon mesh:
///   Per-segment quads, two triangles each. UV.x = [0,1] across width, UV.y = arc-length
///   fraction [0,1] along flow direction. Tangent = per-vertex flow direction; required for
///   water_river.gdshader's NORMAL_MAP to transform normals correctly.
///
/// Material:
///   Stub: StandardMaterial3D blue, unshaded — replace with a ShaderMaterial pointing to
///   res://assets/shaders/water_river.gdshader once Edu has authored that file (see
///   docs/gdd/water.md §4 for the full shader spec).
///
/// Collision:
///   Area3D "WaterTrigger" with per-segment BoxShape3D volumes oriented along flow. The
///   server subscribes to body_entered — stub only logs; future: emit "player entered water"
///   signal to PlayerController for swim state / speed debuff.
///
/// Node must appear in GameWorld.tscn AFTER TerrainSystem.
/// </summary>
public partial class WaterSystem : Node
{
    public static WaterSystem Instance { get; private set; } = null!;

    public override void _Ready()
    {
        Instance = this;

        var river = TerrainSystem.River;
        if (river == null || river.Segments.Count < 2)
        {
            GD.PrintErr("[WaterSystem] No valid river data — add TerrainSystem before WaterSystem, " +
                        "or the generated river path was degenerate (< 2 segments).");
            return;
        }

        BuildRibbonMesh(river.Segments);
        BuildCollisionArea(river.Segments);

        GD.Print($"[WaterSystem] river built: {river.Segments.Count} segments");
    }

    // ── Ribbon mesh ───────────────────────────────────────────────────────────

    private void BuildRibbonMesh(IReadOnlyList<RiverSegment> segs)
    {
        int n = segs.Count;

        var verts    = new Vector3[n * 2];
        var norms    = new Vector3[n * 2];
        var tangents = new float[n * 2 * 4]; // 4 floats per vertex: (x, y, z, w)
        var uvs      = new Vector2[n * 2];
        var indices  = new int[(n - 1) * 6]; // 2 triangles per quad

        // Compute total arc length for UV.y normalisation.
        float totalLen = 0f;
        for (int i = 1; i < n; i++)
        {
            float dx = segs[i].WorldX - segs[i - 1].WorldX;
            float dz = segs[i].WorldZ - segs[i - 1].WorldZ;
            totalLen += MathF.Sqrt(dx * dx + dz * dz);
        }
        if (totalLen < 0.001f) totalLen = 1f;

        float arcLen = 0f;
        for (int i = 0; i < n; i++)
        {
            if (i > 0)
            {
                float dx = segs[i].WorldX - segs[i - 1].WorldX;
                float dz = segs[i].WorldZ - segs[i - 1].WorldZ;
                arcLen += MathF.Sqrt(dx * dx + dz * dz);
            }

            var s = segs[i];

            // Normal to tangent in XZ plane (left-hand rule → left bank at lower X/Z)
            float nx = -s.TangentZ;
            float nz =  s.TangentX;

            int li = i * 2;     // left vertex index
            int ri = i * 2 + 1; // right vertex index

            verts[li] = new Vector3(s.WorldX - nx * s.HalfWidthM, s.WaterY, s.WorldZ - nz * s.HalfWidthM);
            verts[ri] = new Vector3(s.WorldX + nx * s.HalfWidthM, s.WaterY, s.WorldZ + nz * s.HalfWidthM);

            norms[li] = Vector3.Up;
            norms[ri] = Vector3.Up;

            // Tangent: flow direction (TangentX, 0, TangentZ), w=1 (no binormal flip).
            // Required for water_river.gdshader NORMAL_MAP to transform correctly.
            int tb = li * 4;
            tangents[tb]     = s.TangentX; tangents[tb + 1] = 0f;
            tangents[tb + 2] = s.TangentZ; tangents[tb + 3] = 1f;
            tb = ri * 4;
            tangents[tb]     = s.TangentX; tangents[tb + 1] = 0f;
            tangents[tb + 2] = s.TangentZ; tangents[tb + 3] = 1f;

            // UV: x across width, y along arc-length (downstream direction for shader scroll).
            float v = arcLen / totalLen;
            uvs[li] = new Vector2(0f, v);
            uvs[ri] = new Vector2(1f, v);

            // Two CCW triangles per quad (viewed from above, normal = +Y).
            if (i < n - 1)
            {
                int b = i * 6;
                indices[b]     = li;
                indices[b + 1] = li + 2;
                indices[b + 2] = ri;
                indices[b + 3] = ri;
                indices[b + 4] = li + 2;
                indices[b + 5] = ri + 2;
            }
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex]  = verts;
        arrays[(int)Mesh.ArrayType.Normal]  = norms;
        arrays[(int)Mesh.ArrayType.Tangent] = tangents;
        arrays[(int)Mesh.ArrayType.TexUV]   = uvs;
        arrays[(int)Mesh.ArrayType.Index]   = indices;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

        // Load the water shader. The mesh is built entirely at runtime so material
        // changes made in the editor's Remote tree don't persist — we must assign it here.
        const string SHADER_PATH = "res://shaders/water_river.gdshader";
        var shader = GD.Load<Shader>(SHADER_PATH);
        if (shader != null)
        {
            mesh.SurfaceSetMaterial(0, new ShaderMaterial { Shader = shader });
        }
        else
        {
            // Fallback: solid blue until the shader file exists at the expected path.
            GD.PrintErr($"[WaterSystem] Shader not found at {SHADER_PATH} — using flat blue stub");
            mesh.SurfaceSetMaterial(0, new StandardMaterial3D
            {
                AlbedoColor  = new Color(0.12f, 0.45f, 0.75f, 0.82f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode  = BaseMaterial3D.ShadingModeEnum.Unshaded
            });
        }

        var mi = new MeshInstance3D { Mesh = mesh, Name = "RiverMesh" };
        AddChild(mi);
    }

    // ── Area3D collision ("player is in water") ───────────────────────────────

    private void BuildCollisionArea(IReadOnlyList<RiverSegment> segs)
    {
        // Layer 64 (bit 6) = water trigger, distinct from terrain (1), trees (2), buildings (4/8).
        var area = new Area3D
        {
            Name           = "WaterTrigger",
            CollisionLayer = 64u,
            CollisionMask  = 0xFFFFu // detect all physics bodies (tune post-M10)
        };

        float tileSize = TerrainConfig.Default.TileSize;

        for (int i = 0; i < segs.Count; i++)
        {
            var s = segs[i];

            // Pivot handles rotation so the box aligns with the flow direction.
            var pivot = new Node3D
            {
                Position = new Vector3(s.WorldX, s.WaterY - 1.5f, s.WorldZ),
                Rotation = new Vector3(0f, MathF.Atan2(s.TangentX, s.TangentZ), 0f)
            };

            // Width = full river width, height = 5 m (catches player in channel below surface),
            // depth = one tile in the flow direction.
            var box = new BoxShape3D
            {
                Size = new Vector3(s.HalfWidthM * 2f, 5f, tileSize)
            };
            pivot.AddChild(new CollisionShape3D { Shape = box });
            area.AddChild(pivot);
        }

        AddChild(area);

        // Server-only: fire the body_entered signal. Clients don't need to respond.
        if (Multiplayer.IsServer())
            area.BodyEntered += OnBodyEnteredWater;
    }

    private void OnBodyEnteredWater(Node3D body)
    {
        // Stub: log entry. Future (post-M10 BACKLOG): send "entered water" signal to
        // PlayerController for swim state, movement penalty, and drowning logic.
        GD.Print($"[WaterSystem] {body.Name} entered river");
    }
}
