using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Client;

/// <summary>
/// Settlement stockpile panel — shows items accumulated by NPC workers and lets the
/// founder take them all into personal inventory.
///
/// Opened by PlayerController when E is pressed within 3 m of the player's Kingdom Marker.
/// Refreshes automatically when LocalState.StockpileChanged fires.
/// "Take All" sends RequestTakeFromStockpile to SettlementSystem.
///
/// Layer 29. Editor task: add as CanvasLayer child of GameWorld root, attach this script.
/// </summary>
public partial class StockpilePanel : CanvasLayer
{
    private const string SETTLEMENT_SYSTEM_PATH = "/root/GameWorld/SettlementSystem";

    private VBoxContainer _itemList = null!;
    private Label         _emptyLabel = null!;
    private Button        _takeAllBtn = null!;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        Layer   = 29;
        Visible = false;

        // ── Root panel ───────────────────────────────────────────────────────
        var panel = new PanelContainer();
        panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        panel.CustomMinimumSize = new Vector2(300f, 0f);
        AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_top",    12);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        margin.AddThemeConstantOverride("margin_left",   16);
        margin.AddThemeConstantOverride("margin_right",  16);
        panel.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);

        // Title
        var title = new Label
        {
            Text                = Loc.T("stockpile.title"),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 20);
        root.AddChild(title);

        root.AddChild(new HSeparator());

        // Scrollable item list
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0f, 120f) };
        root.AddChild(scroll);

        _itemList = new VBoxContainer();
        _itemList.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(_itemList);

        _emptyLabel = new Label
        {
            Text                = Loc.T("stockpile.empty"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate            = new Color(0.6f, 0.6f, 0.6f),
        };
        _itemList.AddChild(_emptyLabel);

        root.AddChild(new HSeparator());

        // Buttons
        var btnRow = new HBoxContainer();
        btnRow.AddThemeConstantOverride("separation", 8);
        root.AddChild(btnRow);

        _takeAllBtn = new Button
        {
            Text                = Loc.T("stockpile.take_all"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _takeAllBtn.Pressed += OnTakeAllPressed;
        btnRow.AddChild(_takeAllBtn);

        var closeBtn = new Button
        {
            Text                = Loc.T("inventory.close_hint").Split(' ')[1], // "Escape"
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
        };
        closeBtn.Pressed += Close;
        btnRow.AddChild(closeBtn);

        // Hint
        var hint = new Label
        {
            Text                = Loc.T("stockpile.close_hint"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate            = new Color(0.6f, 0.6f, 0.6f),
        };
        hint.AddThemeFontSizeOverride("font_size", 12);
        root.AddChild(hint);

        LocalState.StockpileChanged += RefreshList;
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

    public void Open()
    {
        RefreshList();
        Visible = true;
    }

    public void Close() => Visible = false;

    // ── Internal ──────────────────────────────────────────────────────────────

    private void RefreshList()
    {
        if (!IsInsideTree()) return;

        // Clear previous rows (keep _emptyLabel in the pool by removing others)
        foreach (Node child in _itemList.GetChildren())
        {
            if (child != _emptyLabel)
                child.QueueFree();
        }

        var snapshot = LocalState.StockpileSnapshot;
        bool isEmpty = true;

        foreach (var (itemId, count) in snapshot)
        {
            if (count <= 0) continue;
            isEmpty = false;

            // Resolve display name: "resource.wood" → Loc "item.wood.name" (same stem logic)
            string stem = itemId.Contains('.') ? itemId[(itemId.LastIndexOf('.') + 1)..] : itemId;
            string name = Loc.T($"item.{stem}.name");

            var row = new HBoxContainer();
            var nameLabel  = new Label { Text = name,              SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var countLabel = new Label { Text = count.ToString(),  HorizontalAlignment = HorizontalAlignment.Right };
            row.AddChild(nameLabel);
            row.AddChild(countLabel);
            _itemList.AddChild(row);
        }

        _emptyLabel.Visible  = isEmpty;
        _takeAllBtn.Disabled = isEmpty;
    }

    private void OnTakeAllPressed()
    {
        var ss = GetNodeOrNull(SETTLEMENT_SYSTEM_PATH);
        if (ss == null) return;

        if (Multiplayer.IsServer())
            ss.Call("RequestTakeFromStockpile");
        else
            ss.RpcId(1, "RequestTakeFromStockpile");

        Close();
    }
}
