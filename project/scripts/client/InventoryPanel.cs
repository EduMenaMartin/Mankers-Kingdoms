using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Client;

/// <summary>
/// Inventory panel — toggled open/closed with the "open_inventory" action (I key).
/// Escape also closes it when open.
///
/// Shows a centred modal panel listing every item the local player carries with its count.
/// Refreshes immediately when the server pushes an inventory update (LocalState.InventoryChanged).
///
/// Item display names resolved via Loc.T(itemId + ".name"); all current item IDs have matching
/// entries in en.json so no fallback [key] strings should appear in normal play.
///
/// Layer 25 — above BuildMenu (20), below WorldMapScreen (31).
/// Editor task: add a CanvasLayer node named InventoryPanel with script client/InventoryPanel.cs
/// to GameWorld.tscn.
/// </summary>
public partial class InventoryPanel : CanvasLayer
{
    private const float PANEL_W = 380f;
    private const float PANEL_H = 480f;

    private VBoxContainer _itemList = null!;
    private Label         _emptyLabel = null!;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        Layer   = 25;
        Visible = false;

        BuildUI();

        LocalState.InventoryChanged += OnInventoryChanged;
    }

    public override void _ExitTree()
    {
        LocalState.InventoryChanged -= OnInventoryChanged;
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("open_inventory"))
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

    // ── Build UI ──────────────────────────────────────────────────────────────

    private void BuildUI()
    {
        // Semi-transparent full-screen backdrop — click-through so game still
        // receives input for moving etc. (MouseFilter.Ignore).
        var backdrop = new ColorRect
        {
            Color       = new Color(0f, 0f, 0f, 0.55f),
            AnchorRight  = 1f,
            AnchorBottom = 1f,
            MouseFilter  = Control.MouseFilterEnum.Ignore
        };
        AddChild(backdrop);

        // ── Centred panel ─────────────────────────────────────────────────────
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

        var outerVBox = new VBoxContainer();
        outerVBox.AddThemeConstantOverride("separation", 8);
        panel.AddChild(outerVBox);

        // ── Title ─────────────────────────────────────────────────────────────
        var title = new Label
        {
            Text                = Loc.T("inventory.title"),
            HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize   = new Vector2(0f, 28f)
        };
        title.AddThemeFontSizeOverride("font_size", 18);
        outerVBox.AddChild(title);

        // Divider
        var sep = new HSeparator();
        outerVBox.AddChild(sep);

        // ── Scroll area ───────────────────────────────────────────────────────
        var scroll = new ScrollContainer
        {
            CustomMinimumSize   = new Vector2(0f, PANEL_H - 110f),
            SizeFlagsVertical   = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        outerVBox.AddChild(scroll);

        _itemList = new VBoxContainer();
        _itemList.AddThemeConstantOverride("separation", 4);
        _itemList.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(_itemList);

        // Empty-inventory placeholder shown when there are no items.
        _emptyLabel = new Label
        {
            Text                = Loc.T("inventory.empty"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Visible             = false
        };
        _itemList.AddChild(_emptyLabel);

        // ── Footer hint ───────────────────────────────────────────────────────
        var sep2 = new HSeparator();
        outerVBox.AddChild(sep2);

        var hint = new Label
        {
            Text                = Loc.T("inventory.close_hint"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate            = new Color(0.7f, 0.7f, 0.7f)
        };
        hint.AddThemeFontSizeOverride("font_size", 12);
        outerVBox.AddChild(hint);
    }

    // ── Refresh ───────────────────────────────────────────────────────────────

    private void Toggle()
    {
        Visible = !Visible;
        if (Visible) Refresh();
    }

    private void OnInventoryChanged()
    {
        if (Visible) Refresh();
    }

    private void Refresh()
    {
        // Remove all item rows (keep _emptyLabel at index 0).
        for (int i = _itemList.GetChildCount() - 1; i >= 1; i--)
            _itemList.GetChild(i).QueueFree();

        var items = LocalState.Inventory.Items;

        _emptyLabel.Visible = items.Count == 0;

        foreach (var (itemId, count) in items)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);

            var nameLabel = new Label
            {
                Text               = ItemDisplayName(itemId),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            row.AddChild(nameLabel);

            var countLabel = new Label
            {
                Text                = $"×{count}",
                HorizontalAlignment = HorizontalAlignment.Right,
                CustomMinimumSize   = new Vector2(50f, 0f)
            };
            row.AddChild(countLabel);

            _itemList.AddChild(row);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves a human-readable name for any item ID by appending ".name" and looking up
    /// the loc key. All item IDs in the current game have matching en.json entries.
    /// </summary>
    private static string ItemDisplayName(string itemId)
    {
        string locKey = itemId + ".name";
        string name   = Loc.T(locKey);
        // If Loc returns the fallback "[key]", show the raw ID instead — at least it's legible.
        return name.StartsWith('[') ? itemId : name;
    }
}
