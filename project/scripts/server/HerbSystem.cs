using System.Collections.Generic;
using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Server;

/// <summary>
/// Manages healing herb patches on the map.
///
/// Herb nodes are spawned deterministically in _Ready() on ALL peers from the same seed —
/// identical to BushSystem — so no spawn RPC is needed.
///
/// Server-side NPC API (called by VillageSystem):
///   GetAvailableHerbPatchIds() — list of patch IDs not on harvest cooldown
///   GetHerbPosition(id)        — world position of a patch
///   IsAvailable(id)            — true if patch is not on cooldown
///   ForagerHarvestHerb(id)     — marks patch on cooldown, returns herbs yielded (1)
///
/// Player harvest is NOT implemented here — players pick up herbs by interacting with
/// the patch node (handled by a future PlayerController interaction branch).
///
/// Node must appear in GameWorld.tscn AFTER TerrainSystem.
/// </summary>
public partial class HerbSystem : Node
{
    public static HerbSystem Instance { get; private set; } = null!;

    private const float HARVEST_COOLDOWN = 60f;   // longer than bush — herbs are rarer
    private const uint  HERB_LAYER       = 32u;   // editor Layer 6

    // Shared: both server and clients populate from deterministic generation.
    private readonly Dictionary<string, StaticBody3D> _herbNodes = new();
    private readonly Dictionary<string, Vector3>      _positions  = new();

    // Server-only: harvest cooldowns.
    private readonly SortedDictionary<string, double> _cooldowns = new();
    private double _elapsed;

    public override void _Ready()
    {
        Instance = this;

        var cfg       = TerrainConfig.Default;
        var gen       = new TerrainGenerator(GameSession.WorldSeed, cfg);
        var heightmap = gen.GenerateHeightmap();
        var patches   = new HerbGenerator(GameSession.WorldSeed, cfg).Generate(heightmap);

        foreach (var p in patches)
            SpawnHerbNode(p);
    }

    private void SpawnHerbNode(HerbPatchData p)
    {
        var body = new StaticBody3D
        {
            Name           = $"herb_{p.Index}",
            CollisionLayer = HERB_LAYER,
            CollisionMask  = 0u
        };

        var shape = new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.4f } };
        body.AddChild(shape);

        // Purple-tinted sphere to distinguish herbs from green berry bushes.
        var mat = new StandardMaterial3D { AlbedoColor = new Color(0.6f, 0.15f, 0.7f) };
        var mi  = new MeshInstance3D
        {
            Mesh             = new SphereMesh { Radius = 0.3f, Height = 0.6f },
            MaterialOverride = mat
        };
        body.AddChild(mi);

        AddChild(body);
        var worldPos = new Vector3(p.WorldX, p.WorldY, p.WorldZ);
        body.GlobalPosition = worldPos;

        _herbNodes[body.Name] = body;
        _positions[body.Name] = worldPos;
    }

    // ── Server tick: restore harvested patches ────────────────────────────────

    public override void _Process(double delta)
    {
        if (!Multiplayer.IsServer()) return;

        _elapsed += delta;
        var toRestore = new List<string>();
        foreach (var (id, restoreAt) in _cooldowns)
        {
            if (_elapsed >= restoreAt)
                toRestore.Add(id);
        }
        foreach (var id in toRestore)
        {
            _cooldowns.Remove(id);
            Rpc(MethodName.SetHerbVisible, id, true);
        }
    }

    // ── Server NPC API (called by VillageSystem) ──────────────────────────────

    /// <summary>Returns IDs of all herb patches that are ready to harvest.</summary>
    public IReadOnlyList<string> GetAvailableHerbPatchIds()
    {
        var list = new List<string>();
        foreach (var id in _herbNodes.Keys)
        {
            if (!_cooldowns.ContainsKey(id))
                list.Add(id);
        }
        return list;
    }

    /// <summary>Returns the world position of the patch, or Vector3.Zero if not found.</summary>
    public Vector3 GetHerbPosition(string herbId) =>
        _positions.TryGetValue(herbId, out var p) ? p : Vector3.Zero;

    /// <summary>True if the patch is ready to harvest.</summary>
    public bool IsAvailable(string herbId) => !_cooldowns.ContainsKey(herbId);

    /// <summary>
    /// Marks the patch harvested (cooldown started) and returns the herb count yielded (1).
    /// Returns 0 if the patch is already on cooldown or unknown.
    /// </summary>
    public int ForagerHarvestHerb(string herbId)
    {
        if (_cooldowns.ContainsKey(herbId)) return 0;
        if (!_herbNodes.ContainsKey(herbId)) return 0;

        _cooldowns[herbId] = _elapsed + HARVEST_COOLDOWN;
        Rpc(MethodName.SetHerbVisible, herbId, false);
        GD.Print($"[HerbSystem] NPC harvested {herbId} → +1 herb (respawn in {HARVEST_COOLDOWN}s)");
        return 1;
    }

    // ── RPC ───────────────────────────────────────────────────────────────────

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SetHerbVisible(string herbId, bool visible)
    {
        if (_herbNodes.TryGetValue(herbId, out var node))
            node.Visible = visible;
    }
}
