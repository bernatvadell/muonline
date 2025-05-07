namespace Client.Main.Core.Client
{
    /// <summary>
    /// Enumeration representing different protocol versions for client compatibility.
    /// </summary>
    public enum TargetProtocolVersion
    {
        Season6,
        Version097,
        Version075
    }

    /// <summary>
    /// Enumeration defining the connection states of the client.
    /// </summary>
    public enum ClientConnectionState
    {
        Initial, // Initial state before any connection attempt
        ConnectingToConnectServer, // Attempting to connect to the Connect Server
        ConnectedToConnectServer, // Successfully connected to the Connect Server
        RequestingServerList, // Requesting the list of game servers from the Connect Server
        ReceivedServerList, // Received the list of game servers
        SelectingServer, // User is in the process of selecting a game server
        RequestingConnectionInfo, // Requesting connection information for the selected game server
        ReceivedConnectionInfo, // Received connection information for the game server
        ConnectingToGameServer, // Attempting to connect to the Game Server
        ConnectedToGameServer, // Successfully connected to the Game Server, ready for login
        Authenticating, // Client is authenticating with the Game Server
        SelectingCharacter, // Client is selecting a character to play
        InGame, // Client is in the game world
        Disconnected // Client is disconnected from all servers
    }
}