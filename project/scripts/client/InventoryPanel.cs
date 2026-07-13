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
/// Hotbar assignment: hover a row and press 1–9 to assign that item to the corresponding
/// hotbar slot. The HotbarKeyPressed event from LocalState bridges HotbarHUD's key handling
/// to this panel's hover state without a direct reference between the two.
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
    private const float PANEL_H = 520f;

    // Path to InventorySystem — untyped RPC to avoid client→server import.
    private const string INVENTORY_SYSTEM_PATH = "/root/GameWorld/InventorySystem";

    private VBoxContainer _itemList   = null!;
    private Label         _emptyLabel = null!;

    /// <summary>
    /// Maps each item row control to its itemId.
    /// Rebuilt on every Refresh(). Queried by GetHoveredItemId() on key press so that
    /// we don't rely on MouseEntered/MouseExited — those don't re-fire when rows are
    /// rebuilt after a hotbar slot change, causing stale hover state.
    /// </summary>
    private readonly System.Collections.Generic.Dictionary<HBoxContainer, string> _rowItems = new();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        Layer   = 25;
        Visible = false;

        BuildUI();

        LocalState.InventoryChanged  += OnInventoryChanged;
        LocalState.HotbarKeyPressed  += OnHotbarKeyPressed;
        LocalState.HotbarSlotChanged += OnHotbarSlotChanged;
    }

    public override void _ExitTree()
    {
        LocalState.InventoryChanged  -= OnInventoryChanged;
        LocalState.HotbarKeyPressed  -= OnHotbarKeyPressed;
        LocalState.HotbarSlotChanged -= OnHotbarSlotChanged;
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

        // ── Footer hints ──────────────────────────────────────────────────────
        var sep2 = new HSeparator();
        outerVBox.AddChild(sep2);

        var hotbarHint = new Label
        {
            Text                = Loc.T("inventory.hotbar_hint"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate            = new Color(0.6f, 0.85f, 1f)
        };
        hotbarHint.AddThemeFontSizeOverride("font_size", 11);
        outerVBox.AddChild(hotbarHint);

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

    private void OnHotbarSlotChanged(int _slot, string? _itemId)
    {
        // Refresh only the badge column — avoids rebuilding rows and losing mouse state.
        if (Visible) RefreshBadges();
    }

    private void Refresh()
    {
        // Remove all item rows (keep _emptyLabel at index 0).
        for (int i = _itemList.GetChildCount() - 1; i >= 1; i--)
            _itemList.GetChild(i).QueueFree();

        _rowItems.Clear();

        var items = LocalState.Inventory.Items;
        _emptyLabel.Visible = items.Count == 0;

        foreach (var (itemId, count) in items)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            // Pass-through so mouse position can be queried against GetGlobalRect() on key press.
            row.MouseFilter = Control.MouseFilterEnum.Pass;

            _rowItems[row] = itemId;

            var nameLabel = new Label
            {
                Text                = ItemDisplayName(itemId),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                MouseFilter         = Control.MouseFilterEnum.Ignore
            };
            row.AddChild(nameLabel);

            // Badge shows which hotbar slot (1-9) this item is assigned to, e.g. "[3]".
            var badgeLabel = new Label
            {
                Text                = HotbarBadge(itemId),
                HorizontalAlignment = HorizontalAlignment.Right,
                CustomMinimumSize   = new Vector2(28f, 0f),
                Modulate            = new Color(1f, 0.9f, 0.25f),
                MouseFilter         = Control.MouseFilterEnum.Ignore
            };
            badgeLabel.AddThemeFontSizeOverride("font_size", 11);
            row.AddChild(badgeLabel);

            var countLabel = new Label
            {
                Text                = $"×{count}",
                HorizontalAlignment = HorizontalAlignment.Right,
                CustomMinimumSize   = new Vector2(40f, 0f),
                MouseFilter         = Control.MouseFilterEnum.Ignore
            };
            row.AddChild(countLabel);

            _itemList.AddChild(row);
        }
    }

    /// <summary>
    /// Lightweight refresh that updates only badge labels without rebuilding rows.
    /// Called when a hotbar slot changes so we don't disturb mouse state.
    /// </summary>
    private void RefreshBadges()
    {
        foreach (var (row, itemId) in _rowItems)
        {
            // Badge label is child index 1 (nameLabel=0, badgeLabel=1, countLabel=2).
            if (row.GetChildCount() >= 2 && row.GetChild(1) is Label badge)
                badge.Text = HotbarBadge(itemId);
        }
    }

    // ── Hotbar assignment ─────────────────────────────────────────────────────

    /// <summary>
    /// Called by LocalState.HotbarKeyPressed (fired by HotbarHUD when 1–9 is pressed).
    /// Detects the hovered row at call time via rect-intersection so it stays correct after
    /// rows are rebuilt (MouseEntered events don't re-fire on rebuilt rows).
    /// </summary>
    private void OnHotbarKeyPressed(int slot)
    {
        if (!Visible) return;

        string? itemId = GetHoveredItemId();
        if (itemId == null) return;

        var invSystem = GetNodeOrNull(INVENTORY_SYSTEM_PATH);
        if (invSystem == null) return;

        if (Multiplayer.IsServer())
            invSystem.Call("RequestAssignHotbar", slot, itemId);
        else
            invSystem.RpcId(1, "RequestAssignHotbar", slot, itemId);
        // Badge refresh comes via LocalState.HotbarSlotChanged → RefreshBadges().
    }

    /// <summary>
    /// Returns the itemId of the row the mouse cursor is currently over,
    /// or null if no row is under the cursor.
    /// Uses global rect intersection instead of MouseEntered events so it works
    /// even after rows have been rebuilt (rebuilt nodes don't re-fire MouseEntered).
    /// </summary>
    private string? GetHoveredItemId()
    {
        var mousePos = GetViewport().GetMousePosition();
        foreach (var (row, itemId) in _rowItems)
        {
            if (!row.IsInsideTree()) continue;
            if (row.GetGlobalRect().HasPoint(mousePos))
                return itemId;
        }
        return null;
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

    /// <summary>
    /// Returns a badge string like "[3]" if the item is assigned to any hotbar slot,
    /// or "" if not assigned. Used to show hotbar assignment inline on inventory rows.
    /// </summary>
    private static string HotbarBadge(string itemId)
    {
        for (int i = 0; i < 9; i++)
            if (LocalState.GetHotbarSlot(i) == itemId)
                return $"[{i + 1}]";
        return "";
    }
}
