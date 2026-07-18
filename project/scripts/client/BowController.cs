using System.Collections.Generic;
using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Client;

/// <summary>
/// Handles ranged input for the local player.
/// Instantiated as a child of the local player node by PlayerController._Ready().
///
/// LMB click → compute aim direction from mouse screen position → RequestFireProjectile RPC.
///
/// Also manages arrow ghost meshes on all peers:
///   LocalState.ArrowSpawned  → spawn a small sphere mesh at the arrow origin
///   LocalState.ArrowRemoved  → free that mesh (hit or expired)
///
/// Ghost movement (position + gravity) is simulated locally in _Process,
/// matching the server's physics without additional RPCs.
///
/// Client-side fire cooldown prevents spamming the server RPC; server re-validates.
/// </summary>
public partial class BowController : Node
{
    private const string PROJECTILE_SYSTEM_PATH = "/root/GameWorld/ProjectileSystem";
    private const float  GRAVITY               = 9.8f;

    private double _fireTimer; // counts down; while > 0, bow is on cooldown

    // Ghost meshes for all in-flight arrows (local and remote).
    private readonly Dictionary<long, Node3D>   _ghosts           = new();
    private readonly Dictionary<long, Vector3>  _ghostVelocities  = new();

    // Object pool for arrow ghost nodes — avoids per-shot instantiation/QueueFree overhead.
    // TODO: swap CreateGhostMesh() for Arrow.tscn instances when the editor task lands.
    private const int POOL_SIZE = 16;
    private readonly List<MeshInstance3D>  _pool     = new();
    private readonly Stack<MeshInstance3D> _inactive = new();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        LocalState.ArrowSpawned += OnArrowSpawned;
        LocalState.ArrowRemoved += OnArrowRemoved;

        var scene = GetTree().CurrentScene;
        for (int i = 0; i < POOL_SIZE; i++)
        {
            var ghost = CreateGhostMesh();
            ghost.Visible = false;
            scene.AddChild(ghost);
            _pool.Add(ghost);
            _inactive.Push(ghost);
        }
    }

    public override void _ExitTree()
    {
        LocalState.ArrowSpawned -= OnArrowSpawned;
        LocalState.ArrowRemoved -= OnArrowRemoved;

        foreach (var node in _pool)
            if (IsInstanceValid(node)) node.QueueFree();

        _pool.Clear();
        _inactive.Clear();
        _ghosts.Clear();
        _ghostVelocities.Clear();
    }

    // ── Update ────────────────────────────────────────────────────────────────

    public override void _Process(double delta)
    {
        if (_fireTimer > 0) _fireTimer -= delta;

        // Simulate ghost projectile physics locally (position + gravity).
        float dt = (float)delta;
        foreach (var id in new List<long>(_ghosts.Keys))
        {
            if (!_ghosts.TryGetValue(id, out var ghost)) continue;
            if (!IsInstanceValid(ghost))
            {
                _ghosts.Remove(id);
                _ghostVelocities.Remove(id);
                continue;
            }

            if (!_ghostVelocities.TryGetValue(id, out var vel)) continue;
            vel.Y -= GRAVITY * dt;
            _ghostVelocities[id]  = vel;
            ghost.GlobalPosition += vel * dt;
        }
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            if (TryFireBow())
                GetViewport().SetInputAsHandled();
        }
    }

    // ── Fire ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if the fire was dispatched so _UnhandledInput can mark it handled.
    /// Returns false in three cases (passes LMB through to lower-priority handlers):
    ///   • placement mode is active (PlacementController needs the click)
    ///   • no ranged weapon in inventory
    ///   • player is in melee mode (!PreferRanged) AND also holds a melee weapon
    /// </summary>
    private bool TryFireBow()
    {
        // Only fire in combat mode — build mode owns LMB for placement.
        if (!LocalState.InCombatMode) return false;

        // Never intercept LMB while the building placement ghost is up.
        if (PlacementController.Current?.IsPlacing == true) return false;

        // §15: mutual exclusivity — cannot fire while holding block.
        if (LocalState.IsBlocking) return false;

        var weaponId = GetEquippedRangedWeapon();
        if (weaponId == null) return false;

        // If a melee weapon is also present, only fire in ranged mode.
        if (!LocalState.PreferRanged && HasMeleeWeapon()) return false;

        if (_fireTimer > 0) return true; // has bow but still cooling — consume the event

        var weapon = WeaponRegistry.Find(weaponId)!;
        var player = GetParent<CharacterBody3D>();
        if (player == null) return false;

        var camera = player.GetNodeOrNull<Camera3D>("Camera3D");
        if (camera == null) return false;

        var dir = ComputeAimDirection(camera, player);
        if (dir == null) return false;

        var origin = player.GlobalPosition + Vector3.Up;
        var ps     = GetNodeOrNull(PROJECTILE_SYSTEM_PATH);
        if (ps == null) return false;

        if (Multiplayer.IsServer())
            ps.Call("RequestFireProjectile", weaponId, origin, dir.Value);
        else
            ps.RpcId(1, "RequestFireProjectile", weaponId, origin, dir.Value);

        LocalState.NotifyLocalArrowFired(); // triggers Throw animation on PlayerAnimator
        _fireTimer = weapon.SwingCooldown;
        return true;
    }

    // ── Ghost management ──────────────────────────────────────────────────────

    private void OnArrowSpawned(long id,
        float ox, float oy, float oz,
        float dx, float dy, float dz,
        float speed)
    {
        var origin   = new Vector3(ox, oy, oz);
        var velocity = new Vector3(dx, dy, dz) * speed;

        MeshInstance3D ghost;
        if (_inactive.Count > 0)
        {
            ghost = _inactive.Pop();
        }
        else
        {
            GD.PrintErr("[BowController] arrow pool exhausted — instantiating fallback ghost");
            ghost = CreateGhostMesh();
            GetTree().CurrentScene.AddChild(ghost);
            _pool.Add(ghost);
        }

        // Full state reset so no leftover position/rotation from a prior flight survives reuse.
        ghost.GlobalPosition = origin;
        ghost.Rotation       = Vector3.Zero;
        ghost.Visible        = true;

        _ghosts[id]          = ghost;
        _ghostVelocities[id] = velocity;
    }

    private void OnArrowRemoved(long id)
    {
        if (_ghosts.Remove(id, out var ghost) && IsInstanceValid(ghost))
        {
            ghost.Visible = false;
            _inactive.Push((MeshInstance3D)ghost);
        }

        _ghostVelocities.Remove(id);
    }

    // Small sphere placeholder — Arrow.tscn (editor task) replaces this in a later pass.
    // When that lands, swap this to GD.Load<PackedScene>("res://scenes/Arrow.tscn").Instantiate<Node3D>()
    // and update _pool / _inactive types accordingly.
    private static MeshInstance3D CreateGhostMesh() =>
        new() { Mesh = new SphereMesh { Radius = 0.08f, Height = 0.16f } };

    // ── Utilities ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the equipped ranged weapon ID.
    /// Reads LocalState.EquippedMainHand first (M8 equipment slot system).
    /// Falls back to inventory scan for saves predating the equipment system.
    /// </summary>
    private static string? GetEquippedRangedWeapon()
    {
        var equipped = LocalState.EquippedMainHand;
        if (equipped != null)
        {
            var w = WeaponRegistry.Find(equipped);
            if (w != null && w.IsRanged) return equipped;
        }

        // Legacy fallback: pre-equipment-slot saves have no EquippedMainHand set.
        foreach (var w in WeaponRegistry.All)
        {
            if (w.IsRanged && LocalState.Inventory.Has(w.Id))
                return w.Id;
        }
        return null;
    }

    private static bool HasMeleeWeapon()
    {
        var equipped = LocalState.EquippedMainHand;
        if (equipped != null)
        {
            var w = WeaponRegistry.Find(equipped);
            if (w != null && !w.IsRanged) return true;
        }

        foreach (var w in WeaponRegistry.All)
            if (!w.IsRanged && LocalState.Inventory.Has(w.Id)) return true;
        return false;
    }

    /// <summary>
    /// Projects the mouse screen position onto a horizontal plane at arrow-origin height
    /// and returns the normalised direction from the arrow origin to that world point.
    /// Works for any camera angle but is most intuitive for top-down / high-angle views.
    /// </summary>
    private static Vector3? ComputeAimDirection(Camera3D camera, CharacterBody3D player)
    {
        var mousePos  = camera.GetViewport().GetMousePosition();
        var rayDir    = camera.ProjectRayNormal(mousePos);
        var rayOrigin = camera.GlobalPosition;

        // Intersect a horizontal plane at the arrow's Y (player eye level).
        float  arrowY     = player.GlobalPosition.Y + 1f;
        var    aimPlane   = new Plane(Vector3.Up, arrowY);
        var    worldPoint = aimPlane.IntersectsRay(rayOrigin, rayDir);
        if (worldPoint == null) return null;

        var arrowOrigin = player.GlobalPosition + Vector3.Up;
        var dir         = worldPoint.Value - arrowOrigin;
        if (dir.LengthSquared() < 0.01f) return null;
        return dir.Normalized();
    }
}
