using Godot;

namespace MankersKingdoms.Client;

/// <summary>
/// Runtime-computed top-down camera: steep tilt + player-controlled orbit yaw.
/// Replaces the static Camera3D transform from M1 with a per-frame computation.
///
/// Instantiated as a child of the local player by PlayerController._Ready().
/// Gets the sibling Camera3D and the parent CharacterBody3D via the node tree.
///
/// Orbit input: middle-mouse-button drag rotates yaw. Movement and aim are
/// independent — WASD moves the character, mouse cursor aims weapons via the
/// existing BowController ground-plane raycast, which is camera-angle-agnostic
/// and requires no changes here. See ADR-0025.
/// </summary>
public partial class CameraController : Node
{
    // Tilt above the horizontal plane, in degrees. 65° = steep but not literally
    // top-down; same convention as Diablo/Path of Exile. Tune this visually.
    private const float TILT_ANGLE_DEG    = 65f;

    // Distance from player to camera along the tilt vector.
    private const float FOLLOW_DISTANCE_DEFAULT = 14f;
    private const float FOLLOW_DISTANCE_MIN     = 5f;
    private const float FOLLOW_DISTANCE_MAX     = 30f;
    private const float ZOOM_STEP               = 2f;

    // Scales mouse-delta-X → orbit yaw change per pixel. Tune for feel.
    private const float ORBIT_SENSITIVITY = 0.3f;

    private CharacterBody3D _player = null!;
    private Camera3D        _camera = null!;

    private float _orbitYaw;        // radians; 0 = camera behind player on +Z axis
    private float _followDistance = FOLLOW_DISTANCE_DEFAULT;
    private bool  _middleButtonHeld;

    public override void _Ready()
    {
        _player = GetParent<CharacterBody3D>();
        _camera = _player.GetNode<Camera3D>("Camera3D");
    }

    public override void _Process(double delta)
    {
        var playerPos  = _player.GlobalPosition;
        float tiltRad  = Mathf.DegToRad(TILT_ANGLE_DEG);

        float hDist = _followDistance * Mathf.Cos(tiltRad);
        float vDist = _followDistance * Mathf.Sin(tiltRad);

        var offset = new Vector3(
            hDist * Mathf.Sin(_orbitYaw),
            vDist,
            hDist * Mathf.Cos(_orbitYaw)
        );

        _camera.GlobalPosition = playerPos + offset;

        // Look at a point slightly above the player's feet so the character
        // is centred in frame rather than pushed to the bottom edge.
        _camera.LookAt(playerPos + new Vector3(0f, 0.5f, 0f), Vector3.Up);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mmb)
        {
            switch (mmb.ButtonIndex)
            {
                case MouseButton.Middle:
                    _middleButtonHeld = mmb.Pressed;
                    return;
                case MouseButton.WheelUp when mmb.Pressed:
                    _followDistance = Mathf.Clamp(_followDistance - ZOOM_STEP, FOLLOW_DISTANCE_MIN, FOLLOW_DISTANCE_MAX);
                    GetViewport().SetInputAsHandled();
                    return;
                case MouseButton.WheelDown when mmb.Pressed:
                    _followDistance = Mathf.Clamp(_followDistance + ZOOM_STEP, FOLLOW_DISTANCE_MIN, FOLLOW_DISTANCE_MAX);
                    GetViewport().SetInputAsHandled();
                    return;
            }
        }

        if (@event is InputEventMouseMotion motion && _middleButtonHeld)
        {
            // Negate X so dragging right rotates the camera clockwise (conventional).
            _orbitYaw -= motion.Relative.X * ORBIT_SENSITIVITY * 0.01f;
            GetViewport().SetInputAsHandled();
        }
    }
}
