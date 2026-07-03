using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Client;

public partial class OptionsMenuController : Control
{
	private HSlider _masterVolumeSlider = null!;
	private OptionButton _graphicsButton = null!;
	private OptionButton _languageButton = null!;
	private Button _backButton = null!;

	public override void _Ready()
	{
		_masterVolumeSlider = GetNode<HSlider>("%MasterVolumeSlider");
		_graphicsButton = GetNode<OptionButton>("%GraphicsButton");
		_languageButton = GetNode<OptionButton>("%LanguageButton");
		_backButton = GetNode<Button>("%BackButton");

		_backButton.Text = Loc.T("options.back");

		_masterVolumeSlider.MinValue = 0.0;
		_masterVolumeSlider.MaxValue = 1.0;
		_masterVolumeSlider.Step = 0.01;
		_masterVolumeSlider.Value = DbToLinear(AudioServer.GetBusVolumeDb(0));

		_graphicsButton.Clear();
		_graphicsButton.AddItem(Loc.T("options.graphics.low"));
		_graphicsButton.AddItem(Loc.T("options.graphics.medium"));
		_graphicsButton.AddItem(Loc.T("options.graphics.high"));
		_graphicsButton.Selected = 1; // Medium default

		_languageButton.Clear();
		_languageButton.AddItem("English");
		_languageButton.Selected = 0;
		_languageButton.Disabled = true; // Only English in M1; more languages added via community translation

		_masterVolumeSlider.ValueChanged += OnVolumeChanged;
		_graphicsButton.ItemSelected += OnGraphicsChanged;
		_backButton.Pressed += () => GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
	}

	private static void OnVolumeChanged(double value)
	{
		var db = value > 0.0001 ? Mathf.LinearToDb((float)value) : -80f; // -80 dB ≈ silence
		AudioServer.SetBusVolumeDb(0, db);
	}

	private static void OnGraphicsChanged(long index)
	{
		// M1 stub: graphics quality switching deferred to M2 (no renderer tuning yet).
		GD.Print($"[Options] graphics quality set to index {index}");
	}

	private static double DbToLinear(float db) =>
		db <= -80f ? 0.0 : Mathf.DbToLinear(db);
}
