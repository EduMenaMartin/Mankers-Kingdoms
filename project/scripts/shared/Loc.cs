using System.Collections.Generic;
using System.Text.Json;

namespace MankersKingdoms.Shared;

public static class Loc
{
    static readonly Dictionary<string, string> _strings = new();

    public static void LoadJson(string json)
    {
        var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        if (entries is null) return;
        foreach (var (key, value) in entries)
            _strings[key] = value;
    }

    // See ADR-0012: fallback chain is missing key → [key.name]
    public static string T(string key) =>
        _strings.TryGetValue(key, out var value) ? value : $"[{key}]";

    public static void Reset() => _strings.Clear();
}
