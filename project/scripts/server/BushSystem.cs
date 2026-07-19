using System.Collections.Generic;
using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Server;

/// <summary>
/// Manages the food loop: berry bush spawning, harvesting, cooking, and eating.
///
/// Bush nodes are created programmatically in _Ready() on ALL peers from the same
/// deterministic seed — no spawn RPC required.  CollisionLayer 5 (bitmask 16) keeps
/// bushes distinct from terrain (1), trees (2), and buildings (4/8).
///
/// RPCs:
///   ReceiveHarvest  — client tells server it picked a bush → +1 berry, bush hides for 30s
///   RequestCook     — client asks to cook at a Cooking Fire → 1 berry → 1 cooked berry
///   RequestEat      — client asks to eat → consumes 1 cooked berry → +40 hunger
///
/// Node must appear in GameWorld.tscn AFTER InventorySystem and NeedsSystem.
/// </summary>
public partial class BushSystem : Node
{
    public static BushSystem Instance { get; private set; } = null!;

    private const float  HARVEST_COOLDOWN = 30f;
    private const uint   BUSH_LAYER       = 16u;  // editor Layer 5 = bitmask 16

    // Shared: both server and clients populate this from deterministic generation.
    private readonly Dictionary<string, StaticBody3D> _bushNodes = new();

    // Server-only: which bushes are on cooldown and when they recover.
    private readonly SortedDictionary<string, double> _cooldowns = new();
    private double _elapsed;

    private const string BUSH_SYSTEM_PATH = "/root/GameWorld/BushSystem";

    /// <summary>
    /// Node must appear in GameWorld.tscn AFTER TerrainSystem (carved heightmap + river mask)
    /// and TreeSystem (tree positions for clustering). Already after InventorySystem and NeedsSystem.
    /// </summary>
    public override void _Ready()
    {
        Instance = this;

        var cfg = TerrainConfig.Default;

        // Use TerrainSystem.Heightmap (post-river-carving) — do NOT regenerate the heightmap here.
        var heightmap = TerrainSystem.Heightmap;
        if (heightmap.Length == 0)
        {
            GD.PrintErr("[BushSystem] TerrainSystem.Heightmap not ready — add TerrainSystem before BushSystem.");
            return;
        }

        // Regenerate the tree list for clustering (same seed + same carved heightmap → same result
        // as TreeSystem's list; cheaper than coupling BushSystem to TreeSystem directly).
        var treeCfg = TreeConfig.Default;
        var trees   = new TreeGenerator(GameSession.WorldSeed, cfg, treeCfg)
            .Generate(heightmap, TerrainSystem.River?.ChannelMask);

        var bushes = new BushGenerator(GameSession.WorldSeed, cfg)
            .Generate(heightmap, trees, TerrainSystem.River?.ChannelMask);

        foreach (var b in bushes)
            SpawnBushNode(b);
    }

    private void SpawnBushNode(BushData b)
    {
        var body = new StaticBody3D
        {
            Name           = $"bush_{b.Index}",
            CollisionLayer = BUSH_LAYER,
            CollisionMask  = 0u
        };

        var shape = new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.6f } };
        body.AddChild(shape);

        // Simple green sphere to represent the bush visually.
        var mat = new StandardMaterial3D { AlbedoColor = new Color(0.15f, 0.55f, 0.1f) };
        var mi  = new MeshInstance3D
        {
            Mesh             = new SphereMesh { Radius = 0.45f, Height = 0.9f },
            MaterialOverride = mat
        };
        body.AddChild(mi);

        AddChild(body);
        body.GlobalPosition = new Vector3(b.WorldX, b.WorldY, b.WorldZ);
        _bushNodes[body.Name] = body;
    }

    // ── Server tick: restore harvested bushes ─────────────────────────────────

    public override void _Process(double delta)
    {
        if (!Multiplayer.IsServer()) return;

        _elapsed += delta;
        var toRestore = new List<string>();
        foreach (var (bushId, restoreAt) in _cooldowns)
        {
            if (_elapsed >= restoreAt)
                toRestore.Add(bushId);
        }
        foreach (var id in toRestore)
        {
            _cooldowns.Remove(id);
            Rpc(MethodName.SetBushVisible, id, true);
        }
    }

    // ── Server NPC API (called by VillageSystem) ──────────────────────────────

    /// <summary>Returns IDs of all berry bushes that are ready to harvest.</summary>
    public IReadOnlyList<string> GetAvailableBushIds()
    {
        var list = new List<string>();
        foreach (var id in _bushNodes.Keys)
        {
            if (!_cooldowns.ContainsKey(id))
                list.Add(id);
        }
        return list;
    }

    /// <summary>Returns the world position of the bush, or Vector3.Zero if not found.</summary>
    public Vector3 GetBushPosition(string bushId) =>
        _bushNodes.TryGetValue(bushId, out var node) ? node.GlobalPosition : Vector3.Zero;

    /// <summary>True if the bush is ready to harvest.</summary>
    public bool IsAvailable(string bushId) => !_cooldowns.ContainsKey(bushId);

    /// <summary>
    /// Marks the bush harvested (cooldown started) and returns the berry count yielded (1).
    /// Returns 0 if the bush is already on cooldown or unknown.
    /// Does NOT add berries to any player inventory — caller (VillageSystem) handles stockpile deposit.
    /// </summary>
    public int ForagerHarvestBush(string bushId)
    {
        if (_cooldowns.ContainsKey(bushId)) return 0;
        if (!_bushNodes.ContainsKey(bushId)) return 0;

        _cooldowns[bushId] = _elapsed + HARVEST_COOLDOWN;
        Rpc(MethodName.SetBushVisible, bushId, false);
        GD.Print($"[BushSystem] NPC harvested {bushId} → +1 berry (respawn in {HARVEST_COOLDOWN}s)");
        return 1;
    }

    // ── RPCs ──────────────────────────────────────────────────────────────────

    /// <summary>Client pressed E near a bush.</summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveHarvest(string bushId)
    {
        if (!Multiplayer.IsServer()) return;

        long sender = Multiplayer.GetRemoteSenderId();
        if (sender == 0) sender = 1L;

        if (_cooldowns.ContainsKey(bushId)) return;       // already depleted
        if (!_bushNodes.ContainsKey(bushId)) return;       // unknown bush

        _cooldowns[bushId] = _elapsed + HARVEST_COOLDOWN;
        Rpc(MethodName.SetBushVisible, bushId, false);

        InventorySystem.Instance.AddItem(sender, "item.berry", 1);
        SkillSystem.Instance?.NotifyAction(sender, "skill.foraging");
        GD.Print($"[Bush] peer {sender} harvested {bushId} → +1 berry");
    }

    /// <summary>Client pressed E near a Cooking Fire with berries.</summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestCook()
    {
        if (!Multiplayer.IsServer()) return;

        long sender = Multiplayer.GetRemoteSenderId();
        if (sender == 0) sender = 1L;

        if (!InventorySystem.Instance.HasItems(sender, "item.berry", 1))
        {
            GD.Print($"[Food] peer {sender} tried to cook — no berries");
            return;
        }

        InventorySystem.Instance.RemoveItems(sender, "item.berry", 1);
        InventorySystem.Instance.AddItem(sender, "item.cooked_berry", 1);
        SkillSystem.Instance?.NotifyAction(sender, "skill.cooking");
        GD.Print($"[Food] peer {sender} cooked 1 berry → 1 cooked berry");
    }

    /// <summary>
    /// Client pressed Tab to eat food. Priority: cooked form first, raw form fallback.
    /// Hunger values come from FoodRegistry — no hardcoded numbers here.
    /// Toxic-raw foods: IsToxicRaw flag is stored in FoodData but the poison status
    /// effect is not yet applied (deferred to post-M4 health/damage system).
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestEat()
    {
        if (!Multiplayer.IsServer()) return;

        long sender = Multiplayer.GetRemoteSenderId();
        if (sender == 0) sender = 1L;

        // Try cooked items first (higher nutrition, not toxic).
        foreach (var food in FoodRegistry.All)
        {
            if (food.CookedItemId is null) continue;
            if (!InventorySystem.Instance.HasItems(sender, food.CookedItemId, 1)) continue;

            InventorySystem.Instance.RemoveItems(sender, food.CookedItemId, 1);
            NeedsSystem.Instance.RestoreHunger(sender, food.CookedHunger);
            GD.Print($"[Food] peer {sender} ate {food.CookedItemId} (+{food.CookedHunger} hunger)");
            return;
        }

        // Fall back to raw items.
        foreach (var food in FoodRegistry.All)
        {
            if (!InventorySystem.Instance.HasItems(sender, food.RawItemId, 1)) continue;

            InventorySystem.Instance.RemoveItems(sender, food.RawItemId, 1);
            NeedsSystem.Instance.RestoreHunger(sender, food.BaseHunger);
            // TODO(post-M4): if food.IsToxicRaw, apply poison status to sender.
            GD.Print($"[Food] peer {sender} ate {food.RawItemId} (+{food.BaseHunger} hunger)" +
                     (food.IsToxicRaw ? " [TOXIC — effect not yet implemented]" : ""));
            return;
        }

        GD.Print($"[Food] peer {sender} tried to eat — no food in inventory");
    }

    /// <summary>Hides or shows a bush on all peers (harvest/restore visual).</summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SetBushVisible(string bushId, bool visible)
    {
        if (_bushNodes.TryGetValue(bushId, out var node))
            node.Visible = visible;
    }
}
