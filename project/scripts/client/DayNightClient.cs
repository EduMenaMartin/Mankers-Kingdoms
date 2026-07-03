using Godot;

namespace MankersKingdoms.Client;

/// <summary>
/// Receives day/night updates from DayNightSystem and adjusts the DirectionalLight3D.
/// Listens for the DayNightUpdated signal emitted by DayNightSystem (sibling node).
/// Visual-only: no gameplay effect in M2 (VERTICAL_SLICE §3.8).
/// </summary>
public partial class DayNightClient : Node
{
    private DirectionalLight3D _sun = null!;

    public override void _Ready()
    {
        _sun = GetNode<DirectionalLight3D>("../DirectionalLight3D");

        // Subscribe to DayNightSystem's signal without importing the server class.
        var dayNight = GetNode("../DayNightSystem");
        dayNight.Connect("DayNightUpdated", Callable.From<float, float>(OnDayNightUpdated));
    }

    private void OnDayNightUpdated(float worldTimeSec, float sunAngleDeg)
    {
        // Rotate the light so it tracks the sun across the sky.
        // sunAngleDeg: -90 = midnight (below horizon), 0 = horizon, +90 = noon (overhead).
        _sun.RotationDegrees = new Vector3(-sunAngleDeg, _sun.RotationDegrees.Y, 0f);

        // Energy: full at noon, off at night; smooth transition through horizon.
        float t = Mathf.Clamp(sunAngleDeg / 90f, 0f, 1f);
        _sun.LightEnergy = Mathf.Lerp(0f, 1.5f, t);
    }
}
