using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Server;

/// <summary>
/// ENet multiplayer transport: host a server or join one, and surface peer lifecycle events.
/// Reads GameSession.Intent on _Ready so the GameWorld scene starts the right mode automatically.
///
/// Transport note: ENet is used for M1 LAN bring-up (ARCHITECTURE.md §4.2).
/// GodotSteam SteamMultiplayerPeer (Steam relay + P2P) replaces this when Steam lobbies land.
/// See ADR-0002, ADR-0005.
/// </summary>
public partial class NetworkManager : Node
{
    public const int DEFAULT_PORT = 7777;
    public const int MAX_PEERS = 5; // 6 players max: 1 host + 5 clients

    [Signal] public delegate void PlayerConnectedEventHandler(long peerId);
    [Signal] public delegate void PlayerDisconnectedEventHandler(long peerId);
    [Signal] public delegate void ConnectedToServerEventHandler();
    [Signal] public delegate void ConnectionFailedEventHandler();

    public static NetworkManager Instance { get; private set; } = null!;

    public override void _Ready()
    {
        Instance = this;

        Multiplayer.PeerConnected += id => EmitSignal(SignalName.PlayerConnected, id);
        Multiplayer.PeerDisconnected += id => EmitSignal(SignalName.PlayerDisconnected, id);
        Multiplayer.ConnectedToServer += () => EmitSignal(SignalName.ConnectedToServer);
        Multiplayer.ConnectionFailed += () => EmitSignal(SignalName.ConnectionFailed);

        // GameWorld is entered from the menu with Intent already set.
        GD.Print($"[NetworkManager] _Ready intent={GameSession.Intent}");

        switch (GameSession.Intent)
        {
            case GameSession.SessionIntent.Host:
                StartHost();
                break;
            case GameSession.SessionIntent.Join:
                JoinGame(GameSession.JoinAddress);
                break;
            case GameSession.SessionIntent.Solo:
            case GameSession.SessionIntent.None:
                // No networking. Defer one frame so PlayerSystem._Ready() subscribes first.
                Callable.From(() => EmitSignal(SignalName.PlayerConnected, 1L)).CallDeferred();
                break;
        }
    }

    public Error StartHost(int port = DEFAULT_PORT)
    {
        var peer = new ENetMultiplayerPeer();
        var err = peer.CreateServer(port, MAX_PEERS);
        if (err != Error.Ok)
        {
            GD.PrintErr($"[NetworkManager] CreateServer failed: {err}");
            return err;
        }
        Multiplayer.MultiplayerPeer = peer;
        GD.Print($"[NetworkManager] hosting on :{port}");
        // Host's own peer ID is always 1. Defer so scene siblings can subscribe first.
        Callable.From(() => EmitSignal(SignalName.PlayerConnected, 1L)).CallDeferred();
        return Error.Ok;
    }

    public Error JoinGame(string address, int port = DEFAULT_PORT)
    {
        var peer = new ENetMultiplayerPeer();
        var err = peer.CreateClient(address, port);
        if (err != Error.Ok)
        {
            GD.PrintErr($"[NetworkManager] CreateClient failed: {err}");
            return err;
        }
        Multiplayer.MultiplayerPeer = peer;
        GD.Print($"[NetworkManager] connecting to {address}:{port}");
        return Error.Ok;
    }

    public void Close()
    {
        Multiplayer.MultiplayerPeer?.Close();
        // Reset to OfflineMultiplayerPeer (not null) so GetUniqueId() keeps returning 1.
        // Setting null makes GetUniqueId() return 0, breaking IsMultiplayerAuthority() checks
        // on the next scene load (grey screen + player dot at map origin).
        Multiplayer.MultiplayerPeer = new OfflineMultiplayerPeer();
        GD.Print("[NetworkManager] connection closed");
    }

    public override void _ExitTree()
    {
        Close();
        if (Instance == this) Instance = null!;
    }
}
