using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Client;

/// <summary>
/// In-game pause menu CanvasLayer (M8).
///
/// Toggled by Escape (ui_cancel). Renders above all other HUD layers (Layer = 50).
/// Must be added to GameWorld.tscn as a CanvasLayer node with this script attached.
///
/// Buttons:
///   Resume          — close the pause menu
///   Save Game       — calls SaveSystem.Instance.Save()
///   Load Game       — shows LoadGamePanel; on confirm, reloads GameWorld
///   Quit to Menu    — returns to MainMenu.tscn
/// </summary>
public partial class PauseMenu : CanvasLayer
{
    // Panels checked before PauseMenu opens — if any is visible, ESC goes to that panel instead.
    private static readonly string[] GAME_PANEL_PATHS =
    [
        "/root/GameWorld/WorldMapScreen",
        "/root/GameWorld/BuildingAssignmentPanel",
        "/root/GameWorld/StockpilePanel",
        "/root/GameWorld/RecruitmentDialogue",
        "/root/GameWorld/InventoryPanel",
        "/root/GameWorld/CharacterSheet",
        "/root/GameWorld/BuildMenu",
    ];

    private Control       _root        = null!;
    private LoadGamePanel _loadPanel   = null!;

    public override void _Ready()
    {
        Layer = 50;
        BuildUi();
        // Hidden until Escape is pressed.
        _root.Hide();
    }

    public override void _UnhandledInput(InputEvent ev)
    {
        if (ev.IsActionPressed("ui_cancel"))
        {
            if (_loadPanel.Visible)
            {
                _loadPanel.Hide();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (_root.Visible)
            {
                Close();
                GetViewport().SetInputAsHandled();
                return;
            }

            // Only open when no other game panel is visible.
            // Do NOT consume the event if a panel is open — let the panel handle ESC itself.
            if (AnyGamePanelVisible()) return;

            Open();
            GetViewport().SetInputAsHandled();
        }
    }

    private bool AnyGamePanelVisible()
    {
        foreach (var path in GAME_PANEL_PATHS)
        {
            var node = GetNodeOrNull<CanvasLayer>(path);
            if (node != null && node.Visible) return true;
        }
        return false;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Open()
    {
        _root.Show();
        GetTree().Paused = true;
    }

    public void Close()
    {
        _loadPanel.Hide();
        _root.Hide();
        GetTree().Paused = false;
    }

    // ── Build ─────────────────────────────────────────────────────────────────

    private void BuildUi()
    {
        // Full-screen backdrop.
        _root = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.55f),
            AnchorLeft   = 0f,
            AnchorTop    = 0f,
            AnchorRight  = 1f,
            AnchorBottom = 1f,
            ProcessMode  = ProcessModeEnum.Always // keep visible while tree is paused
        };
        AddChild(_root);

        var panel = new PanelContainer();
        panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        panel.CustomMinimumSize = new Vector2(320f, 0f);
        panel.ProcessMode = ProcessModeEnum.Always;
        _root.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left",   24);
        margin.AddThemeConstantOverride("margin_right",  24);
        margin.AddThemeConstantOverride("margin_top",    24);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        panel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 12);
        margin.AddChild(vbox);

        var title = new Label { Text = Loc.T("pause.title") };
        title.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(title);

        AddMenuButton(vbox, "pause.resume",       OnResume);
        AddMenuButton(vbox, "pause.save",         OnSave);
        AddMenuButton(vbox, "pause.load",         OnLoad);
        AddMenuButton(vbox, "pause.quit_to_menu", OnQuitToMenu);

        // Load panel sits on top (initially hidden).
        _loadPanel = new LoadGamePanel { ProcessMode = ProcessModeEnum.Always };
        _loadPanel.OnSaveSelected = OnSaveChosen;
        _loadPanel.OnCancel       = () => _loadPanel.Hide();
        _loadPanel.Hide();
        AddChild(_loadPanel);
    }

    private static void AddMenuButton(VBoxContainer parent, string locKey, System.Action handler)
    {
        var btn = new Button
        {
            Text        = Loc.T(locKey),
            ProcessMode = ProcessModeEnum.Always
        };
        btn.Pressed += handler;
        parent.AddChild(btn);
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    private void OnResume() => Close();

    private void OnSave()
    {
        GameSession.RequestSave();
        GD.Print("[PauseMenu] Manual save requested");
        Close();
    }

    private void OnLoad() => _loadPanel.ShowAndRefresh();

    private void OnSaveChosen(string saveName)
    {
        // SaveName + session already set by LoadGamePanel.OnLoadPressed().
        GameSession.Intent = GameSession.SessionIntent.Solo;
        GetTree().Paused   = false;
        GetTree().ChangeSceneToFile("res://scenes/GameWorld.tscn");
    }

    private void OnQuitToMenu()
    {
        // Request a save before leaving so progress isn't lost.
        GameSession.RequestSave();
        GetTree().Paused = false;
        GameSession.Reset();
        GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
    }
}
