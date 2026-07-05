using System.Collections.Generic;
using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Client;

/// <summary>
/// Full-screen world map opened/closed with M key (or Escape), Layer 31.
///
/// Shows the same data as MinimapHUD at a larger scale (700×700 px panel):
///   • Greyscale terrain texture
///   • White dot  — local player
///   • Coloured skull dots — nest positions by type
///   • Blue dot + ring — own Kingdom Marker
///
/// Includes a colour legend below the map panel.
/// Generates terrain texture and nest list independently from MinimapHUD
/// (same deterministic data, no coupling needed).
///
/// Editor task: add CanvasLayer node named WorldMapScreen with this script to GameWorld.tscn.
/// </summary>
public partial class WorldMapScreen : CanvasLayer
{
    private const float  MAP_PANEL     = 700f;
    private const float  TERRITORY_W   = 40f;
    private const string PLAYERS_PATH  = "/root/GameWorld/Players";
    private const string SETTLEMENT_PATH = "/root/GameWorld/SettlementSystem";

    private _MapDrawControl _mapDraw  = null!;
    private bool            _texBaked;

    private float   _halfW, _halfH;
    private Vector2 _playerMapPos;
    private Vector2? _markerMapPos;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        Layer   = 31;
        Visible = false;

        var cfg = TerrainConfig.Default;
        _halfW = (cfg.MapWidth  - 1) * cfg.TileSize * 0.5f;
        _halfH = (cfg.MapHeight - 1) * cfg.TileSize * 0.5f;

        var nestDots = new List<(Vector2 pos, Color color, string label)>();
        foreach (var nest in NestGenerator.Generate(GameSession.WorldSeed))
            nestDots.Add((ToMap(nest.WorldX, nest.WorldZ), NestColor(nest.MonsterTypeIds[0]),
                          NestLabel(nest.MonsterTypeIds[0])));

        // ── Full-screen dark backdrop ──────────────────────────────────────────
        var backdrop = new ColorRect { Color = new Color(0f, 0f, 0f, 0.72f) };
        backdrop.AnchorRight = backdrop.AnchorBottom = 1f;
        AddChild(backdrop);

        // ── Centred map panel ─────────────────────────────────────────────────
        float ringPx = TERRITORY_W / (_halfW * 2f) * MAP_PANEL;
        _mapDraw = new _MapDrawControl(nestDots, MAP_PANEL, ringPx)
        {
            AnchorLeft   = 0.5f, AnchorRight  = 0.5f,
            AnchorTop    = 0.5f, AnchorBottom = 0.5f,
            OffsetLeft   = -(MAP_PANEL * 0.5f),
            OffsetRight  =  (MAP_PANEL * 0.5f),
            OffsetTop    = -(MAP_PANEL * 0.5f + 30f), // shift up to leave room for legend
            OffsetBottom =  (MAP_PANEL * 0.5f - 30f),
        };
        AddChild(_mapDraw);

        // ── Legend ────────────────────────────────────────────────────────────
        BuildLegend();

        // ── Close hint ────────────────────────────────────────────────────────
        var hint = new Label
        {
            Text                = "M / Escape — close map",
            HorizontalAlignment = HorizontalAlignment.Center,
            AnchorLeft          = 0f, AnchorRight  = 1f,
            AnchorTop           = 1f, AnchorBottom = 1f,
            OffsetTop           = -22f, OffsetBottom = 0f,
            Modulate            = new Color(1f, 1f, 1f, 0.50f),
        };
        AddChild(hint);

        var ss = GetNodeOrNull(SETTLEMENT_PATH);
        ss?.Connect("MarkerPlanted", new Callable(this, MethodName.OnMarkerPlanted));
    }

    public override void _Process(double _delta)
    {
        if (!Visible) return;

        if (!_texBaked)
        {
            var h = TerrainRenderer.CachedHeightmap;
            if (h.Length > 0)
            {
                _mapDraw.TerrainTex = BakeTexture(h);
                _texBaked           = true;
            }
        }

        long uid  = Multiplayer.GetUniqueId();
        var  node = GetNodeOrNull<Node3D>($"{PLAYERS_PATH}/Player_{uid}");
        if (node != null)
            _playerMapPos = ToMap(node.GlobalPosition.X, node.GlobalPosition.Z);

        _mapDraw.PlayerPos         = _playerMapPos;
        _mapDraw.MarkerMapPos      = _markerMapPos;
        var dd = LocalState.DeathDropWorldPos;
        _mapDraw.DeathMarkerMapPos = dd.HasValue ? ToMap(dd.Value.X, dd.Value.Z) : (Vector2?)null;
        _mapDraw.QueueRedraw();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("open_map") || (Visible && @event.IsActionPressed("ui_cancel")))
        {
            Visible = !Visible;
            GetViewport().SetInputAsHandled();
        }
    }

    // ── Signal ────────────────────────────────────────────────────────────────

    private void OnMarkerPlanted(long peerId, Vector3 pos)
    {
        if (peerId == Multiplayer.GetUniqueId())
            _markerMapPos = ToMap(pos.X, pos.Z);
    }

    // ── Legend ────────────────────────────────────────────────────────────────

    private void BuildLegend()
    {
        var hbox = new HBoxContainer
        {
            AnchorLeft   = 0.5f, AnchorRight  = 0.5f,
            AnchorTop    = 0.5f, AnchorBottom = 0.5f,
            OffsetLeft   = -(MAP_PANEL * 0.5f),
            OffsetRight  =  (MAP_PANEL * 0.5f),
            OffsetTop    =  (MAP_PANEL * 0.5f - 28f),
            OffsetBottom =  (MAP_PANEL * 0.5f + 2f),
        };
        AddChild(hbox);

        (string label, Color color)[] entries =
        [
            ("You",          Colors.White),
            ("Wolf nest",    new Color(1.00f, 0.55f, 0.10f)),
            ("Goblin nest",  new Color(0.30f, 0.90f, 0.30f)),
            ("Bandit camp",  new Color(0.90f, 0.20f, 0.20f)),
            ("Your marker",  new Color(0.35f, 0.75f, 1.00f)),
            ("Death drop",   new Color(1.00f, 0.15f, 0.15f)),
        ];

        foreach (var (text, col) in entries)
        {
            var dot = new ColorRect
            {
                Color             = col,
                CustomMinimumSize = new Vector2(10f, 10f),
            };
            var lbl = new Label
            {
                Text     = text,
                Modulate = new Color(1f, 1f, 1f, 0.85f),
            };
            var spacer = new Control { CustomMinimumSize = new Vector2(18f, 0f) };

            hbox.AddChild(dot);
            hbox.AddChild(lbl);
            hbox.AddChild(spacer);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Vector2 ToMap(float wx, float wz) => new(
        (wx + _halfW) / (_halfW * 2f) * MAP_PANEL,
        (wz + _halfH) / (_halfH * 2f) * MAP_PANEL
    );

    private static Color NestColor(string leadType) => leadType switch
    {
        "monster.wolf"   => new Color(1.00f, 0.55f, 0.10f),
        "monster.goblin" => new Color(0.30f, 0.90f, 0.30f),
        _                => new Color(0.90f, 0.20f, 0.20f),
    };

    private static string NestLabel(string leadType) => leadType switch
    {
        "monster.wolf"   => "W",
        "monster.goblin" => "G",
        _                => "B",
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
                    ? new Color(0.08f, 0.14f, 0.36f + t * 0.35f)
                    : new Color(0.18f + t * 0.46f, 0.23f + t * 0.30f, 0.12f + t * 0.34f);
                img.SetPixel(x, z, col);
            }

        return ImageTexture.CreateFromImage(img);
    }

    // ── Inner draw control ────────────────────────────────────────────────────

    private sealed partial class _MapDrawControl : Control
    {
        public ImageTexture? TerrainTex;
        public Vector2       PlayerPos;
        public Vector2?      MarkerMapPos;
        public Vector2?      DeathMarkerMapPos;

        private readonly List<(Vector2 pos, Color color, string label)> _nestDots;
        private readonly float _mapSize;
        private readonly float _ringPx;

        public _MapDrawControl(
            List<(Vector2 pos, Color color, string label)> nestDots,
            float mapSize,
            float ringPx)
        {
            _nestDots = nestDots;
            _mapSize  = mapSize;
            _ringPx   = ringPx;
        }

        public override void _Draw()
        {
            var rect = new Rect2(Vector2.Zero, Size);

            DrawRect(rect, new Color(0.05f, 0.05f, 0.08f, 1f));

            if (TerrainTex != null)
                DrawTextureRect(TerrainTex, rect, false);

            // Territory ring.
            if (MarkerMapPos.HasValue)
            {
                DrawArc(MarkerMapPos.Value, _ringPx, 0f, Mathf.Tau,
                        72, new Color(0.35f, 0.75f, 1f, 0.55f), 2f);
                DrawCircle(MarkerMapPos.Value, 5f, new Color(0.35f, 0.75f, 1f));
            }

            // Nest dots.
            foreach (var (pos, color, _) in _nestDots)
            {
                DrawCircle(pos, 8f, new Color(0f, 0f, 0f, 0.50f));
                DrawCircle(pos, 6f, color);
            }

            // Death drop marker — red X with label.
            if (DeathMarkerMapPos.HasValue)
            {
                var p = DeathMarkerMapPos.Value;
                var red = new Color(1f, 0.15f, 0.15f);
                DrawLine(p + new Vector2(-8f, -8f), p + new Vector2(8f, 8f), red, 2.5f);
                DrawLine(p + new Vector2(8f, -8f),  p + new Vector2(-8f, 8f), red, 2.5f);
                DrawString(ThemeDB.FallbackFont, p + new Vector2(11f, -6f),
                           "DROP", HorizontalAlignment.Left, -1, 11, red);
            }

            // Player dot.
            DrawCircle(PlayerPos, 8f, new Color(0f, 0f, 0f, 0.60f));
            DrawCircle(PlayerPos, 6f, Colors.White);

            // "YOU" label near player.
            DrawString(ThemeDB.FallbackFont, PlayerPos + new Vector2(10f, -6f),
                       "YOU", HorizontalAlignment.Left, -1, 12, Colors.White);

            // Border.
            DrawRect(rect, new Color(0.60f, 0.60f, 0.60f, 0.80f), false, 1.5f);
        }
    }
}
