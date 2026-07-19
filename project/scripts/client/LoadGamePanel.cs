using System;
using System.Collections.Generic;
using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Client;

/// <summary>
/// Programmatic save-selection overlay (M8).
///
/// Shows a scrollable list of save slots with timestamps.
/// Selecting a slot and pressing Load invokes OnSaveSelected with the slot name.
/// Pressing Cancel invokes OnCancel.
///
/// Instantiate via `new LoadGamePanel()` and add as a child — no .tscn required.
/// Call Show() / Hide() to toggle visibility.
/// </summary>
public partial class LoadGamePanel : Control
{
    /// <summary>Invoked with the chosen save name (no .json extension).</summary>
    public Action<string>? OnSaveSelected;

    /// <summary>Invoked when the user presses Cancel.</summary>
    public Action? OnCancel;

    private ItemList  _saveList     = null!;
    private Button    _loadButton   = null!;
    private Button    _deleteButton = null!;
    private Button    _cancelButton = null!;
    private Label     _titleLabel   = null!;
    private Label     _noSavesLabel = null!;

    private List<string> _saves = new();

    public override void _Ready()
    {
        BuildUi();
        Refresh();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Reload the save list from disk and show the panel.</summary>
    public void ShowAndRefresh()
    {
        Refresh();
        Show();
    }

    // ── Build ─────────────────────────────────────────────────────────────────

    private void BuildUi()
    {
        // Full-viewport semi-transparent backdrop.
        AnchorLeft   = 0f;
        AnchorTop    = 0f;
        AnchorRight  = 1f;
        AnchorBottom = 1f;

        var backdrop = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.65f),
            AnchorLeft   = 0f,
            AnchorTop    = 0f,
            AnchorRight  = 1f,
            AnchorBottom = 1f
        };
        AddChild(backdrop);

        // Centered panel.
        var panel = new PanelContainer();
        panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        panel.CustomMinimumSize = new Vector2(480f, 400f);
        AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left",   16);
        margin.AddThemeConstantOverride("margin_right",  16);
        margin.AddThemeConstantOverride("margin_top",    16);
        margin.AddThemeConstantOverride("margin_bottom", 16);
        panel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 12);
        margin.AddChild(vbox);

        _titleLabel = new Label { Text = Loc.T("load_panel.title") };
        _titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(_titleLabel);

        _saveList = new ItemList();
        _saveList.CustomMinimumSize = new Vector2(0f, 260f);
        _saveList.ItemSelected    += OnItemSelected;
        _saveList.ItemActivated   += _ => OnLoadPressed(); // double-click loads immediately
        vbox.AddChild(_saveList);

        _noSavesLabel = new Label { Text = Loc.T("load_panel.no_saves") };
        _noSavesLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _noSavesLabel.Visible = false;
        vbox.AddChild(_noSavesLabel);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 8);
        vbox.AddChild(hbox);

        // Spacer pushes buttons to the right.
        var spacer = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        hbox.AddChild(spacer);

        _deleteButton = new Button { Text = Loc.T("load_panel.delete"), Disabled = true };
        _deleteButton.Pressed += OnDeletePressed;
        hbox.AddChild(_deleteButton);

        _loadButton = new Button { Text = Loc.T("load_panel.load"), Disabled = true };
        _loadButton.Pressed += OnLoadPressed;
        hbox.AddChild(_loadButton);

        _cancelButton = new Button { Text = Loc.T("load_panel.cancel") };
        _cancelButton.Pressed += OnCancelPressed;
        hbox.AddChild(_cancelButton);
    }

    // ── Data ──────────────────────────────────────────────────────────────────

    private void Refresh()
    {
        _saves = SaveUtil.ListSaves();
        _saveList.Clear();
        _loadButton.Disabled   = true;
        _deleteButton.Disabled = true;

        if (_saves.Count == 0)
        {
            _noSavesLabel.Visible = true;
            _saveList.Visible     = false;
            return;
        }

        _noSavesLabel.Visible = false;
        _saveList.Visible     = true;

        foreach (var name in _saves)
        {
            // Display name: strip "save_" prefix and reformat timestamp for readability.
            var displayName = FormatSaveName(name);
            var session     = SaveUtil.PeekSession(name);
            var detail      = session != null
                ? $"  [{session.ChosenClassId.Replace("class.", "")} / {session.ChosenRaceId.Replace("race.", "")}]"
                : "";
            _saveList.AddItem($"{displayName}{detail}");
        }

        // Auto-select the most recent save (first in the list) so Load is immediately clickable.
        _saveList.Select(0);
        _loadButton.Disabled   = false;
        _deleteButton.Disabled = false;
    }

    private static string FormatSaveName(string name)
    {
        // "save_20260113_142530" → "2026-01-13  14:25:30"
        if (name.StartsWith("save_") && name.Length == 20)
        {
            var d = name.AsSpan(5); // "20260113_142530"
            return $"{d[..4]}-{d[4..6]}-{d[6..8]}  {d[9..11]}:{d[11..13]}:{d[13..15]}";
        }
        return name;
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    private void OnItemSelected(long index)
    {
        _loadButton.Disabled   = false;
        _deleteButton.Disabled = false;
    }

    private void OnDeletePressed()
    {
        int idx = _saveList.GetSelectedItems() is { Length: > 0 } sel ? sel[0] : -1;
        if (idx < 0 || idx >= _saves.Count) return;

        var name = _saves[idx];
        using var dir = DirAccess.Open(SaveUtil.SaveDir);
        dir?.Remove($"{name}.json");

        Refresh();
    }

    private void OnLoadPressed()
    {
        int idx = _saveList.GetSelectedItems() is { Length: > 0 } sel ? sel[0] : -1;
        if (idx < 0 || idx >= _saves.Count) return;

        var name = _saves[idx];

        // Restore session character data from the save before entering GameWorld.
        var session = SaveUtil.PeekSession(name);
        if (session != null)
        {
            GameSession.ChosenClassId   = session.ChosenClassId;
            GameSession.ChosenRaceId    = session.ChosenRaceId;
            GameSession.RolledStats     = session.RolledStats;
            GameSession.HumanChosenStat = session.HumanChosenStat;
        }
        GameSession.SaveName = name;

        OnSaveSelected?.Invoke(name);
    }

    private void OnCancelPressed()
    {
        OnCancel?.Invoke();
    }
}
