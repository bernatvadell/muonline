namespace Client.Main.Core.Models
{
    /// <summary>
    /// Represents information about a game server, as received from the Connect Server.
    /// This class holds details such as the server's ID, load percentage, and optionally its name, IP address, and port.
    /// </summary>
    public class ServerInfo
    {
        /// <summary>
        /// Gets or sets the unique identifier for the game server.
        /// This ID is used to reference the server in communication with the Connect Server.
        /// </summary>
        public ushort ServerId { get; set; }

        /// <summary>
        /// Gets or sets the current load percentage of the game server.
        /// This value indicates how busy the server is, typically ranging from 0% to 100%.
        /// </summary>
        public byte LoadPercentage { get; set; }

        /// <summary>
        /// Gets or sets the optional name of the game server.
        /// The server name might not always be provided by the protocol, so it can be null.
        /// </summary>
        public string ServerName { get; set; } // Optional: Name might not be provided by protocol

        /// <summary>
        /// Gets or sets the optional IP address of the game server.
        /// This is typically populated after requesting connection information for a specific server.
        /// </summary>
        public string IpAddress { get; set; } // Optional: Store IP if needed after requesting connection info

        /// <summary>
        /// Gets or sets the port number of the game server.
        /// This is typically populated after requesting connection information for a specific server.
        /// </summary>
        public ushort Port { get; set; } // Optional: Store Port if needed after requesting connection info

        /// <inheritdoc />
        public override string ToString()
        {
            // Build a string representation of the ServerInfo object, including Server ID, Name (if available), and Load Percentage.
            string namePart = string.IsNullOrEmpty(ServerName) ? string.Empty : $" ({ServerName})";
            return $"Server ID: {ServerId}{namePart}, Load: {LoadPercentage}%";
        }
    }
}