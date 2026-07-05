using System.Collections.Generic;

namespace MankersKingdoms.Shared;

/// <summary>
/// Static registry of all food definitions.
/// Hardcoded for v1; move to data/base/food/*.json when the content loader exists.
/// See IDEAS_BACKLOG.md — "Content loading from repo-root data/ via filesystem".
/// </summary>
public static class FoodRegistry
{
    public static readonly IReadOnlyList<FoodData> All = new[]
    {
        // Berry: raw edible (10 hunger), cookable at Cooking Fire (×4 = 40 hunger).
        new FoodData(
            RawItemId:      "item.berry",
            CookedItemId:   "item.cooked_berry",
            BaseHunger:     10f,
            CookMultiplier: 4f
        ),
    };

    private static readonly Dictionary<string, FoodData> _byRaw   = new();
    private static readonly Dictionary<string, FoodData> _byCooked = new();

    static FoodRegistry()
    {
        foreach (var f in All)
        {
            _byRaw[f.RawItemId] = f;
            if (f.CookedItemId is not null)
                _byCooked[f.CookedItemId] = f;
        }
    }

    /// <summary>Returns the FoodData whose raw item ID matches, or null.</summary>
    public static FoodData? FindByRaw(string rawItemId) =>
        _byRaw.TryGetValue(rawItemId, out var f) ? f : null;

    /// <summary>Returns the FoodData whose cooked item ID matches, or null.</summary>
    public static FoodData? FindByCooked(string cookedItemId) =>
        _byCooked.TryGetValue(cookedItemId, out var f) ? f : null;
}
