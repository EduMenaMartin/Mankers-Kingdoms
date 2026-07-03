using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Server;

/// <summary>
/// Advances world time and broadcasts the sun angle to all clients for visual day/night.
/// Runs only on the server (host or dedicated). Clients receive updates via UpdateDayNight RPC.
///
/// Day length: REAL_SECONDS_PER_DAY real seconds = one in-game day.
/// SunAngleDeg: -90 = midnight, 0 = horizon (dawn/dusk), +90 = noon.
/// </summary>
public partial class DayNightSystem : Node
{
    private const float REAL_SECONDS_PER_DAY = 120f; // 2-minute days; tune later
    private const float BROADCAST_INTERVAL   = 1f;   // seconds between RPC updates

    private float _worldTimeSec;
    private float _timeSinceLastBroadcast;

    public override void _Process(double delta)
    {
        if (!Multiplayer.IsServer()) return;

        _worldTimeSec        = (_worldTimeSec + (float)delta) % REAL_SECONDS_PER_DAY;
        _timeSinceLastBroadcast += (float)delta;

        if (_timeSinceLastBroadcast < BROADCAST_INTERVAL) return;
        _timeSinceLastBroadcast = 0f;

        float sunAngle = ComputeSunAngle(_worldTimeSec);
        Rpc(MethodName.UpdateDayNight, _worldTimeSec, sunAngle);
    }

    private static float ComputeSunAngle(float timeSec)
    {
        // sin wave: -90 at midnight (t=0), +90 at noon (t=0.5 day), -90 at next midnight
        float phase = (float)(System.Math.PI * 2.0 * timeSec / REAL_SECONDS_PER_DAY);
        return (float)System.Math.Sin(phase - System.Math.PI / 2.0) * 90f;
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
         TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void UpdateDayNight(float worldTimeSec, float sunAngleDeg)
    {
        // Clients listen for this on DayNightClient, which is a sibling node.
        // We emit a signal so DayNightClient doesn't need to import this server class.
        EmitSignal(SignalName.DayNightUpdated, worldTimeSec, sunAngleDeg);
    }

    [Signal] public delegate void DayNightUpdatedEventHandler(float worldTimeSec, float sunAngleDeg);
}
