using System.Collections.Generic;
using System.Text;
using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Client;

/// <summary>
/// Build menu CanvasLayer — opened by the "build_menu" action (B key).
/// Lists available building types with their resource costs.
/// Selecting a building hides the menu and hands off to PlacementController.
///
/// Build mode is transient: entering this menu switches LocalState to build mode;
/// closing it (via B, Close button, or Escape) restores combat mode automatically.
/// PlacementController also restores combat mode after a place or cancel.
/// </summary>
public partial class BuildMenu : CanvasLayer
{
	// Place buttons keyed by building ID — refreshed when the menu opens.
	private readonly Dictionary<string, Button> _placeButtons = new();

	public override void _Ready()
	{
		Layer   = 20;
		Visible = false;
		BuildUI();
	}

	// Disable/enable Place buttons based on current founder status.
	// Called each time the menu opens so it reflects state changes mid-session.
	private void RefreshFounderState()
	{
		bool isFounder = LocalState.IsFounder;
		foreach (var btn in _placeButtons.Values)
		{
			btn.Disabled    = !isFounder;
			btn.TooltipText = isFounder ? "" : "Plant a Kingdom Marker first (F key)";
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (Visible && @event.IsActionPressed("ui_cancel"))
		{
			CloseMenu();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (!@event.IsActionPressed("build_menu")) return;

		if (!Visible)
		{
			PlacementController.Current?.Cancel(); // cancel any active placement first
			LocalState.SetCombatMode(false);       // enter build mode
			RefreshFounderState();
			Visible = true;
		}
		else
		{
			CloseMenu();
		}
		GetViewport().SetInputAsHandled();
	}

	private void CloseMenu()
	{
		Visible = false;
		LocalState.SetCombatMode(true); // return to combat mode
	}

	private void BuildUI()
	{
		// Semi-transparent full-screen backdrop.
		var backdrop = new ColorRect { Color = new Color(0f, 0f, 0f, 0.4f) };
		backdrop.AnchorRight  = 1f;
		backdrop.AnchorBottom = 1f;
		AddChild(backdrop);

		// Centred panel.
		var panel = new Panel();
		panel.AnchorLeft   = 0.5f;
		panel.AnchorRight  = 0.5f;
		panel.AnchorTop    = 0.5f;
		panel.AnchorBottom = 0.5f;
		panel.OffsetLeft   = -175f;
		panel.OffsetRight  =  175f;
		panel.OffsetTop    = -180f;
		panel.OffsetBottom =  180f;
		AddChild(panel);

		var vbox = new VBoxContainer();
		vbox.AnchorRight  = 1f;
		vbox.AnchorBottom = 1f;
		vbox.AddThemeConstantOverride("separation", 6);
		panel.AddChild(vbox);

		// Title.
		var title = new Label
		{
			Text                = "Build",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		vbox.AddChild(title);

		vbox.AddChild(new HSeparator());

		// One row per building.
		foreach (var b in BuildingRegistry.All)
		{
			var row = new HBoxContainer();
			vbox.AddChild(row);

			var nameLabel = new Label
			{
				Text              = Loc.T(b.DisplayNameKey),
				CustomMinimumSize = new Vector2(110f, 0f)
			};
			row.AddChild(nameLabel);

			var costLabel = new Label
			{
				Text                = FormatCost(b),
				CustomMinimumSize   = new Vector2(80f, 0f),
				HorizontalAlignment = HorizontalAlignment.Right
			};
			row.AddChild(costLabel);

			var btn = new Button { Text = "Place" };
			var id  = b.Id; // capture for lambda
			btn.Pressed += () => OnPlacePressed(id);
			row.AddChild(btn);
			_placeButtons[b.Id] = btn;
		}

		vbox.AddChild(new HSeparator());

		var closeBtn = new Button
		{
			Text                = "Close  [B]",
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
		};
		closeBtn.Pressed += CloseMenu;
		vbox.AddChild(closeBtn);
	}

	private void OnPlacePressed(string buildingId)
	{
		Visible = false;
		// Do NOT restore combat mode here — PlacementController does it after place or cancel.
		PlacementController.SelectBuilding(buildingId);
	}

	private static string FormatCost(BuildingData b)
	{
		var sb = new StringBuilder();
		foreach (var (itemId, count) in b.Cost)
		{
			var label = itemId switch
			{
				"resource.wood" => "Wood",
				_               => itemId
			};
			sb.Append($"{count} {label}  ");
		}
		return sb.ToString().TrimEnd();
	}
}
