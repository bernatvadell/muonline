using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Network.Packets.ConnectServer;
using Client.Main.Core.Models;            // For ServerInfo
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Client.Main.Networking.PacketHandling.Handlers
{
    /// <summary>
    /// Processes packets from the connect server, including handshake, server list, and connection info.
    /// </summary>
    public class ConnectServerHandler : IGamePacketHandler
    {
        // ──────────────────────────── Fields ────────────────────────────
        private readonly ILogger<ConnectServerHandler> _logger;
        private readonly NetworkManager _networkManager;

        // ───────────────────────── Constructors ─────────────────────────
        public ConnectServerHandler(ILoggerFactory loggerFactory, NetworkManager networkManager)
        {
            _logger = loggerFactory.CreateLogger<ConnectServerHandler>();
            _networkManager = networkManager;
        }

        // ───────────────────────── Packet Handlers ─────────────────────────

        /// <summary>
        /// Handles the initial Hello packet from the connect server.
        /// </summary>
        public Task HandleHelloAsync(Memory<byte> packet)
        {
            _logger.LogInformation("👋 Received Hello from Connect Server.");
            _networkManager.ProcessHelloPacket();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles the response containing the list of available game servers.
        /// </summary>
        public Task HandleServerListResponseAsync(Memory<byte> packet)
        {
            _logger.LogInformation("📊 Received Server List Response.");
            try
            {
                var response = new ServerListResponse(packet);
                var servers = new List<ServerInfo>();
                ushort count = response.ServerCount;
                _logger.LogInformation("Server count: {Count}", count);

                for (int i = 0; i < count; i++)
                {
                    var info = response[i];
                    servers.Add(new ServerInfo
                    {
                        ServerId = info.ServerId,
                        LoadPercentage = info.LoadPercentage
                    });
                    _logger.LogDebug("-> Server ID: {Id}, Load: {Load}%", info.ServerId, info.LoadPercentage);
                }

                _networkManager.StoreServerList(servers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing ServerListResponse packet.");
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles the response containing connection details for the selected game server.
        /// </summary>
        public Task HandleConnectionInfoResponseAsync(Memory<byte> packet)
        {
            _logger.LogInformation("🔗 Received Connection Info Response.");
            try
            {
                var info = new ConnectionInfo(packet);
                string ip = info.IpAddress;
                ushort port = info.Port;
                _logger.LogInformation("Game server address: {IP}:{Port}", ip, port);
                _networkManager.SwitchToGameServer(ip, port);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing ConnectionInfoResponse packet.");
            }
            return Task.CompletedTask;
        }
    }
}
