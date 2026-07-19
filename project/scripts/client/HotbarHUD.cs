using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Client;

/// <summary>
/// Always-visible hotbar — 9 slots anchored to the bottom-centre of the screen.
/// Slot contents are assigned from the InventoryPanel (hover an item + press 1–9).
/// Number keys 1–9 switch the active slot; the active slot is highlighted yellow.
///
/// Layer 5 — below all modal panels, always visible.
///
/// Editor task: add a CanvasLayer node named HotbarHUD with script client/HotbarHUD.cs
/// to GameWorld.tscn.
/// </summary>
public partial class HotbarHUD : CanvasLayer
{
    private const int   SLOT_COUNT = 9;
    private const float SLOT_SIZE  = 64f;
    private const float SLOT_GAP   = 4f;
    private const float MARGIN_BOTTOM = 12f;

    private readonly PanelContainer[] _slotPanels = new PanelContainer[SLOT_COUNT];
    private readonly Label[]          _itemLabels  = new Label[SLOT_COUNT];

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        Layer = 5;
        BuildUI();

        LocalState.HotbarSlotChanged      += OnHotbarSlotChanged;
        LocalState.ActiveHotbarSlotChanged += OnActiveSlotChanged;
    }

    public override void _ExitTree()
    {
        LocalState.HotbarSlotChanged      -= OnHotbarSlotChanged;
        LocalState.ActiveHotbarSlotChanged -= OnActiveSlotChanged;
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo) return;

        int slot = keyEvent.Keycode switch
        {
            Key.Key1 => 0, Key.Key2 => 1, Key.Key3 => 2,
            Key.Key4 => 3, Key.Key5 => 4, Key.Key6 => 5,
            Key.Key7 => 6, Key.Key8 => 7, Key.Key9 => 8,
            _ => -1
        };

        if (slot < 0) return;

        // Update active slot; InventoryPanel subscribes to HotbarKeyPressed to handle assignment.
        LocalState.SetActiveHotbarSlot(slot);
        LocalState.NotifyHotbarKeyPressed(slot);
        GetViewport().SetInputAsHandled();
    }

    // ── Build UI ──────────────────────────────────────────────────────────────

    private void BuildUI()
    {
        float totalWidth = SLOT_COUNT * SLOT_SIZE + (SLOT_COUNT - 1) * SLOT_GAP;

        var container = new HBoxContainer
        {
            AnchorLeft   = 0.5f,
            AnchorRight  = 0.5f,
            AnchorTop    = 1.0f,
            AnchorBottom = 1.0f,
            OffsetLeft   = -totalWidth * 0.5f,
            OffsetRight  =  totalWidth * 0.5f,
            OffsetTop    = -(SLOT_SIZE + MARGIN_BOTTOM),
            OffsetBottom = -MARGIN_BOTTOM
        };
        container.AddThemeConstantOverride("separation", (int)SLOT_GAP);
        AddChild(container);

        for (int i = 0; i < SLOT_COUNT; i++)
        {
            var panel = BuildSlot(i);
            container.AddChild(panel);
            _slotPanels[i] = panel;
        }

        HighlightActiveSlot();
    }

    private PanelContainer BuildSlot(int index)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(SLOT_SIZE, SLOT_SIZE)
        };

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 0);
        panel.AddChild(vbox);

        // Slot number — top-left, small.
        var numLabel = new Label
        {
            Text     = (index + 1).ToString(),
            Modulate = new Color(0.7f, 0.7f, 0.7f)
        };
        numLabel.AddThemeFontSizeOverride("font_size", 10);
        vbox.AddChild(numLabel);

        // Item name — centred, fills remaining space.
        var itemLabel = new Label
        {
            Text                = "",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            AutowrapMode        = TextServer.AutowrapMode.WordSmart,
            SizeFlagsVertical   = Control.SizeFlags.ExpandFill
        };
        itemLabel.AddThemeFontSizeOverride("font_size", 11);
        _itemLabels[index] = itemLabel;
        vbox.AddChild(itemLabel);

        return panel;
    }

    // ── Refresh ───────────────────────────────────────────────────────────────

    private void RefreshSlot(int slot)
    {
        string? itemId = LocalState.GetHotbarSlot(slot);
        _itemLabels[slot].Text = itemId != null ? ShortName(itemId) : "";
    }

    private void HighlightActiveSlot()
    {
        for (int i = 0; i < SLOT_COUNT; i++)
        {
            _slotPanels[i].Modulate = i == LocalState.ActiveHotbarSlot
                ? new Color(1f, 0.9f, 0.25f)  // yellow highlight
                : Colors.White;
        }
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnHotbarSlotChanged(int slot, string? _itemId) => RefreshSlot(slot);

    private void OnActiveSlotChanged(int _slot) => HighlightActiveSlot();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string ShortName(string itemId)
    {
        string loc = Loc.T(itemId + ".name");
        if (!loc.StartsWith('[')) return loc;
        // Fallback to last segment of itemId if no loc entry found.
        int dot = itemId.LastIndexOf('.');
        return dot >= 0 ? itemId[(dot + 1)..] : itemId;
    }
}
