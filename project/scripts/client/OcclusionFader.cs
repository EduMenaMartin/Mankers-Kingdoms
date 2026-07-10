using System.Collections.Generic;
using Godot;

namespace MankersKingdoms.Client;

/// <summary>
/// Detects geometry visually occluding the camera-to-player line of sight and
/// applies a dithered ghost shader to those meshes while in the way.
///
/// Uses a pure geometry approach (AABB bounding-sphere vs segment) rather than
/// physics raycasting, so detection works correctly for the full visual extent
/// of meshes (e.g. tree canopies) regardless of how small the collision shape is.
///
/// Searches direct children of TreeSystem and SettlementSystem each check, so no
/// physics layers or collision masks are involved.
///
/// Runs at ~20 Hz (every 3rd _Process frame). O(N) where N = trees + buildings,
/// which is trivially cheap even at 50+ trees.
///
/// Instantiated as a child of the local player by PlayerController._Ready().
/// See ADR-0025.
/// </summary>
public partial class OcclusionFader : Node
{
    private const string TREES_PATH     = "/root/GameWorld/TreeSystem";
    private const string BUILDINGS_PATH = "/root/GameWorld/SettlementSystem";

    // Extra padding added to each mesh's bounding sphere radius.
    // Catches wide canopies and building overhangs that approach but don't
    // cross the exact camera→player line.
    private const float RADIUS_MARGIN   = 0.5f;

    // Exclude a small fraction at each end so meshes right behind the camera
    // or right behind the player (i.e. not really occluding) don't flicker.
    private const float T_MIN           = 0.08f;
    private const float T_MAX           = 0.92f;

    private const int   CHECK_INTERVAL  = 3;

    private CharacterBody3D _player  = null!;
    private Camera3D        _camera  = null!;
    private ShaderMaterial  _fadeMat = null!;
    private Node?           _trees;
    private Node?           _buildings;

    private readonly HashSet<MeshInstance3D> _occluding = new();
    private int _tick;

    public override void _Ready()
    {
        _player    = GetParent<CharacterBody3D>();
        _camera    = _player.GetNode<Camera3D>("Camera3D");
        _trees     = GetTree().Root.GetNodeOrNull(TREES_PATH);
        _buildings = GetTree().Root.GetNodeOrNull(BUILDINGS_PATH);

        var shader = GD.Load<Shader>("res://shaders/occlusion_fade.gdshader");
        _fadeMat   = new ShaderMaterial { Shader = shader };
    }

    public override void _Process(double delta)
    {
        if (++_tick % CHECK_INTERVAL != 0) return;

        var camPos    = _camera.GlobalPosition;
        // Aim at player chest height — more representative than feet or head alone.
        var targetPos = _player.GlobalPosition + new Vector3(0f, 1f, 0f);
        var segment   = targetPos - camPos;
        float segLen  = segment.Length();

        var newOccluders = new HashSet<MeshInstance3D>();

        if (segLen > 0.5f)
        {
            var segNorm = segment / segLen;
            CheckContainer(_trees,     camPos, segNorm, segLen, newOccluders);
            CheckContainer(_buildings, camPos, segNorm, segLen, newOccluders);
        }

        // Revert meshes that are no longer occluding.
        foreach (var mesh in _occluding)
        {
            if (!newOccluders.Contains(mesh) && IsInstanceValid(mesh))
                mesh.SetSurfaceOverrideMaterial(0, null);
        }

        // Apply shader to newly occluding meshes.
        foreach (var mesh in newOccluders)
        {
            if (!_occluding.Contains(mesh) && IsInstanceValid(mesh))
                mesh.SetSurfaceOverrideMaterial(0, _fadeMat);
        }

        _occluding.Clear();
        foreach (var mesh in newOccluders)
            _occluding.Add(mesh);
    }

    public override void _ExitTree()
    {
        foreach (var mesh in _occluding)
            if (IsInstanceValid(mesh)) mesh.SetSurfaceOverrideMaterial(0, null);
        _occluding.Clear();
    }

    // ── Detection ─────────────────────────────────────────────────────────────

    private static void CheckContainer(Node? container,
        Vector3 camPos, Vector3 segNorm, float segLen,
        HashSet<MeshInstance3D> results)
    {
        if (container == null) return;
        foreach (var child in container.GetChildren())
            CheckNode(child, camPos, segNorm, segLen, results, depth: 0);
    }

    /// <summary>
    /// Recursively walks <paramref name="node"/>'s subtree (depth cap = 4) looking
    /// for visible MeshInstance3D nodes. When found, tests whether the mesh's
    /// world-space bounding sphere overlaps the camera→player segment.
    /// </summary>
    private static void CheckNode(Node node,
        Vector3 camPos, Vector3 segNorm, float segLen,
        HashSet<MeshInstance3D> results, int depth)
    {
        if (depth > 4) return;

        if (node is MeshInstance3D mesh && IsInstanceValid(mesh) && mesh.Visible)
        {
            var localAabb    = mesh.GetAabb();
            // Transform local AABB centre to global space (handles rotation + scale).
            Vector3 gCenter  = mesh.GlobalTransform * localAabb.GetCenter();
            float   radius   = localAabb.GetLongestAxisSize() * 0.5f + RADIUS_MARGIN;

            if (Occludes(gCenter, radius, camPos, segNorm, segLen))
                results.Add(mesh);

            // MeshInstance3D nodes in this project have no MeshInstance3D children.
            return;
        }

        foreach (var child in node.GetChildren())
            CheckNode(child, camPos, segNorm, segLen, results, depth + 1);
    }

    /// <summary>
    /// Returns true if a sphere at <paramref name="center"/> with <paramref name="radius"/>
    /// intersects the camera→player segment (defined by origin, unit direction, and length).
    /// Only the portion between T_MIN and T_MAX of the segment is considered, to avoid
    /// false positives right at the camera or right at the player.
    /// </summary>
    private static bool Occludes(Vector3 center, float radius,
        Vector3 camPos, Vector3 segNorm, float segLen)
    {
        var   toCenter = center - camPos;
        float t        = toCenter.Dot(segNorm);

        if (t < segLen * T_MIN || t > segLen * T_MAX) return false;

        float perpSq = (toCenter - segNorm * t).LengthSquared();
        return perpSq < radius * radius;
    }
}
