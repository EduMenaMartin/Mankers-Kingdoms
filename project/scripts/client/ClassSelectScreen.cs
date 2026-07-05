using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Client;

/// <summary>
/// Two-button class selection screen shown after the Main Menu (before the game world loads).
/// Appears for all session types: Solo, Host, and Join.
///
/// Writes GameSession.ChosenClassId and immediately transitions to GameWorld.tscn.
/// The server's HealthSystem.OnPlayerConnected reads ChosenClassId to distribute the
/// appropriate starting kit (longsword+shield for Fighter; shortbow+arrows for Ranger).
///
/// Scene structure (editor task — see CLAUDE.md, docs/scene_workflow.md):
///   ClassSelectScreen (Control, script: this file)
///   └── VBoxContainer
///       ├── %TitleLabel     (Label)
///       ├── %SubtitleLabel  (Label)
///       └── HBoxContainer
///           ├── %FighterButton (Button)
///           └── %RangerButton  (Button)
/// </summary>
public partial class ClassSelectScreen : Control
{
    private Label  _titleLabel    = null!;
    private Label  _subtitleLabel = null!;
    private Button _fighterButton = null!;
    private Button _rangerButton  = null!;

    public override void _Ready()
    {
        // FindChild searches recursively by name — does not require "Access as Unique Name"
        // to be set on the nodes, so the scene can be built without % unique-name setup.
        _titleLabel    = (Label)FindChild("TitleLabel");
        _subtitleLabel = (Label)FindChild("SubtitleLabel");
        _fighterButton = (Button)FindChild("FighterButton");
        _rangerButton  = (Button)FindChild("RangerButton");

        if (_fighterButton == null || _rangerButton == null)
        {
            GD.PrintErr("[ClassSelect] Scene nodes not found. " +
                "Expected children named: TitleLabel, SubtitleLabel, FighterButton, RangerButton.");
            return;
        }

        if (_titleLabel    != null) _titleLabel.Text    = Loc.T("class.select.title");
        if (_subtitleLabel != null) _subtitleLabel.Text = Loc.T("class.select.subtitle");
        _fighterButton.Text = Loc.T("class.fighter.name");
        _rangerButton.Text  = Loc.T("class.ranger.name");

        _fighterButton.TooltipText = Loc.T("class.fighter.desc");
        _rangerButton.TooltipText  = Loc.T("class.ranger.desc");

        _fighterButton.Pressed += () => OnClassSelected("class.fighter");
        _rangerButton.Pressed  += () => OnClassSelected("class.ranger");
    }

    private void OnClassSelected(string classId)
    {
        GameSession.ChosenClassId = classId;
        GetTree().ChangeSceneToFile("res://scenes/GameWorld.tscn");
    }
}
