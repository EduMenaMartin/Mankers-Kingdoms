using Godot;

namespace MankersKingdoms.Client;

/// <summary>
/// Attached to each Tree.tscn instance spawned by TreeSystem.
/// Client side only: hides the mesh when the server broadcasts OnTreeFelled via TreeSystem.
/// The chop RPC itself lives on TreeSystem (server/), which TreeInteraction logic in
/// PlayerController reaches via NodePath to avoid a client→server import dependency.
/// </summary>
public partial class TreeNode : StaticBody3D
{
    // Set by TreeSystem after AddChild so the node knows its stable ID.
    public string TreeId { get; set; } = string.Empty;

    private MeshInstance3D? _mesh;

    public override void _Ready()
    {
        _mesh = GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
    }

    /// <summary>
    /// Called by TreeSystem.OnTreeFelled via QueueFree — the whole node is removed.
    /// This method exists for any pre-fell visual (e.g. fell animation) added later.
    /// </summary>
    public void OnFelled()
    {
        // Placeholder for fell animation in a later milestone.
        // TreeSystem calls QueueFree() on this node directly for M2.
    }
}
