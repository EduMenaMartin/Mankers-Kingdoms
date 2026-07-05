using System.Text;
using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Client;

/// <summary>
/// Minimal inventory display: a text label in the top-right corner listing
/// every item the local player carries.
/// Reads from LocalState.Inventory (written by InventorySystem) so there is
/// no client/ → server/ import.
/// Refreshes once per second — sufficient for M3; a signal-driven refresh
/// can replace this in M5 when the full inventory panel is built.
/// </summary>
public partial class InventoryHUD : CanvasLayer
{
	private Label _label = null!;
	private double _timer;
	private const double REFRESH_INTERVAL = 0.5;

	public override void _Ready()
	{
		Layer = 10;

		_label = new Label
		{
			Text                = "",
			AnchorLeft          = 0.5f,
			AnchorRight         = 0.5f,
			AnchorTop           = 0f,
			AnchorBottom        = 0f,
			OffsetLeft          = -120f,
			OffsetRight         = 120f,
			OffsetTop           = 8f,
			OffsetBottom        = 220f,
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter         = Control.MouseFilterEnum.Ignore
		};
		AddChild(_label);
	}

	public override void _Process(double delta)
	{
		_timer += delta;
		if (_timer < REFRESH_INTERVAL) return;
		_timer = 0;
		Refresh();
	}

	private void Refresh()
	{
		var items = LocalState.Inventory.Items;
		if (items.Count == 0)
		{
			_label.Text = "";
			return;
		}

		var sb = new StringBuilder();
		foreach (var (id, count) in items)
		{
			// Show a friendly suffix where we know the key; raw ID otherwise.
			var display = id switch
			{
				"resource.wood"        => "Wood",
				"item.berry"           => "Berry",
				"item.cooked_berry"    => "Cooked Berry",
				"item.weapon.longsword" => "Longsword",
				"item.armor.shield"    => "Shield",
				"item.weapon.shortbow" => "Shortbow",
				"item.weapon.dagger"   => "Dagger",
				"item.arrow"           => "Arrow",
				_                      => id
			};
			sb.AppendLine($"{display}: {count}");
		}

		_label.Text = sb.ToString().TrimEnd();
	}
}
