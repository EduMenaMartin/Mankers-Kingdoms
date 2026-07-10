using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Client;

/// <summary>
/// Drives the Player's AnimationPlayer based on movement velocity and combat events.
/// Presentation layer only — reads LocalState events and CharacterBody3D physics state;
/// never touches server-authoritative logic (ARCHITECTURE.md §3.2).
///
/// Attached as a child of the local Player node by PlayerController._Ready().
///
/// Scene layout this script assumes:
///   Player (CharacterBody3D)
///   └── CharacterRig (Node3D)
///       ├── Rig_Medium/Skeleton3D/KnightMeshes  ← visible for Fighter
///       ├── Rig_Medium/Skeleton3D/RangerMeshes  ← visible for Ranger
///       └── AnimationPlayer                     ← root_node=".." (CharacterRig)
///
/// Animation libraries (Godot 4 named library prefix required in Play() calls):
///   Rig_Medium_General:       Idle_A, Hit_A/B, Death_A/B, Throw
///   Rig_Medium_MovementBasic: Walking_A/B/C, Running_A/B,
///                             Jump_Start, Jump_Idle, Jump_Land
///
/// State priority (high → low): Dead > HitStun > Throw > Jumping > Running > Walking > Idle
///
/// NOTE — Throw vs. bow: the pack has no dedicated Shoot/DrawBow clip. Throw is used as a
/// placeholder for ranged attacks. It reads as a forward arm release and is acceptable at
/// this camera distance. Swap the THROW constant if a better clip is sourced later.
/// </summary>
public partial class PlayerAnimator : Node
{
    // ── Clip name constants (library/clip format required by Godot 4 animation libraries) ──

    private const string LIB_GEN = "Rig_Medium_General";
    private const string LIB_MOV = "Rig_Medium_MovementBasic";

    private const string IDLE_A     = LIB_GEN + "/Idle_A";
    private const string HIT_A      = LIB_GEN + "/Hit_A";
    private const string HIT_B      = LIB_GEN + "/Hit_B";
    private const string DEATH_A    = LIB_GEN + "/Death_A";
    private const string DEATH_B    = LIB_GEN + "/Death_B";
    private const string THROW      = LIB_GEN + "/Throw"; // ranged attack placeholder — see class doc

    private const string WALK_A     = LIB_MOV + "/Walking_A";
    private const string WALK_B     = LIB_MOV + "/Walking_B";
    private const string WALK_C     = LIB_MOV + "/Walking_C";
    private const string RUN_A      = LIB_MOV + "/Running_A";
    private const string RUN_B      = LIB_MOV + "/Running_B";
    private const string JUMP_START = LIB_MOV + "/Jump_Start";
    private const string JUMP_IDLE  = LIB_MOV + "/Jump_Idle";
    private const string JUMP_LAND  = LIB_MOV + "/Jump_Land";

    // ── Node paths relative to Player root ───────────────────────────────────

    private const string ANIM_PATH   = "CharacterRig/AnimationPlayer";
    private const string KNIGHT_MESH = "CharacterRig/Rig_Medium/Skeleton3D/KnightMeshes";
    private const string RANGER_MESH = "CharacterRig/Rig_Medium/Skeleton3D/RangerMeshes";

    // ── Velocity thresholds (metres/sec) ─────────────────────────────────────

    /// <summary>Horizontal speed below which Idle plays.</summary>
    private const float IDLE_THRESHOLD = 0.15f;

    /// <summary>
    /// Horizontal speed above which Running plays.
    /// Set above current SPEED (5 f/s) so Walking plays at normal movement.
    /// Running will activate once sprint is implemented.
    /// </summary>
    private const float RUN_THRESHOLD = 7.0f;

    // ── Internal state ────────────────────────────────────────────────────────

    private enum AnimState { Idle, Walking, Running, Jumping, HitStun, Throwing, Dead }
    private AnimState _state = AnimState.Idle;

    private AnimationPlayer _anim   = null!;
    private CharacterBody3D _player = null!;
    private Camera3D        _camera = null!;
    private Node3D          _rig    = null!;

    private bool _wasOnFloor = true; // assume grounded at start to prevent spurious jump trigger
    private bool _isDead;

    // Counters for alternating variety clips (wrap modulo clip count in each method).
    private int _walkCycle;
    private int _runCycle;
    private int _hitCycle;
    private int _deathCycle;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        _player = GetParent<CharacterBody3D>();
        _anim   = _player.GetNode<AnimationPlayer>(ANIM_PATH);
        _camera = _player.GetNode<Camera3D>("Camera3D");
        _rig    = _player.GetNode<Node3D>("CharacterRig");

        ApplyCharacterMesh();

        LocalState.DamageTaken    += OnDamageTaken;
        LocalState.PlayerDied     += OnPlayerDied;
        LocalState.PlayerRevived  += OnPlayerRevived;
        LocalState.LocalArrowFired += OnLocalArrowFired;

        _anim.AnimationFinished += OnAnimationFinished;

        // Start in idle so the character has a pose immediately.
        _anim.Play(IDLE_A);
    }

    public override void _ExitTree()
    {
        LocalState.DamageTaken     -= OnDamageTaken;
        LocalState.PlayerDied      -= OnPlayerDied;
        LocalState.PlayerRevived   -= OnPlayerRevived;
        LocalState.LocalArrowFired -= OnLocalArrowFired;

        if (IsInstanceValid(_anim))
            _anim.AnimationFinished -= OnAnimationFinished;
    }

    // ── Per-frame ─────────────────────────────────────────────────────────────

    public override void _Process(double _delta)
    {
        if (_isDead) return;

        bool onFloor = _player.IsOnFloor();

        // Detect leaving the ground (but don't interrupt HitStun or Throw with a jump).
        if (_wasOnFloor && !onFloor &&
            _state != AnimState.HitStun && _state != AnimState.Throwing)
        {
            EnterJumping();
        }
        else if (!_wasOnFloor && onFloor)
        {
            Land();
        }
        _wasOnFloor = onFloor;

        // Only blend movement states when no higher-priority state is active.
        if (_state == AnimState.Idle || _state == AnimState.Walking || _state == AnimState.Running)
            UpdateMovementState();

        FaceMouseCursor();
    }

    // ── Mouse facing ─────────────────────────────────────────────────────────

    /// <summary>
    /// Rotates CharacterRig around Y so the model faces the mouse cursor.
    /// Projects the cursor onto a horizontal plane at floor height (same approach as
    /// BowController.ComputeAimDirection). The KayKit model faces +Z at Y-rotation=0,
    /// so we use Atan2(dir.X, dir.Z) rather than LookAt (which points -Z at target).
    /// </summary>
    private void FaceMouseCursor()
    {
        var mousePos  = _camera.GetViewport().GetMousePosition();
        var rayDir    = _camera.ProjectRayNormal(mousePos);
        var rayOrigin = _camera.GlobalPosition;

        var    aimPlane  = new Plane(Vector3.Up, _player.GlobalPosition.Y);
        var    worldPt   = aimPlane.IntersectsRay(rayOrigin, rayDir);
        if (worldPt == null) return;

        var dir = new Vector3(
            worldPt.Value.X - _player.GlobalPosition.X,
            0f,
            worldPt.Value.Z - _player.GlobalPosition.Z);

        if (dir.LengthSquared() < 0.01f) return;

        _rig.Rotation = new Vector3(0f, Mathf.Atan2(dir.X, dir.Z), 0f);
    }

    // ── Movement state ────────────────────────────────────────────────────────

    private void UpdateMovementState()
    {
        float hSpeed = new Vector2(_player.Velocity.X, _player.Velocity.Z).Length();

        AnimState target;
        if      (hSpeed >= RUN_THRESHOLD)  target = AnimState.Running;
        else if (hSpeed >= IDLE_THRESHOLD) target = AnimState.Walking;
        else                               target = AnimState.Idle;

        if (target == _state) return;

        _state = target;
        PlayMovementClip();
    }

    private void PlayMovementClip()
    {
        switch (_state)
        {
            case AnimState.Idle:
                _anim.Play(IDLE_A);
                break;

            case AnimState.Walking:
                // Cycle A → B → C → A each time Walking is entered (not every frame).
                _anim.Play(_walkCycle switch { 0 => WALK_A, 1 => WALK_B, _ => WALK_C });
                _walkCycle = (_walkCycle + 1) % 3;
                break;

            case AnimState.Running:
                _anim.Play(_runCycle == 0 ? RUN_A : RUN_B);
                _runCycle = (_runCycle + 1) % 2;
                break;
        }
    }

    // ── Jump ─────────────────────────────────────────────────────────────────

    private void EnterJumping()
    {
        _state = AnimState.Jumping;
        _anim.Play(JUMP_START);
        _anim.Queue(JUMP_IDLE); // auto-plays after Jump_Start completes; loops while airborne
    }

    private void Land()
    {
        if (_state != AnimState.Jumping) return;
        _anim.Play(JUMP_LAND); // OnAnimationFinished returns to movement after this completes
    }

    // ── Combat event handlers ─────────────────────────────────────────────────

    private void OnDamageTaken()
    {
        if (_isDead) return;
        _state = AnimState.HitStun;
        _anim.Play(_hitCycle == 0 ? HIT_A : HIT_B);
        _hitCycle = (_hitCycle + 1) % 2;
    }

    private void OnPlayerDied()
    {
        _isDead = true;
        _state  = AnimState.Dead;
        _anim.Play(_deathCycle == 0 ? DEATH_A : DEATH_B);
        _deathCycle = (_deathCycle + 1) % 2;
    }

    private void OnPlayerRevived()
    {
        _isDead     = false;
        _wasOnFloor = true; // safe assumption — respawn always places player on ground
        _state      = AnimState.Idle;
        _anim.Play(IDLE_A);
    }

    private void OnLocalArrowFired()
    {
        // Don't interrupt a death or an incoming hit — they have higher priority.
        if (_isDead || _state == AnimState.HitStun) return;
        _state = AnimState.Throwing;
        _anim.Play(THROW);
    }

    // ── Animation finished callback ───────────────────────────────────────────

    private void OnAnimationFinished(StringName animName)
    {
        // One-shot clips that return to movement on completion.
        // Death clips and Jump_Idle are not listed here:
        //   Death_A/B → hold final pose (or loop), cleared only by PlayerRevived.
        //   Jump_Idle → queued by EnterJumping(); lands via IsOnFloor() edge detection.
        if (animName == HIT_A      || animName == HIT_B  ||
            animName == THROW      || animName == JUMP_LAND)
        {
            _state = AnimState.Idle; // _Process/UpdateMovementState corrects to Walk/Run next frame
            PlayMovementClip();
        }
    }

    // ── Character mesh selection ──────────────────────────────────────────────

    /// <summary>
    /// Shows the mesh group matching the chosen class; hides the others.
    /// Uses GetNodeOrNull so this is a no-op if the mesh groups haven't been added to the
    /// scene yet — the code is safe to compile and run before the editor work is complete.
    /// </summary>
    private void ApplyCharacterMesh()
    {
        var knightMeshes = _player.GetNodeOrNull<Node3D>(KNIGHT_MESH);
        var rangerMeshes = _player.GetNodeOrNull<Node3D>(RANGER_MESH);

        bool isRanger = GameSession.ChosenClassId == "class.ranger";

        if (knightMeshes != null) knightMeshes.Visible = !isRanger;
        if (rangerMeshes != null) rangerMeshes.Visible =  isRanger;
    }
}
