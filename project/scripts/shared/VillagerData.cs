namespace MankersKingdoms.Shared;

/// <summary>
/// Immutable descriptor for a single village NPC.
///
/// Stats are rolled as best-of-three 3d6 per stat independently.
/// ArchetypeTag is derived post-roll from whichever stat is highest.
/// Tie-break priority: Str > Con > Dex > Wis.
///
/// The archetype is a hint about the villager's natural aptitude shown
/// in the recruitment dialogue. It also maps to the job station that
/// benefits most from this villager (e.g. archetype.woodcutter → Woodcutter's Post).
///
/// See docs/gdd/villages.md §2 for the full villager generation model.
/// </summary>
public sealed record VillagerData(string Id, string Name, StatBlock Stats, float WorldX, float WorldZ)
{
    /// <summary>
    /// Archetype derived from the highest rolled stat.
    /// Tie-break: Str > Con > Dex > Wis.
    /// </summary>
    public string ArchetypeTag => DeriveArchetype(Stats);

    /// <summary>Loc key for the archetype display name (e.g. "archetype.woodcutter.name").</summary>
    public string ArchetypeNameKey => ArchetypeTag + ".name";

    private static string DeriveArchetype(StatBlock s)
    {
        int max = System.Math.Max(System.Math.Max(s.Str, s.Dex), System.Math.Max(s.Con, s.Wis));
        if (s.Str == max) return "archetype.woodcutter";
        if (s.Con == max) return "archetype.laborer";
        if (s.Dex == max) return "archetype.guard";
        return "archetype.forager"; // Wis is max (or only remaining)
    }
}
