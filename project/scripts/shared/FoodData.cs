namespace MankersKingdoms.Shared;

/// <summary>
/// Immutable nutrition definition for one raw/cooked food pair.
/// Pure C# — no Godot dependency, testable in xUnit.
///
/// Cooking multiplier: CookedHunger = BaseHunger * CookMultiplier.
/// CookedItemId is null for foods that cannot be cooked.
///
/// IsToxicRaw: eating the raw form inflicts poison for PoisonDuration seconds.
/// Poison effect implementation deferred to post-M4 (health/damage system required).
/// Fields present now so save format and FoodRegistry don't need a breaking change later.
/// </summary>
public sealed record FoodData(
    string  RawItemId,
    string? CookedItemId,
    float   BaseHunger,
    float   CookMultiplier,
    bool    IsToxicRaw     = false,
    float   PoisonDuration = 0f
)
{
    /// <summary>Hunger restored by the cooked form. Zero if this food cannot be cooked.</summary>
    public float CookedHunger => CookedItemId is not null ? BaseHunger * CookMultiplier : 0f;
}
