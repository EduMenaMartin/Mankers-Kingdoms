namespace MankersKingdoms.Shared;

/// <summary>
/// Lightweight session-intent bridge between the Main Menu and Game World scenes.
/// Set by the menu before calling ChangeSceneToFile; read by NetworkManager._Ready().
/// Not persisted across app restarts.
/// </summary>
public static class GameSession
{
    public enum SessionIntent { None, Solo, Host, Join }

    public static SessionIntent Intent { get; set; } = SessionIntent.None;

    /// <summary>IP address entered in the Join field. Defaults to loopback.</summary>
    public static string JoinAddress { get; set; } = "127.0.0.1";

    /// <summary>World seed used for terrain and tree generation. Same value on all peers.</summary>
    public static uint WorldSeed { get; set; } = 42u;

    /// <summary>
    /// Class kit selected on ClassSelectScreen before entering the game world.
    /// Set by ClassSelectScreen; read by HealthSystem.OnPlayerConnected to distribute
    /// the starting kit. Defaults to Fighter so solo/debug entry still works.
    /// </summary>
    public static string ChosenClassId { get; set; } = "class.fighter";

    public static void Reset()
    {
        Intent        = SessionIntent.None;
        JoinAddress   = "127.0.0.1";
        ChosenClassId = "class.fighter";
    }
}
