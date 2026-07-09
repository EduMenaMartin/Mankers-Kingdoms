using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Client;

public partial class MainMenuController : Control
{
	private Button _startSoloButton = null!;
	private Button _hostButton = null!;
	private Button _joinButton = null!;
	private Button _optionsButton = null!;
	private Button _exitButton = null!;
	private LineEdit _addressInput = null!;
	private Label _statusLabel = null!;

	public override void _Ready()
	{
		RegisterInputActions();

		_startSoloButton = GetNode<Button>("%StartSoloButton");
		_hostButton = GetNode<Button>("%HostButton");
		_joinButton = GetNode<Button>("%JoinButton");
		_optionsButton = GetNode<Button>("%OptionsButton");
		_exitButton = GetNode<Button>("%ExitButton");
		_addressInput = GetNode<LineEdit>("%AddressInput");
		_statusLabel = GetNode<Label>("%StatusLabel");

		_startSoloButton.Text = Loc.T("menu.start_solo");
		_hostButton.Text = Loc.T("menu.host");
		_joinButton.Text = Loc.T("menu.join");
		_optionsButton.Text = Loc.T("menu.options");
		_exitButton.Text = Loc.T("menu.exit");
		_addressInput.PlaceholderText = Loc.T("menu.address_placeholder");

		_startSoloButton.Pressed += OnStartSolo;
		_hostButton.Pressed += OnHost;
		_joinButton.Pressed += OnJoin;
		_optionsButton.Pressed += OnOptions;
		_exitButton.Pressed += OnExit;
	}

	private void OnStartSolo()
	{
		GameSession.Intent = GameSession.SessionIntent.Solo;
		GetTree().ChangeSceneToFile("res://scenes/CharacterCreateScreen.tscn");
	}

	private void OnHost()
	{
		GameSession.Intent = GameSession.SessionIntent.Host;
		SetStatus(Loc.T("menu.status.hosting"));
		GetTree().ChangeSceneToFile("res://scenes/CharacterCreateScreen.tscn");
	}

	private void OnJoin()
	{
		var address = _addressInput.Text.Trim();
		if (string.IsNullOrEmpty(address)) address = "127.0.0.1";
		GameSession.Intent = GameSession.SessionIntent.Join;
		GameSession.JoinAddress = address;
		SetStatus(Loc.T("menu.status.joining"));
		GetTree().ChangeSceneToFile("res://scenes/CharacterCreateScreen.tscn");
	}

	private void OnOptions() =>
		GetTree().ChangeSceneToFile("res://scenes/OptionsMenu.tscn");

	private void OnExit() => GetTree().Quit();

	private void SetStatus(string message) => _statusLabel.Text = message;

	// Register WASD input actions at runtime so we don't have to maintain project.godot manually.
	// Called here because MainMenu is the first scene that needs input, and _Ready is safe to
	// call multiple times (HasAction guard).
	private static void RegisterInputActions()
	{
		AddKeyAction("move_left",    Key.A);
		AddKeyAction("move_right",   Key.D);
		AddKeyAction("move_forward", Key.W);
		AddKeyAction("move_back",    Key.S);
		AddKeyAction("interact",     Key.E);
		AddKeyAction("plant_marker", Key.F);
		AddKeyAction("build_menu",   Key.B);
		AddKeyAction("eat_food",        Key.Tab);
		AddKeyAction("toggle_weapon",   Key.Q);
		AddKeyAction("open_map",        Key.M);
		AddKeyAction("open_inventory",  Key.I);
		AddKeyAction("char_sheet",      Key.K);
	}

	private static void AddKeyAction(string action, Key key)
	{
		if (InputMap.HasAction(action)) return;
		InputMap.AddAction(action);
		var ev = new InputEventKey { PhysicalKeycode = key };
		InputMap.ActionAddEvent(action, ev);
	}
}
