using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Client;

/// <summary>
/// HUD overlay showing the local player's HP bar.
/// Reads from LocalState (written by HealthSystem via RPC).
/// Rendered at Layer 10 — below NeedsHUD (15) and BuildMenu (20).
/// Add as a CanvasLayer child of GameWorld in the editor.
/// </summary>
public partial class HealthHUD : CanvasLayer
{
    private ProgressBar _hpBar   = null!;
    private Label       _hpLabel = null!;

    public override void _Ready()
    {
        Layer = 10;

        var vbox = new VBoxContainer();
        // Anchor top-left, matching the conventional HP position.
        vbox.AnchorLeft   = 0f;
        vbox.AnchorRight  = 0f;
        vbox.AnchorTop    = 0f;
        vbox.AnchorBottom = 0f;
        vbox.OffsetLeft   = 8f;
        vbox.OffsetRight  = 170f;
        vbox.OffsetTop    = 8f;
        vbox.OffsetBottom = 50f;
        vbox.AddThemeConstantOverride("separation", 2);
        AddChild(vbox);

        _hpLabel = new Label { Text = "HP", CustomMinimumSize = new Vector2(0f, 16f) };
        vbox.AddChild(_hpLabel);

        _hpBar = new ProgressBar
        {
            MinValue          = 0,
            MaxValue          = 100,
            Value             = 100,
            CustomMinimumSize = new Vector2(160f, 18f),
            ShowPercentage    = false
        };
        var style = new StyleBoxFlat { BgColor = new Color(0.75f, 0.1f, 0.1f) };
        _hpBar.AddThemeStyleboxOverride("fill", style);
        vbox.AddChild(_hpBar);
    }

    public override void _Process(double delta)
    {
        _hpBar.MaxValue = LocalState.MaxHp;
        _hpBar.Value    = LocalState.CurrentHp;
        _hpLabel.Text   = $"HP  {(int)LocalState.CurrentHp} / {(int)LocalState.MaxHp}";
    }
}
