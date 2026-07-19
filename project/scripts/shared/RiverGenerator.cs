using System;
using System.Collections.Generic;

namespace MankersKingdoms.Shared;

/// <summary>
/// Procedurally generates a river path across the heightmap, carves a channel into it,
/// and returns a RiverData describing the result.
///
/// Algorithm overview:
///   1. Source selection — sample N_SOURCE_TRIES random border cells, pick the highest.
///   2. Downhill-biased walk (D8 + noise) — score each unvisited neighbour by descent + noise,
///      pick the best each step. Terminates at map edge or MAX_PATH_STEPS.
///   3. Height smoothing — min-sweep ensures path heights are monotonically non-increasing.
///   4. Width noise — 1D deterministic hash gives width variation along arc-length.
///   5. Carving — cosine taper profile per path step, modifies heightmap in-place.
///   6. Channel mask — marks all carved cells for downstream exclusion (trees, bushes).
///
/// Seeds:
///   Path RNG:   WorldSeed ^ 0xB1A7E600u  (BLAZE/RIVER mnemonic)
///   Width hash: WorldSeed ^ 0xB1A7E601u  (sub-salt, independent of path decisions)
///
/// Pure C# — no Godot dependency. Safe to call from xUnit tests.
/// </summary>
public sealed class RiverGenerator
{
    // ── Tuning constants ──────────────────────────────────────────────────────

    private const int   N_SOURCE_TRIES        = 8;      // border cells sampled for source
    private const int   MAX_PATH_STEPS        = 192;    // hard cap on path length
    private const int   MIN_RIVER_STEPS       = 20;     // must walk this far before border-exit allowed
    private const float WANDER_FACTOR         = 0.3f;   // noise added to downhill score

    private const float BASE_HALF_WIDTH_TILES = 1.0f;   // minimum half-width (tiles)
    private const float WIDTH_VARIATION_TILES = 1.0f;   // additional half-width range
    private const float WIDTH_NOISE_FREQ      = 0.04f;  // spatial frequency of width changes

    private const float CHANNEL_DEPTH         = 3.0f;   // max carve depth at channel centre (m)
    private const float WATER_SURFACE_OFFSET  = 1.5f;   // water surface above channel floor (m)

    // ── State ─────────────────────────────────────────────────────────────────

    private readonly uint         _seed;
    private readonly TerrainConfig _cfg;

    public RiverGenerator(uint seed, TerrainConfig cfg)
    {
        _seed = seed;
        _cfg  = cfg;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Carves a river channel into the supplied heightmap (modified in-place) and returns
    /// RiverData describing the path and channel mask.
    ///
    /// Call this BEFORE building the HeightMapShape3D collision mesh in TerrainSystem._Ready(),
    /// so both the physics collider and downstream generators (trees, bushes) see the carved terrain.
    /// </summary>
    public RiverData Generate(float[,] heightmap)
    {
        var rng = new System.Random((int)(_seed ^ 0xB1A7E600u));

        // 1. Source
        var (startX, startZ) = PickSource(heightmap, rng);

        // 2. Walk
        var path = WalkPath(heightmap, startX, startZ, rng);

        if (path.Count < 2)
        {
            // Degenerate case — return empty river rather than crashing.
            return new RiverData(
                new List<RiverSegment>(),
                new bool[_cfg.MapWidth, _cfg.MapHeight]);
        }

        // 3. Smooth path heights (monotonically non-increasing)
        var pathHeights = new float[path.Count];
        for (int i = 0; i < path.Count; i++)
            pathHeights[i] = heightmap[path[i].x, path[i].z];
        for (int i = 1; i < pathHeights.Length; i++)
            pathHeights[i] = Math.Min(pathHeights[i], pathHeights[i - 1]);

        // 4. Width per step (arc-length parameterised)
        var halfWidths = ComputeHalfWidths(path);

        // 5. Carve and build channel mask
        var channelMask = new bool[_cfg.MapWidth, _cfg.MapHeight];
        Carve(heightmap, path, pathHeights, halfWidths, channelMask);

        // 6. Build segment list
        var segments = BuildSegments(path, pathHeights, halfWidths);

        return new RiverData(segments, channelMask);
    }

    // ── Private — path generation ─────────────────────────────────────────────

    private (int x, int z) PickSource(float[,] heightmap, System.Random rng)
    {
        int  bestX = 0, bestZ = 0;
        float bestH = float.MinValue;

        for (int i = 0; i < N_SOURCE_TRIES; i++)
        {
            // Pick a random border cell.
            int edge = rng.Next(4);
            int x, z;
            switch (edge)
            {
                case 0:  x = 0;                   z = rng.Next(_cfg.MapHeight); break; // left edge
                case 1:  x = _cfg.MapWidth - 1;   z = rng.Next(_cfg.MapHeight); break; // right edge
                case 2:  x = rng.Next(_cfg.MapWidth); z = 0;                   break; // top edge
                default: x = rng.Next(_cfg.MapWidth); z = _cfg.MapHeight - 1;  break; // bottom edge
            }

            if (heightmap[x, z] > bestH)
            {
                bestH = heightmap[x, z];
                bestX = x;
                bestZ = z;
            }
        }

        return (bestX, bestZ);
    }

    private List<(int x, int z)> WalkPath(
        float[,] heightmap, int startX, int startZ, System.Random rng)
    {
        var path    = new List<(int x, int z)>(MAX_PATH_STEPS);
        var visited = new bool[_cfg.MapWidth, _cfg.MapHeight];

        int cx = startX, cz = startZ;

        while (path.Count < MAX_PATH_STEPS)
        {
            path.Add((cx, cz));
            visited[cx, cz] = true;

            // Terminate when we've walked far enough and landed on a map border.
            // The source is a border cell, and the walk may skirt the edge early —
            // MIN_RIVER_STEPS prevents trivially short paths on small maps.
            if (path.Count > MIN_RIVER_STEPS &&
                (cx == 0 || cx == _cfg.MapWidth - 1 || cz == 0 || cz == _cfg.MapHeight - 1))
                break;

            int   bestX = -1, bestZ = -1;
            float bestScore = float.MinValue;

            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                if (dx == 0 && dz == 0) continue;

                int nx = cx + dx, nz = cz + dz;
                if (nx < 0 || nx >= _cfg.MapWidth || nz < 0 || nz >= _cfg.MapHeight) continue;
                if (visited[nx, nz]) continue;

                float descent = heightmap[cx, cz] - heightmap[nx, nz]; // positive = downhill
                float noise   = (float)rng.NextDouble() * WANDER_FACTOR;
                float score   = descent + noise;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestX     = nx;
                    bestZ     = nz;
                }
            }

            if (bestX < 0) break; // all neighbours visited

            cx = bestX;
            cz = bestZ;
        }

        return path;
    }

    // ── Private — width ───────────────────────────────────────────────────────

    private float[] ComputeHalfWidths(List<(int x, int z)> path)
    {
        var widths  = new float[path.Count];
        float arcLen = 0f;

        for (int i = 0; i < path.Count; i++)
        {
            if (i > 0)
            {
                float dx = path[i].x - path[i - 1].x;
                float dz = path[i].z - path[i - 1].z;
                arcLen += MathF.Sqrt(dx * dx + dz * dz);
            }

            float noise01  = WidthNoise(arcLen * WIDTH_NOISE_FREQ);
            widths[i]      = BASE_HALF_WIDTH_TILES + noise01 * WIDTH_VARIATION_TILES;
        }

        return widths;
    }

    // 1D smoothstep value noise using a dedicated sub-salt — does not consume path RNG.
    private float WidthNoise(float t)
    {
        int   t0 = (int)MathF.Floor(t);
        float ft = t - t0;
        ft = ft * ft * (3f - 2f * ft); // smoothstep
        return Lerp(Hash1D(t0), Hash1D(t0 + 1), ft);
    }

    private float Hash1D(int it)
    {
        unchecked
        {
            uint h = _seed ^ 0xB1A7E601u;
            h ^= (uint)(it * 2654435761u);
            h ^= h >> 16;
            h *= 0x45d9f3b;
            h ^= h >> 16;
            return (h & 0xFFFFu) / 65535f;
        }
    }

    // ── Private — carving ─────────────────────────────────────────────────────

    private void Carve(
        float[,]      heightmap,
        List<(int x, int z)> path,
        float[]       pathHeights,
        float[]       halfWidths,
        bool[,]       channelMask)
    {
        for (int i = 0; i < path.Count; i++)
        {
            var (gx, gz)  = path[i];
            float centreH = pathHeights[i];
            float halfW   = halfWidths[i];
            int   iRadius = (int)MathF.Ceiling(halfW) + 1;

            for (int dx = -iRadius; dx <= iRadius; dx++)
            for (int dz = -iRadius; dz <= iRadius; dz++)
            {
                int cx = gx + dx, cz = gz + dz;
                if (cx < 0 || cx >= _cfg.MapWidth || cz < 0 || cz >= _cfg.MapHeight) continue;

                float d = MathF.Sqrt(dx * dx + dz * dz) / halfW;
                if (d > 1f) continue;

                float profile    = (1f + MathF.Cos(MathF.PI * d)) / 2f; // 1.0 at centre, 0.0 at edge
                float carveFloor = centreH - CHANNEL_DEPTH * profile;

                heightmap[cx, cz] = Math.Min(heightmap[cx, cz], carveFloor);
                channelMask[cx, cz] = true;
            }
        }
    }

    // ── Private — segment list ────────────────────────────────────────────────

    private List<RiverSegment> BuildSegments(
        List<(int x, int z)> path,
        float[]              pathHeights,
        float[]              halfWidths)
    {
        var segments = new List<RiverSegment>(path.Count);
        float half = (_cfg.MapWidth - 1) / 2f;
        float halfH = (_cfg.MapHeight - 1) / 2f;

        for (int i = 0; i < path.Count; i++)
        {
            var (gx, gz) = path[i];

            float worldX = (gx - half)  * _cfg.TileSize;
            float worldZ = (gz - halfH) * _cfg.TileSize;
            float waterY = pathHeights[i] - CHANNEL_DEPTH + WATER_SURFACE_OFFSET;

            var (tx, tz) = ComputeTangent(path, i);

            segments.Add(new RiverSegment(
                GridX:      gx,
                GridZ:      gz,
                WorldX:     worldX,
                WaterY:     waterY,
                WorldZ:     worldZ,
                TangentX:   tx,
                TangentZ:   tz,
                HalfWidthM: halfWidths[i] * _cfg.TileSize
            ));
        }

        return segments;
    }

    private static (float tx, float tz) ComputeTangent(List<(int x, int z)> path, int i)
    {
        int a = Math.Max(0,              i - 1);
        int b = Math.Min(path.Count - 1, i + 1);
        if (a == b) return (1f, 0f); // single-point degenerate

        float dx = path[b].x - path[a].x;
        float dz = path[b].z - path[a].z;
        float len = MathF.Sqrt(dx * dx + dz * dz);
        if (len < 0.001f) return (1f, 0f);

        return (dx / len, dz / len);
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
