using Godot;

namespace MankersKingdoms.Client;

/// <summary>
/// Displays floating combat numbers above attacked entities.
/// Receives ShowCombatResult RPCs from server combat systems (CombatSystem,
/// MonsterSystem, ProjectileSystem) and spawns a Label3D at the target's
/// world position that floats upward and fades out via Tween.
///
/// Normal hit  — yellow number, e.g. "7"
/// Critical hit — red, larger, number + "!", e.g. "14!"
/// Miss        — white "Miss"
///
/// The server passes the target's world position directly so the client
/// does not need to find the entity node by ID.
///
/// Node must be added to GameWorld.tscn in the editor (any position is fine —
/// it contains no visual of its own; only spawned Label3D children matter).
/// Layer order does not matter; Label3D nodes live in world space.
/// </summary>
public partial class CombatFeedbackHUD : Node
{
    public static CombatFeedbackHUD Instance { get; private set; } = null!;

    // Vertical offset above the entity's origin where the label spawns.
    private const float SPAWN_OFFSET_Y = 2.0f;
    // Additional upward travel during the animation.
    private const float FLOAT_DISTANCE = 1.2f;
    // Total animation duration in seconds.
    private const float ANIM_DURATION  = 1.2f;
    // Fade starts this fraction into the animation (0–1).
    private const float FADE_DELAY_F   = 0.35f;

    public override void _Ready()
    {
        Instance = this;
    }

    // ── RPC ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by the server on all peers (CallLocal = true so the host also sees it).
    /// targetPosition: world-space position of the entity that was attacked.
    /// hit: false = miss. damage: 0 on miss. isCrit: true = natural 20.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ShowCombatResult(Vector3 targetPosition, bool hit, int damage, bool isCrit)
    {
        SpawnLabel(targetPosition + Vector3.Up * SPAWN_OFFSET_Y, hit, damage, isCrit);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SpawnLabel(Vector3 worldPos, bool hit, int damage, bool isCrit)
    {
        var label = new Label3D();
        label.Billboard  = BaseMaterial3D.BillboardMode.Enabled;
        label.NoDepthTest = true;
        label.FixedSize  = true;

        if (!hit)
        {
            label.Text     = "Miss";
            label.Modulate = new Color(1f, 1f, 1f);   // white
            label.FontSize = 24;
        }
        else if (isCrit)
        {
            label.Text     = $"{damage}!";
            label.Modulate = new Color(1f, 0.15f, 0.1f);  // red
            label.FontSize = 32;
        }
        else
        {
            label.Text     = damage.ToString();
            label.Modulate = new Color(1f, 0.9f, 0.1f);  // yellow
            label.FontSize = 24;
        }

        label.GlobalPosition = worldPos;
        AddChild(label);

        // Float up while fading out, then free the node.
        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(label, "global_position",
            worldPos + Vector3.Up * FLOAT_DISTANCE, ANIM_DURATION);
        tween.TweenProperty(label, "modulate:a", 0f, ANIM_DURATION * (1f - FADE_DELAY_F))
             .SetDelay(ANIM_DURATION * FADE_DELAY_F);
        tween.SetParallel(false);
        tween.TweenCallback(Callable.From(label.QueueFree));
    }
}
