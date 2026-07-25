using System.Collections.Generic;
using System.Text.Json;
using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Client;

public partial class MainMenuController : Control
{
	private Button        _startSoloButton = null!;
	private Button        _loadButton      = null!;
	private Button        _hostButton      = null!;
	private Button        _joinButton      = null!;
	private Button        _optionsButton   = null!;
	private Button        _exitButton      = null!;
	private LineEdit      _addressInput    = null!;
	private Label         _statusLabel     = null!;
	private LoadGamePanel _loadPanel       = null!;

	public override void _Ready()
	{
		// Wire SaveUtil providers so LoadGamePanel can list saves at the main menu.
		// SaveSystem (GameWorld.tscn) wires these too, but it doesn't exist here.
		SaveUtil.ListSavesProvider   = ListSaves;
		SaveUtil.PeekSessionProvider = PeekSession;

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

		// Inject "Load Game" button after StartSolo in the same VBoxContainer.
		var container = _startSoloButton.GetParent();
		_loadButton = new Button { Text = Loc.T("menu.load_game") };
		container.AddChild(_loadButton);
		container.MoveChild(_loadButton, _startSoloButton.GetIndex() + 1);
		_loadButton.Pressed += OnLoadGame;

		// Add the save-selection panel (full-screen overlay, hidden by default).
		_loadPanel = new LoadGamePanel();
		_loadPanel.OnSaveSelected = OnSaveSelected;
		_loadPanel.OnCancel       = () => _loadPanel.Hide();
		_loadPanel.Hide();
		AddChild(_loadPanel);
	}

	private void OnStartSolo()
	{
		GameSession.Intent = GameSession.SessionIntent.Solo;
		GetTree().ChangeSceneToFile("res://scenes/CharacterCreateScreen.tscn");
	}

	private void OnLoadGame() => _loadPanel.ShowAndRefresh();

	private void OnSaveSelected(string saveName)
	{
		// GameSession.SaveName, ChosenClassId, ChosenRaceId, RolledStats
		// are already set by LoadGamePanel.OnLoadPressed() before this fires.
		GameSession.Intent = GameSession.SessionIntent.Solo;
		GetTree().ChangeSceneToFile("res://scenes/GameWorld.tscn");
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
		AddKeyAction("open_inventory",    Key.I);
		AddKeyAction("char_sheet",        Key.K);
		AddKeyAction("open_assignment",   Key.N);
	}

	private static void AddKeyAction(string action, Key key)
	{
		if (InputMap.HasAction(action)) return;
		InputMap.AddAction(action);
		var ev = new InputEventKey { PhysicalKeycode = key };
		InputMap.ActionAddEvent(action, ev);
	}

	// ── SaveUtil provider implementations ─────────────────────────────────────
	// Mirrors SaveSystem.DoListSaves / DoPeekSession so LoadGamePanel works
	// before GameWorld (and therefore SaveSystem) is ever loaded.

	private static List<string> ListSaves()
	{
		var result = new List<string>();
		var dir = DirAccess.Open(SaveUtil.SaveDir);
		if (dir == null) return result;

		dir.ListDirBegin();
		string name = dir.GetNext();
		while (name != "")
		{
			if (!dir.CurrentIsDir() && name.EndsWith(".json"))
				result.Add(name[..^5]); // strip .json
			name = dir.GetNext();
		}
		dir.ListDirEnd();

		result.Sort(static (a, b) => string.CompareOrdinal(b, a)); // newest-first
		return result;
	}

	private static SessionSave? PeekSession(string saveName)
	{
		var path = $"{SaveUtil.SaveDir}/{saveName}.json";
		if (!FileAccess.FileExists(path)) return null;
		try
		{
			using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
			if (f == null) return null;
			var data = JsonSerializer.Deserialize<SaveData>(f.GetAsText());
			return data?.Session;
		}
		catch { return null; }
	}
}
