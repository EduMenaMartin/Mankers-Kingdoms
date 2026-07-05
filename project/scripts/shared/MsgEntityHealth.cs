namespace MankersKingdoms.Shared;

/// <summary>
/// Server → client health snapshot for one entity.
/// Not sent over the wire directly — individual fields are passed as RPC params
/// (same pattern as MsgPlayerState). Used for save state (M8) and test assertions.
///
/// EntityId: peer ID for players; MonsterSystem-assigned long for monsters.
/// </summary>
public sealed record MsgEntityHealth(long EntityId, float CurrentHp, float MaxHp)
{
    public HealthData ToHealthData() => new(CurrentHp, MaxHp);
}
