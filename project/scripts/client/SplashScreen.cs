using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Client;

public partial class SplashScreen : Control
{
    public override void _Ready()
    {
        Loc.LoadJson(FileAccess.GetFileAsString("res://data/lang/en.json"));
    }
}
