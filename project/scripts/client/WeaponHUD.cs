using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Client;

/// <summary>
/// Displays the current combat/build mode and equipped weapon at the bottom-centre of the screen.
///
/// Format:
///   Build mode  →  "[Build Mode]"
///   Combat melee →  "[Combat · Melee · Sword]"
///   Combat ranged → "[Combat · Ranged · Shortbow]"
///
/// Polls LocalState each frame — no events needed; label text is assigned only when it changes.
///
/// Layer 13: above HealthHUD (10), below NeedsHUD (15).
/// Editor task: add a CanvasLayer node to GameWorld.tscn with script client/WeaponHUD.cs.
/// </summary>
public partial class WeaponHUD : CanvasLayer
{
    private Label _label = null!;
    private string _lastText = "";

    // Colours — applied via BBCode-style modulate on the label.
    private static readonly Color BUILD_COLOR  = new(0.65f, 0.85f, 1.00f); // soft blue
    private static readonly Color COMBAT_COLOR = new(1.00f, 0.42f, 0.35f); // warm red

    public override void _Ready()
    {
        Layer = 13;

        _label = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Bottom,
            AnchorLeft          = 0f,
            AnchorRight         = 1f,
            AnchorTop           = 0f,
            AnchorBottom        = 1f,
            OffsetBottom        = -8f,   // small bottom margin
        };
        AddChild(_label);

        Refresh();
    }

    public override void _Process(double _delta)
    {
        Refresh();
    }

    private void Refresh()
    {
        string text  = BuildText();
        Color  color = LocalState.InCombatMode ? COMBAT_COLOR : BUILD_COLOR;

        // Only write to the label when something actually changed to avoid per-frame GC churn.
        if (text != _lastText)
        {
            _label.Text          = text;
            _label.Modulate      = color;
            _lastText            = text;
        }
        else if (_label.Modulate != color)
        {
            _label.Modulate = color;
        }
    }

    private static string BuildText()
    {
        if (!LocalState.InCombatMode)
            return "[Build Mode]";

        string modeStr   = LocalState.PreferRanged ? "Ranged" : "Melee";
        string weaponStr = GetEquippedWeaponName();

        return string.IsNullOrEmpty(weaponStr)
            ? $"[Combat · {modeStr}]"
            : $"[Combat · {modeStr} · {weaponStr}]";
    }

    /// <summary>
    /// Returns the display name of the currently active weapon based on mode.
    /// Melee mode: first melee weapon found; Ranged mode: first ranged weapon found.
    /// Returns empty string when no matching weapon is in inventory.
    /// </summary>
    private static string GetEquippedWeaponName()
    {
        foreach (var w in WeaponRegistry.All)
        {
            bool modeMatch = LocalState.PreferRanged ? w.IsRanged : !w.IsRanged;
            if (modeMatch && LocalState.Inventory.Has(w.Id))
                return Loc.T(w.DisplayNameKey);
        }
        return "";
    }
}
