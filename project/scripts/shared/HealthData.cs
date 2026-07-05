namespace MankersKingdoms.Shared;

/// <summary>
/// Snapshot of one entity's health state.
/// Pure C# — no Godot dependency, usable in xUnit and save/load (M8).
/// EntityId: peer ID for players; MonsterSystem-assigned ID for monsters.
/// </summary>
public sealed record HealthData(float CurrentHp, float MaxHp)
{
    public bool  IsDead   => CurrentHp <= 0f;

    /// <summary>0.0 – 1.0 fraction. 0 when MaxHp is zero (degenerate guard).</summary>
    public float Fraction => MaxHp > 0f ? CurrentHp / MaxHp : 0f;
}
