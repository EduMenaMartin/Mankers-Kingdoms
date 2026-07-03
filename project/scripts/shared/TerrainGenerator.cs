using System;

namespace MankersKingdoms.Shared;

/// <summary>
/// Generates a heightmap from a seed and terrain config using fractal value noise.
/// Pure C#, no Godot dependency — safe to use in xUnit tests and on both server and client.
///
/// Both TerrainSystem (server, collision) and TerrainRenderer (client, mesh) call this with
/// the same seed to produce identical heightmaps without any network sync. See CURRENT_MILESTONE.md.
/// </summary>
public sealed class TerrainGenerator
{
    private readonly uint _seed;
    private readonly TerrainConfig _config;

    public TerrainGenerator(uint seed, TerrainConfig config)
    {
        _seed = seed;
        _config = config;
    }

    /// <summary>
    /// Returns a [MapWidth, MapHeight] array of world-space Y values.
    /// Heights are centred around 0, ranging roughly within ±(NoiseAmp/2).
    /// </summary>
    public float[,] GenerateHeightmap()
    {
        var map = new float[_config.MapWidth, _config.MapHeight];
        for (int x = 0; x < _config.MapWidth; x++)
        for (int z = 0; z < _config.MapHeight; z++)
            map[x, z] = Sample(x * _config.NoiseFreq, z * _config.NoiseFreq);
        return map;
    }

    // --- noise internals ---

    private float Sample(float x, float z)
    {
        float value = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float totalAmp = 0f;

        for (int o = 0; o < _config.NoiseOctaves; o++)
        {
            value    += (ValueNoise(x * frequency, z * frequency) - 0.5f) * amplitude;
            totalAmp += amplitude;
            amplitude *= 0.5f;
            frequency *= 2f;
        }

        // Normalise to [-0.5, 0.5] then scale to world amplitude.
        return (value / totalAmp) * _config.NoiseAmp;
    }

    private float ValueNoise(float x, float z)
    {
        int x0 = (int)MathF.Floor(x);
        int z0 = (int)MathF.Floor(z);
        float fx = x - x0;
        float fz = z - z0;

        // Smoothstep
        fx = fx * fx * (3f - 2f * fx);
        fz = fz * fz * (3f - 2f * fz);

        float v00 = Hash(x0,     z0);
        float v10 = Hash(x0 + 1, z0);
        float v01 = Hash(x0,     z0 + 1);
        float v11 = Hash(x0 + 1, z0 + 1);

        return Lerp(Lerp(v00, v10, fx), Lerp(v01, v11, fx), fz);
    }

    private float Hash(int ix, int iz)
    {
        unchecked
        {
            uint h = _seed;
            h ^= (uint)(ix * 1619 + iz * 31337);
            h ^= h >> 16;
            h *= 0x45d9f3b;
            h ^= h >> 16;
            return (h & 0xFFFFu) / 65535f;   // [0, 1]
        }
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
