namespace MankersKingdoms.Shared;

/// <summary>
/// Client → Server: movement intent from the owning peer for a single physics tick.
/// The server validates and applies this input authoritatively.
/// Stored as flat floats to avoid a Godot.Vector2 dependency in the test project.
/// See ADR-0005: authoritative host, client prediction.
/// </summary>
public readonly struct MsgPlayerInput
{
    /// <summary>Horizontal movement axis (-1 = left, +1 = right). Not normalized; server normalizes.</summary>
    public readonly float DirectionX;

    /// <summary>Forward/back movement axis (-1 = forward / -Z, +1 = back / +Z).</summary>
    public readonly float DirectionZ;

    /// <summary>Client-side physics tick counter at time of send. Used for future reconciliation.</summary>
    public readonly uint Tick;

    public MsgPlayerInput(float directionX, float directionZ, uint tick)
    {
        DirectionX = directionX;
        DirectionZ = directionZ;
        Tick = tick;
    }
}
