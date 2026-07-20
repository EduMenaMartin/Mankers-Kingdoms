namespace MankersKingdoms.Shared;

/// <summary>
/// Character alignment — a personal trait selected at creation.
/// Three-point axis matching the world win-condition alignment categories (PRD.md §4.9),
/// but separate from and not mechanically tied to the world setting.
///
/// Purpose: flavor text and future faction-interaction texture layered on top of
/// FactionService's Hostile/Neutral/Allied model (not mechanically wired in v1).
/// See docs/gdd/character-creation.md §11.
/// </summary>
public enum Alignment
{
    Lawful,
    Neutral,
    Chaotic,
}

public static class AlignmentExtensions
{
    public static string ToLocKey(this Alignment a) => a switch
    {
        Alignment.Lawful  => "alignment.lawful",
        Alignment.Neutral => "alignment.neutral",
        Alignment.Chaotic => "alignment.chaotic",
        _                 => "alignment.neutral",
    };

    public static Alignment FromString(string? s) => s?.ToLowerInvariant() switch
    {
        "lawful"  => Alignment.Lawful,
        "chaotic" => Alignment.Chaotic,
        _         => Alignment.Neutral,
    };
}
