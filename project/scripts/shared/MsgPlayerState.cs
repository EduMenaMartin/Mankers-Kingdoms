namespace MankersKingdoms.Shared;

/// <summary>
/// Server → All Clients: authoritative snapshot of a single player's world position.
/// Remote clients interpolate toward the received position.
/// Local client uses it to reconcile against its client-predicted position.
/// Stored as flat floats to avoid a Godot.Vector3 dependency in the test project.
/// See ADR-0005: server-authoritative, client prediction/interpolation.
/// </summary>
public readonly struct MsgPlayerState
{
    /// <summary>Godot ENet peer ID of the player this state describes.</summary>
    public readonly long PeerId;

    public readonly float X;
    public readonly float Y;
    public readonly float Z;

    /// <summary>Server tick at which this state was computed.</summary>
    public readonly uint Tick;

    public MsgPlayerState(long peerId, float x, float y, float z, uint tick)
    {
        PeerId = peerId;
        X = x;
        Y = y;
        Z = z;
        Tick = tick;
    }
}
