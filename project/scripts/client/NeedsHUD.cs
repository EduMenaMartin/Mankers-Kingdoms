using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Client;

/// <summary>
/// HUD overlay showing the local player's Hunger and Rest bars.
/// Reads from LocalState (written by NeedsSystem via RPC).
/// Rendered at Layer 15 — above InventoryHUD (10), below BuildMenu (20).
/// Add as a CanvasLayer child of GameWorld in the editor.
/// </summary>
public partial class NeedsHUD : CanvasLayer
{
    private ProgressBar _hungerBar = null!;
    private ProgressBar _restBar   = null!;

    public override void _Ready()
    {
        Layer = 15;

        var vbox = new VBoxContainer();
        // Anchor to bottom-right corner.
        vbox.AnchorLeft   = 1f;
        vbox.AnchorRight  = 1f;
        vbox.AnchorTop    = 1f;
        vbox.AnchorBottom = 1f;
        vbox.OffsetLeft   = -168f;
        vbox.OffsetRight  = -8f;
        vbox.OffsetTop    = -80f;
        vbox.OffsetBottom = -8f;
        vbox.AddThemeConstantOverride("separation", 4);
        AddChild(vbox);

        vbox.AddChild(MakeLabel("Hunger"));
        _hungerBar = MakeBar(new Color(0.85f, 0.3f, 0.1f));
        vbox.AddChild(_hungerBar);

        vbox.AddChild(MakeLabel("Rest"));
        _restBar = MakeBar(new Color(0.2f, 0.5f, 0.9f));
        vbox.AddChild(_restBar);
    }

    public override void _Process(double delta)
    {
        _hungerBar.Value = LocalState.Hunger;
        _restBar.Value   = LocalState.Rest;
    }

    private static Label MakeLabel(string text) =>
        new() { Text = text, CustomMinimumSize = new Vector2(0f, 16f) };

    private static ProgressBar MakeBar(Color fillColor)
    {
        var bar = new ProgressBar
        {
            MinValue             = 0,
            MaxValue             = 100,
            Value                = 100,
            CustomMinimumSize    = new Vector2(160f, 18f),
            ShowPercentage       = false
        };
        // Tint the fill using a theme override so it's visible without a custom theme.
        var style = new StyleBoxFlat { BgColor = fillColor };
        bar.AddThemeStyleboxOverride("fill", style);
        return bar;
    }
}
