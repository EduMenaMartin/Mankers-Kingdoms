namespace MankersKingdoms.Shared;

/// <summary>
/// The four stats used in Mankers Kingdoms (M5+).
/// Rolled as 3d6 straight on character creation; race modifiers applied before use.
///
/// SkillCap(stat): integer ceiling on any skill whose governing stat is `stat`.
/// Formula: floor(99 × stat / 18) — maps 3–18 to 17–99. See ADR-0019.
///
/// GoverningStats per skill (docs/gdd/skills.md §3):
///   Melee → Str, Ranged → Dex, Athletics → max(Str,Con), Woodcutting → Str,
///   Foraging → Wis, Cooking → Wis.
/// </summary>
public sealed record StatBlock(int Str, int Dex, int Con, int Wis)
{
    /// <summary>Skill-cap formula (ADR-0019): floor(99 × stat / 18).</summary>
    public static int SkillCap(int stat) => stat * 99 / 18;

    /// <summary>Clamp each stat to the valid AD&amp;D range 3–18 after applying race modifiers.</summary>
    public StatBlock Clamped() =>
        new(Clamp(Str), Clamp(Dex), Clamp(Con), Clamp(Wis));

    private static int Clamp(int v) => System.Math.Clamp(v, 3, 18);
}
