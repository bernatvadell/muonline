using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Network;
using Client.Main.Networking.PacketHandling;

namespace Client.Main.Networking.Services
{
    /// <summary>
    /// Handles communication with the Connect Server:
    /// requesting server lists and game server connection info.
    /// </summary>
    public class ConnectServerService
    {
        // ───────────────────────── Fields ─────────────────────────

        private readonly ConnectionManager _connectionManager;
        private readonly ILogger<ConnectServerService> _logger;

        // ─────────────────────── Constructors ───────────────────────

        public ConnectServerService(
            ConnectionManager connectionManager,
            ILogger<ConnectServerService> logger)
        {
            _connectionManager = connectionManager;
            _logger = logger;
        }

        // ─────────────────────── Public Methods ───────────────────────

        /// <summary>
        /// Requests the list of available game servers from the Connect Server.
        /// </summary>
        public async Task RequestServerListAsync()
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("No connection to Connect Server; cannot request server list.");
                return;
            }

            _logger.LogInformation("Sending ServerListRequest packet...");
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                    PacketBuilder.BuildServerListRequestPacket(_connectionManager.Connection.Output));
                _logger.LogInformation("ServerListRequest packet sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending ServerListRequest packet.");
            }
        }

        /// <summary>
        /// Requests connection information (IP and port) for the specified game server.
        /// </summary>
        /// <param name="serverId">The ID of the target game server.</param>
        public async Task RequestConnectionInfoAsync(ushort serverId)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("No connection to Connect Server; cannot request connection info.");
                return;
            }

            _logger.LogInformation("Requesting connection info for Server ID {ServerId}...", serverId);
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                    PacketBuilder.BuildServerInfoRequestPacket(_connectionManager.Connection.Output, serverId));
                _logger.LogInformation("ConnectionInfoRequest packet sent for Server ID {ServerId}.", serverId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending ConnectionInfoRequest packet for Server ID {ServerId}.", serverId);
            }
        }
    }
}
