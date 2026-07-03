using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Client;

public partial class SplashScreen : Control
{
    private const double SPLASH_DURATION = 2.0; // seconds before auto-advancing to main menu

    // Kept as field so _Process can guard against running callbacks before init
    private bool _steamReady;
    private double _elapsed;

    public override void _Ready()
    {
        Loc.LoadJson(FileAccess.GetFileAsString("res://data/lang/en.json"));
        _steamReady = SteamInit();
    }

    public override void _Process(double delta)
    {
        // Steam callbacks must be pumped every frame after a successful init.
        // Without this, no Steam events (achievements, lobby, networking) will fire.
        // M1 note: proper Steam init moves to server/NetworkManager in a later milestone (ADR-0002, ADR-0005).
        if (_steamReady)
            Engine.GetSingleton("Steam").Call("run_callbacks");

        _elapsed += delta;
        if (_elapsed >= SPLASH_DURATION)
            GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
    }

    // M0 smoke test — proper Steam init moves to server/ in M1 (ADR-0002, ADR-0005)
    // status 0 = k_ESteamAPIInitResult_OK (Steamworks SDK enum, GodotSteam 4.20 passes through)
    private static bool SteamInit()
    {
        if (!Engine.HasSingleton("Steam"))
        {
            GD.PrintErr("[Steam] singleton not found — GDExtension not loaded");
            return false;
        }

        var steam = Engine.GetSingleton("Steam");
        var result = steam.Call("steamInitEx").AsGodotDictionary();
        var status = result["status"].AsInt32();
        GD.Print($"[Steam] steamInitEx status={status} verbal=\"{result["verbal"]}\"");

        if (status != 0)
        {
            GD.PrintErr($"[Steam] init failed (status={status})");
            return false;
        }

        GD.Print($"[Steam] active — SteamID={steam.Call("getSteamID")}");
        return true;
    }
}
