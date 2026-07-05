using System.Collections.Generic;
using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Client;

/// <summary>
/// Always-visible minimap in the top-right corner (150×150 px, Layer 30).
///
/// Renders a greyscale terrain texture baked lazily from TerrainRenderer.CachedHeightmap,
/// overlaid with:
///   • White dot    — local player
///   • Orange dots  — wolf nests
///   • Green dots   — goblin nests
///   • Red dots     — bandit camp nests
///   • Blue dot + ring — own Kingdom Marker + territory radius
///
/// Nest positions are generated client-side from NestGenerator (same seed, no RPC needed).
///
/// Editor task: add CanvasLayer node named MinimapHUD with this script to GameWorld.tscn.
/// </summary>
public partial class MinimapHUD : CanvasLayer
{
    private const float MAP_SIZE           = 150f;
    private const float TERRITORY_RADIUS_W = 40f;   // must match PlacementController
    private const string PLAYERS_PATH      = "/root/GameWorld/Players";
    private const string SETTLEMENT_PATH   = "/root/GameWorld/SettlementSystem";

    private _DrawControl _draw = null!;
    private bool         _textureBaked;

    // Computed once in _Ready.
    private float _halfW, _halfH;

    // Updated each frame.
    private Vector2  _playerMapPos;
    private Vector2? _markerMapPos;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        Layer = 30;

        var cfg = TerrainConfig.Default;
        _halfW = (cfg.MapWidth  - 1) * cfg.TileSize * 0.5f;
        _halfH = (cfg.MapHeight - 1) * cfg.TileSize * 0.5f;

        // Pre-compute nest dot list — positions are deterministic.
        var nestDots = new List<(Vector2 pos, Color color)>();
        foreach (var nest in NestGenerator.Generate(GameSession.WorldSeed))
            nestDots.Add((ToMap(nest.WorldX, nest.WorldZ), NestColor(nest.MonsterTypeIds[0])));

        _draw = new _DrawControl(nestDots, MAP_SIZE, TERRITORY_RADIUS_W / (_halfW * 2f) * MAP_SIZE)
        {
            AnchorLeft   = 1f, AnchorRight  = 1f,
            AnchorTop    = 0f, AnchorBottom = 0f,
            OffsetLeft   = -(MAP_SIZE + 10f),
            OffsetRight  = -10f,
            OffsetTop    = 10f,
            OffsetBottom = MAP_SIZE + 10f,
        };
        AddChild(_draw);

        var ss = GetNodeOrNull(SETTLEMENT_PATH);
        ss?.Connect("MarkerPlanted", new Callable(this, MethodName.OnMarkerPlanted));
    }

    public override void _Process(double _delta)
    {
        // Bake terrain texture the first frame the heightmap is ready.
        if (!_textureBaked)
        {
            var h = TerrainRenderer.CachedHeightmap;
            if (h.Length > 0)
            {
                _draw.TerrainTex = BakeTexture(h);
                _textureBaked    = true;
            }
        }

        long uid        = Multiplayer.GetUniqueId();
        var  playerNode = GetNodeOrNull<Node3D>($"{PLAYERS_PATH}/Player_{uid}");
        if (playerNode != null)
            _playerMapPos = ToMap(playerNode.GlobalPosition.X, playerNode.GlobalPosition.Z);

        _draw.PlayerPos         = _playerMapPos;
        _draw.MarkerMapPos      = _markerMapPos;
        var dd = LocalState.DeathDropWorldPos;
        _draw.DeathMarkerMapPos = dd.HasValue ? ToMap(dd.Value.X, dd.Value.Z) : (Vector2?)null;
        _draw.QueueRedraw();
    }

    // ── Signal handlers ───────────────────────────────────────────────────────

    private void OnMarkerPlanted(long peerId, Vector3 pos)
    {
        if (peerId == Multiplayer.GetUniqueId())
            _markerMapPos = ToMap(pos.X, pos.Z);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Vector2 ToMap(float worldX, float worldZ) => new(
        (worldX + _halfW) / (_halfW * 2f) * MAP_SIZE,
        (worldZ + _halfH) / (_halfH * 2f) * MAP_SIZE
    );

    private static Color NestColor(string leadType) => leadType switch
    {
        "monster.wolf"   => new Color(1.00f, 0.55f, 0.10f), // orange
        "monster.goblin" => new Color(0.30f, 0.90f, 0.30f), // green
        _                => new Color(0.90f, 0.20f, 0.20f), // red (bandits + fallback)
    };

    private ImageTexture BakeTexture(float[,] h)
    {
        var cfg = TerrainConfig.Default;
        var img = Image.CreateEmpty(cfg.MapWidth, cfg.MapHeight, false, Image.Format.Rgb8);

        float minH = float.MaxValue, maxH = float.MinValue;
        for (int x = 0; x < cfg.MapWidth; x++)
            for (int z = 0; z < cfg.MapHeight; z++)
            {
                if (h[x, z] < minH) minH = h[x, z];
                if (h[x, z] > maxH) maxH = h[x, z];
            }
        float range = maxH - minH < 0.001f ? 1f : maxH - minH;

        for (int x = 0; x < cfg.MapWidth; x++)
            for (int z = 0; z < cfg.MapHeight; z++)
            {
                float t = (h[x, z] - minH) / range;
                var col = t < 0.28f
                    ? new Color(0.08f, 0.14f, 0.36f + t * 0.35f)              // deep-to-shallow water
                    : new Color(0.18f + t * 0.46f, 0.23f + t * 0.30f, 0.12f + t * 0.34f); // land
                img.SetPixel(x, z, col);
            }

        return ImageTexture.CreateFromImage(img);
    }

    // ── Inner draw control (non-partial — no exports/signals, no source-gen needed) ──

    private sealed partial class _DrawControl : Control
    {
        public ImageTexture? TerrainTex;
        public Vector2       PlayerPos;
        public Vector2?      MarkerMapPos;
        public Vector2?      DeathMarkerMapPos;

        private readonly List<(Vector2 pos, Color color)> _nestDots;
        private readonly float _mapSize;
        private readonly float _territoryRingPx;

        public _DrawControl(
            List<(Vector2 pos, Color color)> nestDots,
            float mapSize,
            float territoryRingPx)
        {
            _nestDots        = nestDots;
            _mapSize         = mapSize;
            _territoryRingPx = territoryRingPx;
        }

        public override void _Draw()
        {
            var rect = new Rect2(Vector2.Zero, Size);

            // Background.
            DrawRect(rect, new Color(0f, 0f, 0f, 0.60f));

            // Terrain texture.
            if (TerrainTex != null)
                DrawTextureRect(TerrainTex, rect, false);

            // Territory ring + marker dot.
            if (MarkerMapPos.HasValue)
            {
                DrawArc(MarkerMapPos.Value, _territoryRingPx, 0f, Mathf.Tau,
                        48, new Color(0.35f, 0.75f, 1f, 0.65f), 1.5f);
                DrawCircle(MarkerMapPos.Value, 3f, new Color(0.35f, 0.75f, 1f));
            }

            // Nest dots.
            foreach (var (pos, color) in _nestDots)
            {
                DrawCircle(pos, 5f, new Color(0f, 0f, 0f, 0.45f)); // drop shadow
                DrawCircle(pos, 4f, color);
            }

            // Death drop marker — red X.
            if (DeathMarkerMapPos.HasValue)
            {
                var p = DeathMarkerMapPos.Value;
                var red = new Color(1f, 0.15f, 0.15f);
                DrawLine(p + new Vector2(-5f, -5f), p + new Vector2(5f, 5f), red, 2f);
                DrawLine(p + new Vector2(5f, -5f),  p + new Vector2(-5f, 5f), red, 2f);
            }

            // Player dot.
            DrawCircle(PlayerPos, 5f, new Color(0f, 0f, 0f, 0.55f));
            DrawCircle(PlayerPos, 4f, Colors.White);

            // Border.
            DrawRect(rect, new Color(0.55f, 0.55f, 0.55f, 0.70f), false, 1f);
        }
    }
}
