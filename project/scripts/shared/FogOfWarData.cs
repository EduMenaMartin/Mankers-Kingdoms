using System;
using System.Collections.Generic;

namespace MankersKingdoms.Shared;

/// <summary>
/// Shared-reveal fog-of-war grid for the full world map (worldgen.md §11).
///
/// 64×64 cells — one per terrain tile (TerrainConfig.Default.MapWidth/MapHeight).
/// Cell states:
///   0 = UNSEEN   — solid black on the world map
///   1 = SEEN     — previously visited, rendered dark/greyed
///   2 = VISIBLE  — currently within vision radius, full colour
///
/// Vision radius reuses TERRITORY_RADIUS_W (40 m) so no second number is invented
/// (worldgen.md §11.3 — "reuse the same radius value already used for the minimap").
///
/// All cell state transitions happen in one call: <see cref="UpdateVisibility"/>.
/// Persistence: <see cref="ToBytes"/> / <see cref="FromBytes"/>; SaveSystem stores it
/// as Base64 in SaveData.FogBase64 (additive field, no version bump).
/// </summary>
public sealed class FogOfWarData
{
    // Vision radius in world units — matches MinimapHUD.TERRITORY_RADIUS_W.
    public const float VISION_RADIUS = 40f;

    public const byte UNSEEN  = 0;
    public const byte SEEN    = 1;
    public const byte VISIBLE = 2;

    // Row-major: _grid[x, z].
    private readonly byte[,] _grid;

    private readonly int   _width;
    private readonly int   _height;
    private readonly float _tileSize;
    private readonly float _halfW;
    private readonly float _halfH;

    public FogOfWarData()
    {
        var cfg   = TerrainConfig.Default;
        _width    = cfg.MapWidth;
        _height   = cfg.MapHeight;
        _tileSize = cfg.TileSize;
        _halfW    = (_width  - 1) * _tileSize * 0.5f;
        _halfH    = (_height - 1) * _tileSize * 0.5f;
        _grid     = new byte[_width, _height];
    }

    // ── Visibility update ─────────────────────────────────────────────────────

    /// <summary>
    /// Downgrades VISIBLE→SEEN for all cells, then marks cells within VISION_RADIUS
    /// of any supplied player position as VISIBLE.
    ///
    /// Returns true if any cell changed state (used by FogSystem to skip RPC when nothing changed).
    /// </summary>
    public bool UpdateVisibility(IReadOnlyList<(float X, float Z)> playerPositions)
    {
        bool changed = false;

        // Step 1: downgrade VISIBLE → SEEN.
        for (int x = 0; x < _width; x++)
        for (int z = 0; z < _height; z++)
        {
            if (_grid[x, z] == VISIBLE)
            {
                _grid[x, z] = SEEN;
                changed = true;
            }
        }

        // Step 2: mark cells within VISION_RADIUS of any player as VISIBLE.
        float radiusSq = VISION_RADIUS * VISION_RADIUS;
        float radiusCells = VISION_RADIUS / _tileSize;
        int   radiusCellsI = (int)Math.Ceiling(radiusCells);

        foreach (var (px, pz) in playerPositions)
        {
            int cx = WorldToCell(px, _halfW);
            int cz = WorldToCell(pz, _halfH);

            for (int dx = -radiusCellsI; dx <= radiusCellsI; dx++)
            for (int dz = -radiusCellsI; dz <= radiusCellsI; dz++)
            {
                int nx = cx + dx;
                int nz = cz + dz;
                if (nx < 0 || nx >= _width || nz < 0 || nz >= _height) continue;

                // Check circle in world-space.
                float wx = CellToWorld(nx, _halfW) - px;
                float wz = CellToWorld(nz, _halfH) - pz;
                if (wx * wx + wz * wz > radiusSq) continue;

                if (_grid[nx, nz] != VISIBLE)
                {
                    _grid[nx, nz] = VISIBLE;
                    changed = true;
                }
            }
        }

        return changed;
    }

    // ── Read access ───────────────────────────────────────────────────────────

    /// <summary>Returns the fog state for grid cell (x, z). 0=unseen, 1=seen, 2=visible.</summary>
    public byte GetCell(int x, int z) => _grid[x, z];

    /// <summary>
    /// Returns true if the cell at the given world position has ever been visited (SEEN or VISIBLE).
    /// Use this to decide whether a POI should appear on the map.
    /// </summary>
    public bool IsDiscovered(float worldX, float worldZ)
    {
        int cx = WorldToCell(worldX, _halfW);
        int cz = WorldToCell(worldZ, _halfH);
        return _grid[cx, cz] != UNSEEN;
    }

    public int Width  => _width;
    public int Height => _height;

    // ── Serialization ─────────────────────────────────────────────────────────

    public byte[] ToBytes()
    {
        var bytes = new byte[_width * _height];
        for (int x = 0; x < _width; x++)
        for (int z = 0; z < _height; z++)
            bytes[x * _height + z] = _grid[x, z];
        return bytes;
    }

    public static FogOfWarData FromBytes(byte[] bytes)
    {
        var fog = new FogOfWarData();
        int expected = fog._width * fog._height;
        if (bytes.Length != expected) return fog; // bad data → return blank
        for (int x = 0; x < fog._width; x++)
        for (int z = 0; z < fog._height; z++)
        {
            byte v = bytes[x * fog._height + z];
            // Downgrade VISIBLE→SEEN on restore (we don't know current positions yet).
            fog._grid[x, z] = v == VISIBLE ? SEEN : v;
        }
        return fog;
    }

    // ── Coordinate helpers ────────────────────────────────────────────────────

    private int WorldToCell(float world, float half) =>
        Math.Clamp((int)Math.Round((world + half) / _tileSize), 0, _width - 1);

    private float CellToWorld(int cell, float half) =>
        cell * _tileSize - half;
}
