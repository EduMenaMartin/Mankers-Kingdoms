using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Client;

/// <summary>
/// Handles the building ghost preview while the player is in placement mode.
/// Instantiated as a child of the local player node by PlayerController._Ready().
///
/// Ghost is green when placement is valid (within territory, no collision with trees/buildings).
/// Ghost is red otherwise.
/// Left-click confirms placement; Escape or B cancels.
///
/// Reads TerrainRenderer.CachedHeightmap to snap ghost Y to terrain — avoids importing server/.
/// Connects to SettlementSystem.MarkerPlanted via untyped path — avoids importing server/.
/// </summary>
public partial class PlacementController : Node
{
    // ── Static interface used by BuildMenu ────────────────────────────────────

    private static event System.Action<string>? OnBuildingSelected;

    /// <summary>Called by BuildMenu to start placement of a given building type.</summary>
    public static void SelectBuilding(string buildingId) => OnBuildingSelected?.Invoke(buildingId);

    /// <summary>The active PlacementController instance for the local player, if any.</summary>
    public static PlacementController? Current { get; private set; }

    // ── Instance state ────────────────────────────────────────────────────────

    private Camera3D _camera = null!;
    private BuildingData? _building;
    private Node3D? _ghost;
    private MeshInstance3D? _ghostMesh;
    private Node3D? _territoryRing;
    private Vector3? _markerPos;
    private bool _isValid;

    private StandardMaterial3D _matOk      = null!;
    private StandardMaterial3D _matBlocked = null!;

    private const string SETTLEMENT_PATH     = "/root/GameWorld/SettlementSystem";
    private const float  GRID                = 1f;   // 1-unit snap
    private const float  TERRITORY_RADIUS    = 40f;  // must match SettlementSystem.TERRITORY_RADIUS

    // Collision layer bitmasks.
    // Editor "Layer N" = bitmask 2^(N-1):  Layer1=1, Layer2=2, Layer3=4, Layer4=8
    private const uint LAYER_TERRAIN   = 1u;   // terrain StaticBody3D
    private const uint LAYER_TREES     = 2u;   // tree StaticBody3D (set in tree.tscn editor)
    private const uint LAYER_BUILDINGS = 8u;   // placed buildings (editor layer 4)
    private const uint LAYER_BLOCKERS  = LAYER_TREES | LAYER_BUILDINGS;  // = 10

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        Current = this;
        _camera = GetParent().GetNode<Camera3D>("Camera3D");

        _matOk = new StandardMaterial3D
        {
            AlbedoColor  = new Color(0f, 1f, 0f, 0.45f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode     = BaseMaterial3D.CullModeEnum.Disabled
        };
        _matBlocked = new StandardMaterial3D
        {
            AlbedoColor  = new Color(1f, 0f, 0f, 0.45f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode     = BaseMaterial3D.CullModeEnum.Disabled
        };

        OnBuildingSelected += StartPlacement;

        // Connect to SettlementSystem signal without importing server/ namespace.
        var ss = GetNodeOrNull(SETTLEMENT_PATH);
        ss?.Connect("MarkerPlanted", new Callable(this, MethodName.OnMarkerPlanted));
    }

    public override void _ExitTree()
    {
        if (Current == this) Current = null;
        OnBuildingSelected -= StartPlacement;
        _ghost?.QueueFree();
        _territoryRing?.QueueFree();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Cancel() => CancelPlacement();

    // ── Signal receiver (untyped connect from SettlementSystem) ──────────────

    private void OnMarkerPlanted(long peerId, Vector3 position)
    {
        if (peerId != Multiplayer.GetUniqueId()) return;
        _markerPos = position;
        // Refresh ring if placement was already active when marker was planted.
        if (_ghost != null)
        {
            _territoryRing?.QueueFree();
            _territoryRing = CreateTerritoryRing(position);
        }
    }

    // ── Placement lifecycle ───────────────────────────────────────────────────

    private void StartPlacement(string buildingId)
    {
        var data = BuildingRegistry.Find(buildingId);
        if (data == null) return;

        CancelPlacement(); // destroy any existing ghost first
        _building = data;
        CreateGhost();
        if (_markerPos.HasValue)
            _territoryRing = CreateTerritoryRing(_markerPos.Value);
    }

    private void CancelPlacement()
    {
        _ghost?.QueueFree();
        _ghost     = null;
        _ghostMesh = null;
        _building  = null;
        _territoryRing?.QueueFree();
        _territoryRing = null;
    }

    private Node3D CreateTerritoryRing(Vector3 centre)
    {
        var mesh = new ImmediateMesh();
        const int SEGMENTS = 72;

        mesh.SurfaceBegin(Mesh.PrimitiveType.LineStrip);
        for (int i = 0; i <= SEGMENTS; i++)
        {
            float angle = i * Mathf.Tau / SEGMENTS;
            mesh.SurfaceAddVertex(new Vector3(
                Mathf.Cos(angle) * TERRITORY_RADIUS,
                0.15f,   // slightly above ground
                Mathf.Sin(angle) * TERRITORY_RADIUS
            ));
        }
        mesh.SurfaceEnd();

        var mat = new StandardMaterial3D
        {
            AlbedoColor  = new Color(0.2f, 0.8f, 1f, 0.9f),
            ShadingMode  = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            NoDepthTest  = true
        };
        mesh.SurfaceSetMaterial(0, mat);

        var mi = new MeshInstance3D { Mesh = mesh };
        var root = new Node3D();
        root.AddChild(mi);
        GetTree().GetCurrentScene().AddChild(root);
        root.GlobalPosition = centre;
        return root;
    }

    private void CreateGhost()
    {
        var go = new Node3D();
        GetTree().GetCurrentScene().AddChild(go);

        _ghostMesh = new MeshInstance3D
        {
            Mesh             = new BoxMesh { Size = new Vector3(_building!.Width, _building.Height, _building.Depth) },
            MaterialOverride = _matOk,
            // Raise mesh so its bottom sits at the node origin (terrain surface).
            Position         = new Vector3(0f, _building.Height * 0.5f, 0f)
        };
        go.AddChild(_ghostMesh);

        _ghost = go;
    }

    // ── Per-frame update ──────────────────────────────────────────────────────

    public override void _Process(double delta)
    {
        if (_ghost == null || _building == null) return;
        UpdateGhost();
    }

    private void UpdateGhost()
    {
        // Ray from camera through mouse cursor → terrain surface.
        var mouse = GetViewport().GetMousePosition();
        var from  = _camera.ProjectRayOrigin(mouse);
        var to    = from + _camera.ProjectRayNormal(mouse) * 500f;

        var space = GetViewport().GetWorld3D().DirectSpaceState;
        var ray   = PhysicsRayQueryParameters3D.Create(from, to);
        ray.CollisionMask = LAYER_TERRAIN;
        var hit = space.IntersectRay(ray);
        if (hit.Count == 0) return;

        var hitPos = (Vector3)hit["position"];

        // Snap X and Z to grid; sample terrain for accurate Y at snapped position.
        float sx = Mathf.Round(hitPos.X / GRID) * GRID;
        float sz = Mathf.Round(hitPos.Z / GRID) * GRID;
        float sy = SampleTerrainHeight(sx, sz);

        var snapped = new Vector3(sx, sy, sz);
        _ghost.GlobalPosition = snapped;

        // Validate.
        bool overlaps   = CheckOverlap(snapped);
        bool inTerritory = _markerPos.HasValue &&
                           snapped.DistanceTo(_markerPos.Value) <= TERRITORY_RADIUS;

        // No marker yet → force red with a hint.
        _isValid = !overlaps && inTerritory;
        _ghostMesh!.MaterialOverride = _isValid ? _matOk : _matBlocked;
    }


    private float SampleTerrainHeight(float worldX, float worldZ)
    {
        var h = TerrainRenderer.CachedHeightmap;
        if (h.Length == 0) return 0f;

        var cfg   = TerrainConfig.Default;
        float halfW = (cfg.MapWidth  - 1) * cfg.TileSize * 0.5f;
        float halfH = (cfg.MapHeight - 1) * cfg.TileSize * 0.5f;
        float gx    = (worldX + halfW) / cfg.TileSize;
        float gz    = (worldZ + halfH) / cfg.TileSize;

        int x0 = System.Math.Clamp((int)gx, 0, cfg.MapWidth  - 2);
        int z0 = System.Math.Clamp((int)gz, 0, cfg.MapHeight - 2);
        float fx = gx - x0, fz = gz - z0;

        float h00 = h[x0,     z0],     h10 = h[x0 + 1, z0];
        float h01 = h[x0,     z0 + 1], h11 = h[x0 + 1, z0 + 1];
        return h00 + (h10 - h00) * fx + (h01 - h00) * fz + (h11 - h10 - h01 + h00) * fx * fz;
    }

    private bool CheckOverlap(Vector3 pos)
    {
        var space = GetViewport().GetWorld3D().DirectSpaceState;
        var box   = new BoxShape3D
        {
            // Slightly smaller than footprint to avoid false positives at grid edges.
            Size = new Vector3(_building!.Width - 0.2f, _building.Height, _building.Depth - 0.2f)
        };
        var query = new PhysicsShapeQueryParameters3D
        {
            Shape         = box,
            Transform     = new Transform3D(Basis.Identity, pos + Vector3.Up * (_building.Height * 0.5f)),
            CollisionMask = LAYER_BLOCKERS
        };
        return space.IntersectShape(query).Count > 0;
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_ghost == null) return;

        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            if (_isValid) ConfirmPlacement();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event.IsActionPressed("ui_cancel"))
        {
            CancelPlacement();
            GetViewport().SetInputAsHandled();
        }
    }

    private void ConfirmPlacement()
    {
        if (_ghost == null || _building == null) return;

        var pos = _ghost.GlobalPosition;
        var ss  = GetNodeOrNull(SETTLEMENT_PATH);
        if (ss == null) return;

        if (Multiplayer.IsServer())
            ss.Call("RequestPlaceBuilding", _building.Id, pos);
        else
            ss.RpcId(1, "RequestPlaceBuilding", _building.Id, pos);

        CancelPlacement();
    }

}
