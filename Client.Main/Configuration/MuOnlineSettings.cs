using System.Collections.Generic;
using Client.Main.Core.Client;

namespace Client.Main.Configuration
{
    public class PacketLoggingSettings
    {
        public bool ShowWeather { get; set; } = true;
        public bool ShowDamage { get; set; } = true;
    }

    public class MuOnlineSettings
    {
        // Connect Server Settings
        public string ConnectServerHost { get; set; } = "127.0.0.1";
        public int ConnectServerPort { get; set; } = 44405;

        // Game Server Settings (usually obtained from Connect Server)
        // public string GameServerHost { get; set; } = ""; // Not typically set here
        // public int GameServerPort { get; set; }

        // Account Settings
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";

        // Client/Protocol Settings
        public string ProtocolVersion { get; set; } = nameof(TargetProtocolVersion.Season6); // Use nameof for safety
        public string ClientVersion { get; set; } = "1.04d"; // Example default
        public string ClientSerial { get; set; } = "0123456789ABCDEF"; // Example default
        public Dictionary<byte, byte> DirectionMap { get; set; } = new(); // Direction mapping for walk packets
        public PacketLoggingSettings PacketLogging { get; set; } = new();
    }
}