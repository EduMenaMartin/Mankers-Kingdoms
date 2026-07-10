using Godot;

namespace MankersKingdoms.Client;

/// <summary>
/// Visual and physics representative of a village NPC on all peers.
/// Instantiated by VillageSystem; named "villager_{id_underscored}".
///
/// Reads display data from node metadata set by VillageSystem before AddChild:
///   "villager_name"      — display name shown in the Label3D
///   "archetype_name_key" — loc key for archetype subtitle (shown under name)
///
/// Server: position is set directly by VillageSystem each tick (_Process is a no-op).
/// Clients: lerp toward target position pushed via RPC (Phase 2+). In Phase 1, villagers
///          are stationary so no lerp is needed.
///
/// Collision layer 9 (bitmask 256) — distinct from terrain (1), players (32),
/// monsters (64), and item drops (128). E-key sphere query will include 256 in Phase 2.
/// </summary>
public partial class VillagerNode : CharacterBody3D
{
    private const uint  VILLAGER_LAYER = 256u;
    private const float LERP_WEIGHT    = 0.15f;

    private MeshInstance3D _mesh     = null!;
    private Vector3         _remoteTarget;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        CollisionLayer = VILLAGER_LAYER;
        CollisionMask  = 0u;

        // Collision shape
        var shape = new CapsuleShape3D { Radius = 0.4f, Height = 1.8f };
        var col   = new CollisionShape3D { Shape = shape };
        AddChild(col);

        // Teal capsule mesh — visually distinct from monsters (red/brown/green/purple)
        var mat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.15f, 0.70f, 0.65f)
        };
        var capsule = new CapsuleMesh { Radius = 0.4f, Height = 1.8f };
        _mesh = new MeshInstance3D { Mesh = capsule, MaterialOverride = mat };
        AddChild(_mesh);

        // Name label above head
        string displayName   = GetMeta("villager_name",      "Villager").AsString();
        string archetypeKey  = GetMeta("archetype_name_key", "").AsString();
        string archetypeName = Shared.Loc.T(archetypeKey);

        var label = new Label3D
        {
            Text         = $"{displayName}\n[{archetypeName}]",
            FontSize     = 20,
            Modulate     = new Color(1f, 1f, 1f),
            Billboard    = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest  = true,
            Position     = new Vector3(0f, 1.4f, 0f),
        };
        AddChild(label);

        _remoteTarget = GlobalPosition;
    }

    public override void _Process(double delta)
    {
        // Server positions are set directly — no lerp needed.
        if (Multiplayer.IsServer()) return;

        // Phase 2+ will drive _remoteTarget via RPC; for now villagers are stationary.
        GlobalPosition = GlobalPosition.Lerp(_remoteTarget, LERP_WEIGHT);
    }

    // ── Called by VillageSystem via RPC (Phase 2+) ───────────────────────────

    /// <summary>Updates the lerp target; client-side only.</summary>
    public void SetTarget(Vector3 target) => _remoteTarget = target;
}
