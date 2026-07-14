using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Client;

/// <summary>
/// Character sheet panel — toggled open/closed with the "char_sheet" action (K key).
/// Escape also closes it when open; if the equip picker is open, Escape closes the
/// picker first before closing the sheet itself.
///
/// Displays the local player's:
///   • Race and class
///   • Effective stats (Str / Dex / Con / Wis after race modifiers)
///   • Equipment slots (Main Hand / Off Hand / Body Armor) with inline item picker
///   • Six skill levels with their stat-derived caps
///
/// All data is read client-side:
///   • Race/class: GameSession.ChosenRaceId / ChosenClassId
///   • Effective stats: GameSession.RolledStats + race.Apply()
///   • Equipment: LocalState.GetEquipped(slot) (updated by InventorySystem RPC)
///   • Skill levels: LocalState.SkillLevels (updated by SkillSystem RPC)
///   • Skill caps: SkillData.GetCap(effectiveStats)
///
/// Refreshes on LocalState.SkillLevelChanged so level-up is reflected immediately.
/// Refreshes equipment slot labels on LocalState.EquippedSlotChanged.
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
    private const float PANEL_H = 620f;

    private const string INVENTORY_SYSTEM_PATH = "/root/GameWorld/InventorySystem";

    // ── Palette ───────────────────────────────────────────────────────────────
    // All colour constants in one place so a theme change is one edit.

    private static readonly Color COL_BG          = new(0.11f, 0.09f, 0.07f, 0.97f); // near-black warm
    private static readonly Color COL_BORDER       = new(0.55f, 0.42f, 0.18f, 1.00f); // antique gold
    private static readonly Color COL_BORDER_DIM   = new(0.30f, 0.23f, 0.10f, 1.00f); // muted gold
    private static readonly Color COL_SLOT_BG      = new(0.07f, 0.06f, 0.05f, 1.00f); // dark inset
    private static readonly Color COL_SLOT_HOVER   = new(0.18f, 0.15f, 0.10f, 1.00f); // warm highlight
    private static readonly Color COL_SLOT_PRESSED = new(0.25f, 0.20f, 0.12f, 1.00f); // pressed amber
    private static readonly Color COL_HEADER       = new(0.85f, 0.72f, 0.35f, 1.00f); // gold text
    private static readonly Color COL_SUBTEXT      = new(0.60f, 0.58f, 0.54f, 1.00f); // muted label
    private static readonly Color COL_HINT         = new(0.45f, 0.43f, 0.40f, 1.00f); // footer hint

    // Skill level colours (cap indicator).
    private static readonly Color COL_CAP_OK   = new(0.45f, 0.72f, 0.45f, 1.00f); // green — room to grow
    private static readonly Color COL_CAP_FULL = new(0.90f, 0.50f, 0.28f, 1.00f); // orange — at cap

    // ── Live refs ─────────────────────────────────────────────────────────────

    private Label[] _skillLevelLabels = System.Array.Empty<Label>();
    private Label[] _skillCapLabels   = System.Array.Empty<Label>();
    private Label   _statLine1        = null!;
    private Label   _statLine2        = null!;
    private Label   _raceClassLabel   = null!;
    private Button[] _slotButtons     = System.Array.Empty<Button>();

    // Equipment item picker — built once in _Ready(), persists across opens.
    private EquipSlot      _pendingSlot;
    private PanelContainer _equipPicker = null!;
    private VBoxContainer  _pickerList  = null!;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        Layer   = 26;
        Visible = false;

        BuildPicker();

        LocalState.SkillLevelChanged   += OnSkillLevelChanged;
        LocalState.EquippedSlotChanged += OnEquippedSlotChanged;
    }

    public override void _ExitTree()
    {
        LocalState.SkillLevelChanged   -= OnSkillLevelChanged;
        LocalState.EquippedSlotChanged -= OnEquippedSlotChanged;
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("char_sheet"))
        {
            if (_equipPicker.Visible)
            {
                _equipPicker.Visible = false;
                GetViewport().SetInputAsHandled();
                return;
            }
            Toggle();
            GetViewport().SetInputAsHandled();
        }
        else if (e.IsActionPressed("ui_cancel"))
        {
            if (_equipPicker.Visible)
            {
                _equipPicker.Visible = false;
                GetViewport().SetInputAsHandled();
                return;
            }
            if (Visible)
            {
                Visible = false;
                GetViewport().SetInputAsHandled();
            }
        }
    }

    // ── Show / hide ───────────────────────────────────────────────────────────

    private void Toggle()
    {
        if (Visible) { Visible = false; return; }
        RebuildUI();
        Visible = true;
    }

    private void OnSkillLevelChanged(string skillId, int newLevel)
    {
        if (!Visible) return;
        RefreshSkillLabels();
    }

    private void OnEquippedSlotChanged(EquipSlot slot, string? itemId)
    {
        if (!Visible) return;
        if ((int)slot < _slotButtons.Length)
            _slotButtons[(int)slot].Text = GetSlotButtonText(slot);
    }

    // ── UI construction ───────────────────────────────────────────────────────

    private void RebuildUI()
    {
        // Clear previously-built children, preserving the persistent picker.
        foreach (var child in GetChildren())
        {
            if (child == _equipPicker) continue;
            child.QueueFree();
        }

        _skillLevelLabels = new Label[SkillRegistry.All.Count];
        _skillCapLabels   = new Label[SkillRegistry.All.Count];
        _slotButtons      = new Button[System.Enum.GetValues<EquipSlot>().Length];

        // Dim backdrop.
        var backdrop = new ColorRect
        {
            Color        = new Color(0f, 0f, 0f, 0.60f),
            AnchorRight  = 1f,
            AnchorBottom = 1f,
            MouseFilter  = Control.MouseFilterEnum.Ignore
        };
        AddChild(backdrop);

        // Main panel — dark parchment with gold border.
        var panel = new PanelContainer
        {
            AnchorLeft   = 0.5f, AnchorRight  = 0.5f,
            AnchorTop    = 0.5f, AnchorBottom = 0.5f,
            OffsetLeft   = -(PANEL_W * 0.5f),
            OffsetRight  =  (PANEL_W * 0.5f),
            OffsetTop    = -(PANEL_H * 0.5f),
            OffsetBottom =  (PANEL_H * 0.5f)
        };
        panel.AddThemeStyleboxOverride("panel", MakePanel(COL_BG, COL_BORDER, radius: 6, borderW: 2,
            padH: 14f, padV: 10f));
        AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 5);
        panel.AddChild(vbox);

        // ── Title ──
        var title = new Label
        {
            Text                = Loc.T("charSheet.title"),
            HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize   = new Vector2(0f, 30f),
            Modulate            = COL_HEADER
        };
        title.AddThemeFontSizeOverride("font_size", 20);
        vbox.AddChild(title);

        vbox.AddChild(MakeSeparator());

        // ── Race / Class ──
        _raceClassLabel = new Label
        {
            Text                = BuildRaceClassLine(),
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate            = COL_SUBTEXT
        };
        vbox.AddChild(_raceClassLabel);

        vbox.AddChild(MakeSeparator());

        // ── Stats ──
        vbox.AddChild(MakeSectionHeader("charSheet.stats"));

        var stats = GetEffectiveStats();
        _statLine1 = new Label { Text = FormatStatLine1(stats) };
        _statLine2 = new Label { Text = FormatStatLine2(stats) };
        vbox.AddChild(_statLine1);
        vbox.AddChild(_statLine2);

        vbox.AddChild(MakeSeparator());

        // ── Equipment ──
        vbox.AddChild(MakeSectionHeader("charSheet.equipment"));

        foreach (EquipSlot slot in System.Enum.GetValues<EquipSlot>())
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);

            var slotLabel = new Label
            {
                Text                = Loc.T($"charSheet.slot.{SlotLocKey(slot)}"),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                Modulate            = COL_SUBTEXT
            };

            var btn = new Button
            {
                Text              = GetSlotButtonText(slot),
                CustomMinimumSize = new Vector2(200f, 28f)
            };
            StyleSlotButton(btn);

            var capturedSlot = slot;
            btn.Pressed += () => OpenPicker(capturedSlot);

            _slotButtons[(int)slot] = btn;
            row.AddChild(slotLabel);
            row.AddChild(btn);
            vbox.AddChild(row);
        }

        vbox.AddChild(MakeSeparator());

        // ── Skills ──
        vbox.AddChild(MakeSectionHeader("charSheet.skills"));

        // Column header row.
        var colHeader = new HBoxContainer();
        colHeader.AddThemeConstantOverride("separation", 4);
        colHeader.AddChild(MakeColLabel("charSheet.col.skill",  Control.SizeFlags.ExpandFill));
        colHeader.AddChild(MakeColLabel("charSheet.col.level",  Control.SizeFlags.ShrinkCenter, minW: 55f));
        colHeader.AddChild(MakeColLabel("charSheet.col.cap",    Control.SizeFlags.ShrinkCenter, minW: 55f));
        vbox.AddChild(colHeader);

        for (int i = 0; i < SkillRegistry.All.Count; i++)
        {
            var skill = SkillRegistry.All[i];
            var row   = new HBoxContainer();
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
                HorizontalAlignment = HorizontalAlignment.Center
            };

            _skillLevelLabels[i] = levelLabel;
            _skillCapLabels[i]   = capLabel;

            row.AddChild(nameLabel);
            row.AddChild(levelLabel);
            row.AddChild(capLabel);
            vbox.AddChild(row);
        }

        RefreshSkillLabels();

        vbox.AddChild(MakeSeparator());

        // ── Footer ──
        var hint = new Label
        {
            Text                = Loc.T("charSheet.close_hint"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate            = COL_HINT
        };
        hint.AddThemeFontSizeOverride("font_size", 11);
        vbox.AddChild(hint);
    }

    /// <summary>
    /// Builds the floating equip-item picker once in _Ready(). Persists across panel opens
    /// so we only pay construction cost once; its contents are repopulated in OpenPicker().
    /// </summary>
    private void BuildPicker()
    {
        _equipPicker = new PanelContainer
        {
            AnchorLeft   = 0.5f, AnchorRight  = 0.5f,
            AnchorTop    = 0.5f, AnchorBottom = 0.5f,
            OffsetLeft   = -140f, OffsetRight  = 140f,
            OffsetTop    = -170f, OffsetBottom = 170f,
            Visible      = false
        };
        _equipPicker.AddThemeStyleboxOverride("panel",
            MakePanel(new Color(0.09f, 0.07f, 0.05f, 0.99f), COL_BORDER, radius: 4, borderW: 2,
                padH: 10f, padV: 8f));
        AddChild(_equipPicker);

        _pickerList = new VBoxContainer();
        _pickerList.AddThemeConstantOverride("separation", 5);
        _equipPicker.AddChild(_pickerList);
    }

    /// <summary>
    /// Populates and shows the item picker for the given equip slot.
    /// Lists all compatible items currently in the player's inventory, plus an Unequip option.
    /// </summary>
    private void OpenPicker(EquipSlot slot)
    {
        _pendingSlot = slot;

        foreach (var child in _pickerList.GetChildren())
            child.QueueFree();

        bool any = false;
        foreach (var (itemId, _) in LocalState.Inventory.Items)
        {
            if (!IsCompatibleWithSlot(slot, itemId)) continue;
            any = true;

            var btn = new Button { Text = GetItemDisplayName(itemId) };
            StylePickerButton(btn, primary: true);
            string captured = itemId;
            btn.Pressed += () => OnPickerItemSelected(captured);
            _pickerList.AddChild(btn);
        }

        if (!any)
        {
            _pickerList.AddChild(new Label
            {
                Text     = Loc.T("charSheet.slot.no_compatible"),
                Modulate = COL_SUBTEXT
            });
        }

        var unequipBtn = new Button { Text = Loc.T("charSheet.slot.unequip") };
        StylePickerButton(unequipBtn, primary: false);
        unequipBtn.Pressed += () => OnPickerItemSelected(null);
        _pickerList.AddChild(unequipBtn);

        _equipPicker.Visible = true;
    }

    private void OnPickerItemSelected(string? itemId)
    {
        _equipPicker.Visible = false;
        GetNodeOrNull<Node>(INVENTORY_SYSTEM_PATH)
            ?.Rpc("RequestEquipItem", (int)_pendingSlot, itemId ?? "");
    }

    // ── Refresh ───────────────────────────────────────────────────────────────

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
                _skillCapLabels[i].Text     = cap.ToString();
                _skillCapLabels[i].Modulate = level >= cap ? COL_CAP_FULL : COL_CAP_OK;
            }
            else
            {
                _skillCapLabels[i].Text    = "—";
                _skillCapLabels[i].Modulate = COL_SUBTEXT;
            }
        }
    }

    // ── StyleBox helpers ──────────────────────────────────────────────────────

    /// <summary>Creates a filled panel StyleBoxFlat with uniform rounded corners and padding.</summary>
    private static StyleBoxFlat MakePanel(Color bg, Color border,
        int radius = 4, int borderW = 1, float padH = 8f, float padV = 6f)
    {
        var s = new StyleBoxFlat
        {
            BgColor                   = bg,
            BorderColor               = border,
            BorderWidthTop            = borderW,
            BorderWidthBottom         = borderW,
            BorderWidthLeft           = borderW,
            BorderWidthRight          = borderW,
            CornerRadiusTopLeft       = radius,
            CornerRadiusTopRight      = radius,
            CornerRadiusBottomLeft    = radius,
            CornerRadiusBottomRight   = radius,
            ContentMarginLeft         = padH,
            ContentMarginRight        = padH,
            ContentMarginTop          = padV,
            ContentMarginBottom       = padV
        };
        return s;
    }

    /// <summary>Creates a StyleBoxFlat with no border — used for button hover/pressed states.</summary>
    private static StyleBoxFlat MakeFill(Color bg, int radius = 3)
    {
        return new StyleBoxFlat
        {
            BgColor                 = bg,
            CornerRadiusTopLeft     = radius,
            CornerRadiusTopRight    = radius,
            CornerRadiusBottomLeft  = radius,
            CornerRadiusBottomRight = radius,
            ContentMarginLeft       = 6f,
            ContentMarginRight      = 6f,
            ContentMarginTop        = 3f,
            ContentMarginBottom     = 3f
        };
    }

    /// <summary>
    /// Applies the dark-inset slot button style (normal/hover/pressed/focus states).
    /// Used for the three equipment slot buttons in the main panel.
    /// </summary>
    private static void StyleSlotButton(Button btn)
    {
        btn.AddThemeStyleboxOverride("normal",  MakePanel(COL_SLOT_BG,    COL_BORDER_DIM, radius: 3, borderW: 1, padH: 6f, padV: 2f));
        btn.AddThemeStyleboxOverride("hover",   MakePanel(COL_SLOT_HOVER,  COL_BORDER,    radius: 3, borderW: 1, padH: 6f, padV: 2f));
        btn.AddThemeStyleboxOverride("pressed", MakePanel(COL_SLOT_PRESSED, COL_BORDER,   radius: 3, borderW: 1, padH: 6f, padV: 2f));
        btn.AddThemeStyleboxOverride("focus",   MakePanel(COL_SLOT_BG,    COL_BORDER,    radius: 3, borderW: 1, padH: 6f, padV: 2f));
    }

    /// <summary>
    /// Applies button style for the equip picker list.
    /// Primary = item selection (gold tint on hover); non-primary = Unequip (muted).
    /// </summary>
    private static void StylePickerButton(Button btn, bool primary)
    {
        var hoverCol   = primary ? COL_SLOT_HOVER   : new Color(0.15f, 0.08f, 0.08f, 1f);
        var borderCol  = primary ? COL_BORDER        : new Color(0.45f, 0.20f, 0.20f, 1f);
        btn.AddThemeStyleboxOverride("normal",  MakeFill(COL_SLOT_BG, radius: 3));
        btn.AddThemeStyleboxOverride("hover",   MakePanel(hoverCol,  borderCol, radius: 3, borderW: 1, padH: 6f, padV: 2f));
        btn.AddThemeStyleboxOverride("pressed", MakeFill(COL_SLOT_PRESSED, radius: 3));
        btn.AddThemeStyleboxOverride("focus",   MakeFill(COL_SLOT_BG, radius: 3));
        if (!primary) btn.Modulate = new Color(0.75f, 0.55f, 0.55f, 1f);
    }

    // ── Layout helpers ────────────────────────────────────────────────────────

    private static Label MakeSectionHeader(string locKey)
    {
        var lbl = new Label
        {
            Text     = Loc.T(locKey),
            Modulate = COL_HEADER
        };
        lbl.AddThemeFontSizeOverride("font_size", 13);
        return lbl;
    }

    private static Label MakeColLabel(string locKey, Control.SizeFlags flags, float minW = 0f)
    {
        return new Label
        {
            Text                = Loc.T(locKey),
            SizeFlagsHorizontal = flags,
            CustomMinimumSize   = new Vector2(minW, 0f),
            HorizontalAlignment = minW > 0f ? HorizontalAlignment.Center : HorizontalAlignment.Left,
            Modulate            = COL_SUBTEXT
        };
    }

    private static HSeparator MakeSeparator()
    {
        var sep = new HSeparator();
        // Tint the separator to match the gold palette.
        sep.Modulate = new Color(0.40f, 0.30f, 0.12f, 0.80f);
        return sep;
    }

    // ── Data helpers ──────────────────────────────────────────────────────────

    private static StatBlock? GetEffectiveStats()
    {
        if (GameSession.RolledStats == null) return null;
        var race = RaceRegistry.Find(GameSession.ChosenRaceId);
        return race?.Apply(GameSession.RolledStats, GameSession.HumanChosenStat)
               ?? GameSession.RolledStats;
    }

    private static string BuildRaceClassLine()
    {
        var race      = RaceRegistry.Find(GameSession.ChosenRaceId);
        var kit       = ClassKitRegistry.Find(GameSession.ChosenClassId);
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

    private static string GetSlotButtonText(EquipSlot slot)
    {
        string? itemId = LocalState.GetEquipped(slot);
        return itemId != null ? GetItemDisplayName(itemId) : Loc.T("charSheet.slot.empty");
    }

    private static string GetItemDisplayName(string itemId)
    {
        var weapon = WeaponRegistry.Find(itemId);
        if (weapon != null) return Loc.T(weapon.DisplayNameKey);
        var armor = ArmorRegistry.Find(itemId);
        if (armor != null) return Loc.T(armor.DisplayNameKey);
        return itemId;
    }

    private static bool IsCompatibleWithSlot(EquipSlot slot, string itemId) => slot switch
    {
        EquipSlot.MainHand  => WeaponRegistry.Find(itemId) != null,
        EquipSlot.OffHand   => itemId == "item.armor.shield" || WeaponRegistry.Find(itemId) != null,
        EquipSlot.BodyArmor => ArmorRegistry.Find(itemId) != null && itemId != "item.armor.shield",
        _                   => false
    };

    private static string SlotLocKey(EquipSlot slot) => slot switch
    {
        EquipSlot.MainHand  => "mainhand",
        EquipSlot.OffHand   => "offhand",
        EquipSlot.BodyArmor => "bodyarmor",
        _                   => slot.ToString().ToLowerInvariant()
    };
}
