using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Network;
using Client.Main.Networking.PacketHandling;
using System.Threading.Tasks;
using System; // For PacketBuilder

namespace Client.Main.Networking.Services
{
    /// <summary>
    ///  Service class responsible for handling communication specifically with the Connect Server.
    ///  Provides methods for requesting server lists and connection information for game servers.
    /// </summary>
    public class ConnectServerService
    {
        private readonly ConnectionManager _connectionManager; // Manages the network connection to the Connect Server
        private readonly ILogger<ConnectServerService> _logger; // Logger for this service

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectServerService"/> class.
        /// </summary>
        /// <param name="connectionManager">The connection manager instance to use for sending packets to the Connect Server.</param>
        /// <param name="logger">The logger instance for logging service operations and errors.</param>
        public ConnectServerService(ConnectionManager connectionManager, ILogger<ConnectServerService> logger)
        {
            _connectionManager = connectionManager;
            _logger = logger;
        }

        /// <summary>
        /// Sends a request to the Connect Server to get the list of available game servers.
        /// </summary>
        /// <returns>A Task representing the asynchronous operation.</returns>
        public async Task RequestServerListAsync()
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("🔒 No connection to Connect Server – cannot request server list.");
                return; // Exit if not connected to Connect Server
            }

            _logger.LogInformation("📜 Sending ServerListRequest packet...");
            try
            {
                // Use PacketBuilder to create and send the ServerListRequest packet
                await _connectionManager.Connection.SendAsync(() =>
                    PacketBuilder.BuildServerListRequestPacket(_connectionManager.Connection.Output)
                );
                _logger.LogInformation("✔️ ServerListRequest packet sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error while sending ServerListRequest packet.");
            }
        }

        /// <summary>
        /// Sends a request to the Connect Server to get connection information for a specific game server.
        /// This information typically includes the IP address and port of the game server.
        /// </summary>
        /// <param name="serverId">The ID of the game server for which to request connection information.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        public async Task RequestConnectionInfoAsync(ushort serverId)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("🔒 No connection to Connect Server – cannot request connection info.");
                return; // Exit if not connected to Connect Server
            }

            _logger.LogInformation("ℹ️ Requesting connection info for Server ID {ServerId}...", serverId);
            try
            {
                // Use PacketBuilder to create and send the ConnectionInfoRequest packet
                await _connectionManager.Connection.SendAsync(() =>
                    PacketBuilder.BuildServerInfoRequestPacket(_connectionManager.Connection.Output, serverId)
                );
                _logger.LogInformation("✔️ ConnectionInfoRequest packet sent for Server ID {ServerId}.", serverId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error while sending ConnectionInfoRequest packet for Server ID {ServerId}.", serverId);
            }
        }
    }
}