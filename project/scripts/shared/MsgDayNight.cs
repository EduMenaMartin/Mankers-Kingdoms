namespace MankersKingdoms.Shared;

/// <summary>
/// Server → all clients: current sun angle for day/night visual update.
/// Flat DTO — no Godot dependency, testable.
/// SunAngleDeg: degrees above (+) or below (-) the horizon. Range [-90, +90].
///   -90 = midnight, 0 = horizon (dawn/dusk), +90 = noon.
/// </summary>
public sealed class MsgDayNight
{
    public float WorldTimeSec { get; init; }
    public float SunAngleDeg  { get; init; }
}
