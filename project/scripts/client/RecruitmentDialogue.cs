using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Client;

/// <summary>
/// Recruitment dialogue shown when the player presses E near a villager.
/// Displays name, archetype, and rolled stats. Recruit/Leave buttons send RPCs
/// to VillageSystem. Escape closes without action.
///
/// All display data is read from node metadata set by VillageSystem._Ready():
///   villager_id, villager_name, archetype_name_key, stat_str/dex/con/wis.
///
/// Layer 28 — above inventory (25), character sheet (26), stockpile panel (29 Phase 3).
/// Editor task: add as CanvasLayer child of GameWorld root, attach this script.
/// </summary>
public partial class RecruitmentDialogue : CanvasLayer
{
    private const string VILLAGE_SYSTEM_PATH = "/root/GameWorld/VillageSystem";

    private string _currentVillagerId = "";

    // UI nodes built in _Ready
    private Label  _nameLabel      = null!;
    private Label  _archetypeLabel = null!;
    private Label  _strLabel       = null!;
    private Label  _dexLabel       = null!;
    private Label  _conLabel       = null!;
    private Label  _wisLabel       = null!;
    private Button _recruitBtn     = null!;
    private Button _leaveBtn       = null!;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        Layer   = 28;
        Visible = false;

        // ── Root panel (centred) ─────────────────────────────────────────────
        var panel = new PanelContainer();
        panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        panel.CustomMinimumSize = new Vector2(340f, 0f);
        AddChild(panel);

        var root = new VBoxContainer { CustomMinimumSize = new Vector2(320f, 0f) };
        root.AddThemeConstantOverride("separation", 8);
        panel.AddChild(root);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_top",    12);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        margin.AddThemeConstantOverride("margin_left",   16);
        margin.AddThemeConstantOverride("margin_right",  16);
        root.AddChild(margin);

        var inner = new VBoxContainer();
        inner.AddThemeConstantOverride("separation", 10);
        margin.AddChild(inner);

        // Name
        _nameLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode        = TextServer.AutowrapMode.Off,
        };
        _nameLabel.AddThemeFontSizeOverride("font_size", 22);
        inner.AddChild(_nameLabel);

        // Archetype subtitle
        _archetypeLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate            = new Color(0.75f, 0.85f, 1f),
        };
        inner.AddChild(_archetypeLabel);

        inner.AddChild(new HSeparator());

        // Stats header
        var statsHeader = new Label { Text = Loc.T("charSheet.stats") };
        statsHeader.AddThemeFontSizeOverride("font_size", 14);
        inner.AddChild(statsHeader);

        // Stat rows
        _strLabel = AddStatRow(inner, Loc.T("charCreate.stat.str"));
        _dexLabel = AddStatRow(inner, Loc.T("charCreate.stat.dex"));
        _conLabel = AddStatRow(inner, Loc.T("charCreate.stat.con"));
        _wisLabel = AddStatRow(inner, Loc.T("charCreate.stat.wis"));

        inner.AddChild(new HSeparator());

        // Buttons
        var btnRow = new HBoxContainer();
        btnRow.AddThemeConstantOverride("separation", 8);
        inner.AddChild(btnRow);

        _recruitBtn = new Button { Text = Loc.T("recruit.btn_recruit"), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _recruitBtn.Pressed += OnRecruitPressed;
        btnRow.AddChild(_recruitBtn);

        _leaveBtn = new Button { Text = Loc.T("recruit.btn_leave"), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _leaveBtn.Pressed += OnLeavePressed;
        btnRow.AddChild(_leaveBtn);

        // Hint
        var hint = new Label
        {
            Text                = Loc.T("recruit.close_hint"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate            = new Color(0.6f, 0.6f, 0.6f),
        };
        hint.AddThemeFontSizeOverride("font_size", 12);
        inner.AddChild(hint);

        // Subscribe so Leave button stays in sync if the follower changes while dialogue is open.
        LocalState.FollowerChanged += _ => RefreshButtonState();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible) return;
        if (@event.IsActionPressed("ui_cancel"))
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens the dialogue populated with data from the given VillagerNode.
    /// Called by PlayerController when E is pressed near a villager.
    /// </summary>
    public void Open(Node3D villagerNode)
    {
        _currentVillagerId = villagerNode.GetMeta("villager_id", "").AsString();

        string name         = villagerNode.GetMeta("villager_name",     "?").AsString();
        string archetypeKey = villagerNode.GetMeta("archetype_name_key", "").AsString();
        int    str          = villagerNode.GetMeta("stat_str", 10).AsInt32();
        int    dex          = villagerNode.GetMeta("stat_dex", 10).AsInt32();
        int    con          = villagerNode.GetMeta("stat_con", 10).AsInt32();
        int    wis          = villagerNode.GetMeta("stat_wis", 10).AsInt32();

        _nameLabel.Text      = name;
        _archetypeLabel.Text = Loc.T(archetypeKey);

        // Determine the highest stat value to mark with ★
        int maxStat = System.Math.Max(System.Math.Max(str, dex), System.Math.Max(con, wis));
        _strLabel.Text = FormatStat(str, str == maxStat);
        _dexLabel.Text = FormatStat(dex, dex == maxStat);
        _conLabel.Text = FormatStat(con, con == maxStat);
        _wisLabel.Text = FormatStat(wis, wis == maxStat);

        // Colour highest-stat row gold
        _strLabel.Modulate = str == maxStat ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 1f, 1f);
        _dexLabel.Modulate = dex == maxStat ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 1f, 1f);
        _conLabel.Modulate = con == maxStat ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 1f, 1f);
        _wisLabel.Modulate = wis == maxStat ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 1f, 1f);

        RefreshButtonState();
        Visible = true;
    }

    public void Close()
    {
        Visible            = false;
        _currentVillagerId = "";
    }

    // ── Button handlers ───────────────────────────────────────────────────────

    private void OnRecruitPressed()
    {
        if (string.IsNullOrEmpty(_currentVillagerId)) return;

        var vs = GetNodeOrNull(VILLAGE_SYSTEM_PATH);
        if (vs == null) return;

        if (Multiplayer.IsServer())
            vs.Call("RequestRecruit", _currentVillagerId);
        else
            vs.RpcId(1, "RequestRecruit", _currentVillagerId);

        Close();
    }

    private void OnLeavePressed()
    {
        if (string.IsNullOrEmpty(_currentVillagerId)) return;

        var vs = GetNodeOrNull(VILLAGE_SYSTEM_PATH);
        if (vs == null) return;

        if (Multiplayer.IsServer())
            vs.Call("RequestLeave", _currentVillagerId);
        else
            vs.RpcId(1, "RequestLeave", _currentVillagerId);

        Close();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void RefreshButtonState()
    {
        if (!IsInsideTree()) return;
        // Recruit is available unless this NPC is already following someone.
        bool isFollowing    = LocalState.FollowerNpcId == _currentVillagerId
                              && !string.IsNullOrEmpty(_currentVillagerId);
        bool hasAnyFollower = !string.IsNullOrEmpty(LocalState.FollowerNpcId);

        _recruitBtn.Disabled = hasAnyFollower; // can only lead one NPC at a time
        // Leave stops the follow (NPC stays in settlement). Only available if this NPC is following us.
        _leaveBtn.Disabled   = !isFollowing;
    }

    private static Label AddStatRow(VBoxContainer parent, string statName)
    {
        var row = new HBoxContainer();
        parent.AddChild(row);

        var nameLabel = new Label
        {
            Text               = statName,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        row.AddChild(nameLabel);

        var valueLabel = new Label { HorizontalAlignment = HorizontalAlignment.Right };
        row.AddChild(valueLabel);

        return valueLabel;
    }

    private static string FormatStat(int value, bool isHighest) =>
        isHighest ? $"{value} ★" : value.ToString();
}
