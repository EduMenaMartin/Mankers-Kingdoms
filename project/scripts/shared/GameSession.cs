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

    public static void Reset()
    {
        Intent = SessionIntent.None;
        JoinAddress = "127.0.0.1";
    }
}
