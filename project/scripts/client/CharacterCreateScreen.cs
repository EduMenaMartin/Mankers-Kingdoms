using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Client;

/// <summary>
/// Character creation screen shown before entering the game world.
/// Rolls stats (3d6 per stat), lets the player pick race and class, then
/// writes GameSession.RolledStats / ChosenRaceId / HumanChosenStat / ChosenClassId
/// before transitioning to GameWorld.tscn.
///
/// Supersedes ClassSelectScreen as the entry point for Solo, Host, and Join.
///
/// Scene structure (editor task — see below for exact node names):
///   CharacterCreateScreen  (Control, script: this file)
///   └── VBoxContainer
///       ├── TitleLabel            (Label)
///       ├── StatStrLabel          (Label)
///       ├── StatDexLabel          (Label)
///       ├── StatConLabel          (Label)
///       ├── StatWisLabel          (Label)
///       ├── RaceHumanButton       (Button)
///       ├── RaceDwarfButton       (Button)
///       ├── RaceElfButton         (Button)
///       ├── RaceHalflingButton    (Button)
///       ├── HumanChoiceContainer  (HBoxContainer — hidden for non-Human races)
///       │   ├── HumanChoiceLabel  (Label)
///       │   ├── ChooseStrButton   (Button)
///       │   ├── ChooseDexButton   (Button)
///       │   ├── ChooseConButton   (Button)
///       │   └── ChooseWisButton   (Button)
///       ├── FighterButton         (Button)
///       ├── RangerButton          (Button)
///       ├── RerollButton          (Button)
///       └── ConfirmButton         (Button)
/// </summary>
public partial class CharacterCreateScreen : Control
{
    // --- Node refs ---
    private Label   _titleLabel            = null!;
    private Label   _statStrLabel          = null!;
    private Label   _statDexLabel          = null!;
    private Label   _statConLabel          = null!;
    private Label   _statWisLabel          = null!;
    private Button  _raceHumanButton       = null!;
    private Button  _raceDwarfButton       = null!;
    private Button  _raceElfButton         = null!;
    private Button  _raceHalflingButton    = null!;
    private Label   _raceDescLabel         = null!;
    private Control _humanChoiceContainer  = null!;
    private Button  _chooseStrButton       = null!;
    private Button  _chooseDexButton       = null!;
    private Button  _chooseConButton       = null!;
    private Button  _chooseWisButton       = null!;
    private Button  _fighterButton         = null!;
    private Button  _rangerButton          = null!;
    private Label   _classDescLabel        = null!;
    private Button  _rerollButton          = null!;
    private Button  _confirmButton         = null!;

    // --- State ---
    private StatBlock _rawStats;
    private string    _selectedRaceId    = "race.human";
    private string?   _selectedHumanStat = null;
    private string    _selectedClassId   = "class.fighter";

    // Client-side UI only — System.Random is fine here (not server simulation, not seeded)
    private readonly System.Random _rng = new();

    public override void _Ready()
    {
        _titleLabel           = (Label)FindChild("TitleLabel");
        _statStrLabel         = (Label)FindChild("StatStrLabel");
        _statDexLabel         = (Label)FindChild("StatDexLabel");
        _statConLabel         = (Label)FindChild("StatConLabel");
        _statWisLabel         = (Label)FindChild("StatWisLabel");
        _raceHumanButton      = (Button)FindChild("RaceHumanButton");
        _raceDwarfButton      = (Button)FindChild("RaceDwarfButton");
        _raceElfButton        = (Button)FindChild("RaceElfButton");
        _raceHalflingButton   = (Button)FindChild("RaceHalflingButton");
        _raceDescLabel        = (Label)FindChild("RaceDescLabel");
        _humanChoiceContainer = (Control)FindChild("HumanChoiceContainer");
        _chooseStrButton      = (Button)FindChild("ChooseStrButton");
        _chooseDexButton      = (Button)FindChild("ChooseDexButton");
        _chooseConButton      = (Button)FindChild("ChooseConButton");
        _chooseWisButton      = (Button)FindChild("ChooseWisButton");
        _fighterButton        = (Button)FindChild("FighterButton");
        _rangerButton         = (Button)FindChild("RangerButton");
        _classDescLabel       = (Label)FindChild("ClassDescLabel");
        _rerollButton         = (Button)FindChild("RerollButton");
        _confirmButton        = (Button)FindChild("ConfirmButton");

        // Check every required node and report exactly which are missing.
        // A single missing node causes a NullReferenceException that silently aborts _Ready.
        var missing = new System.Text.StringBuilder();
        if (_statStrLabel         == null) missing.Append(" StatStrLabel");
        if (_statDexLabel         == null) missing.Append(" StatDexLabel");
        if (_statConLabel         == null) missing.Append(" StatConLabel");
        if (_statWisLabel         == null) missing.Append(" StatWisLabel");
        if (_raceHumanButton      == null) missing.Append(" RaceHumanButton");
        if (_raceDwarfButton      == null) missing.Append(" RaceDwarfButton");
        if (_raceElfButton        == null) missing.Append(" RaceElfButton");
        if (_raceHalflingButton   == null) missing.Append(" RaceHalflingButton");
        if (_raceDescLabel        == null) missing.Append(" RaceDescLabel");
        if (_humanChoiceContainer == null) missing.Append(" HumanChoiceContainer");
        if (_chooseStrButton      == null) missing.Append(" ChooseStrButton");
        if (_chooseDexButton      == null) missing.Append(" ChooseDexButton");
        if (_chooseConButton      == null) missing.Append(" ChooseConButton");
        if (_chooseWisButton      == null) missing.Append(" ChooseWisButton");
        if (_fighterButton        == null) missing.Append(" FighterButton");
        if (_rangerButton         == null) missing.Append(" RangerButton");
        if (_classDescLabel       == null) missing.Append(" ClassDescLabel");
        if (_rerollButton         == null) missing.Append(" RerollButton");
        if (_confirmButton        == null) missing.Append(" ConfirmButton");

        if (missing.Length > 0)
        {
            GD.PrintErr($"[CharacterCreate] Missing nodes:{missing}  — check node names in the scene (case-sensitive).");
            return;
        }

        // Static text
        if (_titleLabel != null) _titleLabel.Text = Loc.T("charCreate.title");

        // Section headers — optional: missing nodes are silently skipped
        var statsHeader = (Label?)FindChild("Stats");
        if (statsHeader != null) statsHeader.Text = Loc.T("charCreate.stats.label");
        var raceHeader  = (Label?)FindChild("Race");
        if (raceHeader  != null) raceHeader.Text  = Loc.T("charCreate.race.label");
        var classHeader = (Label?)FindChild("Class");
        if (classHeader != null) classHeader.Text = Loc.T("charCreate.class.label");

        _raceHumanButton.Text    = Loc.T("race.human.name");
        _raceDwarfButton.Text    = Loc.T("race.dwarf.name");
        _raceElfButton.Text      = Loc.T("race.elf.name");
        _raceHalflingButton.Text = Loc.T("race.halfling.name");

        var humanChoiceLabel = (Label?)FindChild("HumanChoiceLabel");
        if (humanChoiceLabel != null) humanChoiceLabel.Text = Loc.T("charCreate.human.choice");
        _chooseStrButton.Text = Loc.T("charCreate.stat.str");
        _chooseDexButton.Text = Loc.T("charCreate.stat.dex");
        _chooseConButton.Text = Loc.T("charCreate.stat.con");
        _chooseWisButton.Text = Loc.T("charCreate.stat.wis");

        _fighterButton.Text = Loc.T("class.fighter.name");
        _rangerButton.Text  = Loc.T("class.ranger.name");

        _rerollButton.Text  = Loc.T("charCreate.reroll");
        _confirmButton.Text = Loc.T("charCreate.confirm");

        // Create ButtonGroups in code so all buttons in each group share one instance.
        // Editor-assigned ButtonGroup resources are per-button by default (each "New ButtonGroup"
        // click creates a separate resource), which breaks exclusive selection.
        var raceGroup = new ButtonGroup();
        _raceHumanButton.ButtonGroup    = raceGroup;
        _raceDwarfButton.ButtonGroup    = raceGroup;
        _raceElfButton.ButtonGroup      = raceGroup;
        _raceHalflingButton.ButtonGroup = raceGroup;
        raceGroup.Pressed += OnRaceGroupPressed;

        // AllowUnpress=true so clearing the selection on reroll/race-switch is possible.
        var bonusStatGroup = new ButtonGroup { AllowUnpress = true };
        _chooseStrButton.ButtonGroup = bonusStatGroup;
        _chooseDexButton.ButtonGroup = bonusStatGroup;
        _chooseConButton.ButtonGroup = bonusStatGroup;
        _chooseWisButton.ButtonGroup = bonusStatGroup;
        bonusStatGroup.Pressed += OnBonusStatGroupPressed;

        var classGroup = new ButtonGroup();
        _fighterButton.ButtonGroup = classGroup;
        _rangerButton.ButtonGroup  = classGroup;
        classGroup.Pressed += OnClassGroupPressed;

        _rerollButton.Pressed  += OnReroll;
        _confirmButton.Pressed += OnConfirm;

        // Sync initial visual state to match logical defaults without re-firing handlers.
        _raceHumanButton.SetPressedNoSignal(true);
        _fighterButton.SetPressedNoSignal(true);

        // Initial roll — Human selected, Fighter selected, no bonus stat chosen yet
        RollStats();
        _classDescLabel.Text = Loc.T($"{_selectedClassId}.desc");
    }

    // ---------------------------------------------------------------------------
    // Rolling
    // ---------------------------------------------------------------------------

    private void RollStats()
    {
        _rawStats = new StatBlock(Roll3d6(), Roll3d6(), Roll3d6(), Roll3d6());
        _selectedHumanStat = null; // reroll clears any prior bonus stat choice
        _chooseStrButton?.ButtonGroup?.GetPressedButton()?.SetPressedNoSignal(false);
        RefreshDisplay();
    }

    private int Roll3d6() => _rng.Next(1, 7) + _rng.Next(1, 7) + _rng.Next(1, 7);

    // ---------------------------------------------------------------------------
    // Event handlers
    // ---------------------------------------------------------------------------

    private void OnReroll() => RollStats();

    private void OnRaceGroupPressed(BaseButton btn)
    {
        _selectedRaceId = btn switch
        {
            _ when btn == _raceHumanButton    => "race.human",
            _ when btn == _raceDwarfButton    => "race.dwarf",
            _ when btn == _raceElfButton      => "race.elf",
            _ when btn == _raceHalflingButton => "race.halfling",
            _                                 => _selectedRaceId
        };
        _selectedHumanStat = null; // clear bonus-stat choice whenever race changes
        // Clear bonus-stat button visual selection — requires allow_unpress=true on BonusStatButtonGroup
        _chooseStrButton.ButtonGroup?.GetPressedButton()?.SetPressedNoSignal(false);
        RefreshDisplay();
    }

    private void OnBonusStatGroupPressed(BaseButton btn)
    {
        _selectedHumanStat = btn switch
        {
            _ when btn == _chooseStrButton => "Str",
            _ when btn == _chooseDexButton => "Dex",
            _ when btn == _chooseConButton => "Con",
            _ when btn == _chooseWisButton => "Wis",
            _                              => _selectedHumanStat
        };
        RefreshDisplay();
    }

    private void OnClassGroupPressed(BaseButton btn)
    {
        _selectedClassId = btn == _fighterButton ? "class.fighter" : "class.ranger";
        _classDescLabel.Text = Loc.T($"{_selectedClassId}.desc");
    }

    private void OnConfirm()
    {
        var race = RaceRegistry.Find(_selectedRaceId);
        GameSession.RolledStats     = race?.Apply(_rawStats, _selectedHumanStat) ?? _rawStats.Clamped();
        GameSession.ChosenRaceId    = _selectedRaceId;
        GameSession.HumanChosenStat = _selectedHumanStat;
        GameSession.ChosenClassId   = _selectedClassId;
        // Stamp a unique save slot name so the save lands in its own file.
        GameSession.SaveName = $"save_{System.DateTime.Now:yyyyMMdd_HHmmss}";
        // Each new game gets a fresh random world. See BUGS.md [P2] for why this was missing.
        GameSession.WorldSeed = (uint)Godot.GD.Randi();
        GetTree().ChangeSceneToFile("res://scenes/GameWorld.tscn");
    }

    // ---------------------------------------------------------------------------
    // Display
    // ---------------------------------------------------------------------------

    private void RefreshDisplay()
    {
        bool isHuman = _selectedRaceId == "race.human";
        _humanChoiceContainer.Visible = isHuman;

        // Compute effective stats (raw + race modifiers; human choice applied only once chosen)
        var race      = RaceRegistry.Find(_selectedRaceId);
        var effective = race?.Apply(_rawStats, _selectedHumanStat) ?? _rawStats.Clamped();

        _statStrLabel.Text = FormatStat(Loc.T("charCreate.stat.str"), _rawStats.Str, effective.Str);
        _statDexLabel.Text = FormatStat(Loc.T("charCreate.stat.dex"), _rawStats.Dex, effective.Dex);
        _statConLabel.Text = FormatStat(Loc.T("charCreate.stat.con"), _rawStats.Con, effective.Con);
        _statWisLabel.Text = FormatStat(Loc.T("charCreate.stat.wis"), _rawStats.Wis, effective.Wis);

        _raceDescLabel.Text = Loc.T($"{_selectedRaceId}.desc");

        // Confirm is locked until Human player has picked a bonus stat
        _confirmButton.Disabled = isHuman && _selectedHumanStat == null;
    }

    private static string FormatStat(string label, int raw, int effective)
    {
        if (effective != raw)
            return $"{label}: {raw} \u2192 {effective}";  // → arrow
        return $"{label}: {raw}";
    }
}
