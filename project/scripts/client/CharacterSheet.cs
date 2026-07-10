using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Client;

/// <summary>
/// Character sheet panel — toggled open/closed with the "char_sheet" action (K key).
/// Escape also closes it when open.
///
/// Displays the local player's:
///   • Race and class
///   • Effective stats (Str / Dex / Con / Wis after race modifiers)
///   • Six skill levels with their stat-derived caps
///
/// All data is read client-side:
///   • Race/class: GameSession.ChosenRaceId / ChosenClassId
///   • Effective stats: GameSession.RolledStats + race.Apply()
///   • Skill levels: LocalState.SkillLevels (updated by SkillSystem RPC)
///   • Skill caps: SkillData.GetCap(effectiveStats)
///
/// Refreshes on LocalState.SkillLevelChanged so level-up is reflected immediately.
/// If the player entered the world without completing CharacterCreateScreen
/// (GameSession.RolledStats == null), stats show "—" and caps are hidden.
///
/// Layer 26 — directly above InventoryPanel (25).
/// Editor task: add a CanvasLayer node named CharacterSheet with script
/// client/CharacterSheet.cs to GameWorld.tscn.
/// </summary>
public partial class CharacterSheet : CanvasLayer
{
    private const float PANEL_W = 460f;
    private const float PANEL_H = 500f;

    // Skill section rows — rebuilt on open, updated in-place on skill level change.
    private Label[] _skillLevelLabels = System.Array.Empty<Label>();
    private Label[] _skillCapLabels   = System.Array.Empty<Label>();

    // Stat labels — rebuilt on open.
    private Label _statLine1 = null!;
    private Label _statLine2 = null!;

    // Race/class line.
    private Label _raceClassLabel = null!;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        Layer   = 26;
        Visible = false;

        LocalState.SkillLevelChanged += OnSkillLevelChanged;
    }

    public override void _ExitTree()
    {
        LocalState.SkillLevelChanged -= OnSkillLevelChanged;
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("char_sheet"))
        {
            Toggle();
            GetViewport().SetInputAsHandled();
        }
        else if (Visible && e.IsActionPressed("ui_cancel"))
        {
            Visible = false;
            GetViewport().SetInputAsHandled();
        }
    }

    // ── Show / hide ───────────────────────────────────────────────────────────

    private void Toggle()
    {
        if (Visible)
        {
            Visible = false;
            return;
        }

        // Rebuild from scratch each time so GameSession changes (race reselect) are always fresh.
        RebuildUI();
        Visible = true;
    }

    private void OnSkillLevelChanged(string skillId, int newLevel)
    {
        if (!Visible) return;
        RefreshSkillLabels();
    }

    // ── UI construction ───────────────────────────────────────────────────────

    private void RebuildUI()
    {
        // Clear any previously-built tree.
        foreach (var child in GetChildren())
            child.QueueFree();

        _skillLevelLabels = new Label[SkillRegistry.All.Count];
        _skillCapLabels   = new Label[SkillRegistry.All.Count];

        // Semi-transparent backdrop.
        var backdrop = new ColorRect
        {
            Color        = new Color(0f, 0f, 0f, 0.55f),
            AnchorRight  = 1f,
            AnchorBottom = 1f,
            MouseFilter  = Control.MouseFilterEnum.Ignore
        };
        AddChild(backdrop);

        // Centred panel.
        var panel = new PanelContainer
        {
            AnchorLeft   = 0.5f, AnchorRight  = 0.5f,
            AnchorTop    = 0.5f, AnchorBottom = 0.5f,
            OffsetLeft   = -(PANEL_W * 0.5f),
            OffsetRight  =  (PANEL_W * 0.5f),
            OffsetTop    = -(PANEL_H * 0.5f),
            OffsetBottom =  (PANEL_H * 0.5f)
        };
        AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 6);
        panel.AddChild(vbox);

        // Title.
        var title = new Label
        {
            Text                = Loc.T("charSheet.title"),
            HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize   = new Vector2(0f, 28f)
        };
        title.AddThemeFontSizeOverride("font_size", 18);
        vbox.AddChild(title);

        vbox.AddChild(new HSeparator());

        // Race / Class.
        _raceClassLabel = new Label
        {
            Text                = BuildRaceClassLine(),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        vbox.AddChild(_raceClassLabel);

        vbox.AddChild(new HSeparator());

        // Stats section.
        var statsHeader = new Label
        {
            Text = Loc.T("charSheet.stats"),
            Modulate = new Color(0.85f, 0.85f, 0.55f)
        };
        statsHeader.AddThemeFontSizeOverride("font_size", 14);
        vbox.AddChild(statsHeader);

        var stats = GetEffectiveStats();

        _statLine1 = new Label { Text = FormatStatLine1(stats) };
        _statLine2 = new Label { Text = FormatStatLine2(stats) };
        vbox.AddChild(_statLine1);
        vbox.AddChild(_statLine2);

        vbox.AddChild(new HSeparator());

        // Skills section header row.
        var skillsHeader = new Label
        {
            Text = Loc.T("charSheet.skills"),
            Modulate = new Color(0.85f, 0.85f, 0.55f)
        };
        skillsHeader.AddThemeFontSizeOverride("font_size", 14);
        vbox.AddChild(skillsHeader);

        // Column header.
        var colHeader = new HBoxContainer();
        colHeader.AddThemeConstantOverride("separation", 4);
        var colSkill = new Label
        {
            Text                = Loc.T("charSheet.col.skill"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Modulate            = new Color(0.65f, 0.65f, 0.65f)
        };
        var colLevel = new Label
        {
            Text              = Loc.T("charSheet.col.level"),
            CustomMinimumSize = new Vector2(55f, 0f),
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate          = new Color(0.65f, 0.65f, 0.65f)
        };
        var colCap = new Label
        {
            Text              = Loc.T("charSheet.col.cap"),
            CustomMinimumSize = new Vector2(55f, 0f),
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate          = new Color(0.65f, 0.65f, 0.65f)
        };
        colHeader.AddChild(colSkill);
        colHeader.AddChild(colLevel);
        colHeader.AddChild(colCap);
        vbox.AddChild(colHeader);

        // One row per skill.
        for (int i = 0; i < SkillRegistry.All.Count; i++)
        {
            var skill = SkillRegistry.All[i];

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 4);

            var nameLabel = new Label
            {
                Text                = Loc.T(skill.DisplayNameKey),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };

            var levelLabel = new Label
            {
                CustomMinimumSize   = new Vector2(55f, 0f),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var capLabel = new Label
            {
                CustomMinimumSize   = new Vector2(55f, 0f),
                HorizontalAlignment = HorizontalAlignment.Center,
                Modulate            = new Color(0.55f, 0.75f, 0.55f)
            };

            _skillLevelLabels[i] = levelLabel;
            _skillCapLabels[i]   = capLabel;

            row.AddChild(nameLabel);
            row.AddChild(levelLabel);
            row.AddChild(capLabel);
            vbox.AddChild(row);
        }

        RefreshSkillLabels();

        vbox.AddChild(new HSeparator());

        // Footer hint.
        var hint = new Label
        {
            Text                = Loc.T("charSheet.close_hint"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate            = new Color(0.7f, 0.7f, 0.7f)
        };
        hint.AddThemeFontSizeOverride("font_size", 12);
        vbox.AddChild(hint);
    }

    // ── Refresh helpers ───────────────────────────────────────────────────────

    private void RefreshSkillLabels()
    {
        var stats = GetEffectiveStats();

        for (int i = 0; i < SkillRegistry.All.Count; i++)
        {
            var skill = SkillRegistry.All[i];
            LocalState.SkillLevels.TryGetValue(skill.Id, out int level);

            _skillLevelLabels[i].Text = level.ToString();

            if (stats != null)
            {
                int cap = skill.GetCap(stats);
                _skillCapLabels[i].Text    = cap.ToString();
                // Dim the cap label when already at cap.
                _skillCapLabels[i].Modulate = level >= cap
                    ? new Color(0.9f, 0.5f, 0.3f)   // orange = capped
                    : new Color(0.55f, 0.75f, 0.55f); // green = room to grow
            }
            else
            {
                _skillCapLabels[i].Text = "—";
            }
        }
    }

    // ── Data helpers ──────────────────────────────────────────────────────────

    /// <summary>Returns the player's effective StatBlock (race modifiers applied), or null.</summary>
    private static StatBlock? GetEffectiveStats()
    {
        if (GameSession.RolledStats == null) return null;
        var race = RaceRegistry.Find(GameSession.ChosenRaceId);
        return race?.Apply(GameSession.RolledStats, GameSession.HumanChosenStat)
               ?? GameSession.RolledStats;
    }

    private static string BuildRaceClassLine()
    {
        var race  = RaceRegistry.Find(GameSession.ChosenRaceId);
        var kit   = ClassKitRegistry.Find(GameSession.ChosenClassId);
        string raceName  = race != null ? Loc.T(race.DisplayNameKey)  : GameSession.ChosenRaceId;
        string className = kit  != null ? Loc.T(kit.DisplayNameKey)   : GameSession.ChosenClassId;
        return $"{Loc.T("charSheet.race")}: {raceName}    {Loc.T("charSheet.class")}: {className}";
    }

    private static string FormatStatLine1(StatBlock? s)
    {
        string str = s != null ? s.Str.ToString() : "—";
        string dex = s != null ? s.Dex.ToString() : "—";
        return $"{Loc.T("charCreate.stat.str")}: {str,2}       {Loc.T("charCreate.stat.dex")}: {dex,2}";
    }

    private static string FormatStatLine2(StatBlock? s)
    {
        string con = s != null ? s.Con.ToString() : "—";
        string wis = s != null ? s.Wis.ToString() : "—";
        return $"{Loc.T("charCreate.stat.con")}: {con,2}       {Loc.T("charCreate.stat.wis")}: {wis,2}";
    }
}
