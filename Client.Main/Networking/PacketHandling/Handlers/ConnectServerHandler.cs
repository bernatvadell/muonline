using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Network.Packets.ConnectServer;
using Client.Main.Client; // For SimpleLoginClient
using Client.Main.Core.Models; // For ServerInfo
using Client.Main.Core.Utilities;
using System.Threading.Tasks;
using System;
using System.Collections.Generic; // For PacketHandlerAttribute

namespace Client.Main.Networking.PacketHandling.Handlers
{
    /// <summary>
    /// Handles packets received from the Connect Server.
    /// </summary>
    public class ConnectServerHandler : IGamePacketHandler // Can reuse interface marker
    {
        private readonly ILogger<ConnectServerHandler> _logger;
        private readonly NetworkManager _networkManager; // Needed for state changes and server list storage

        public ConnectServerHandler(ILoggerFactory loggerFactory, NetworkManager networkManager)
        {
            _logger = loggerFactory.CreateLogger<ConnectServerHandler>();
            _networkManager = networkManager;
        }

        // Note: These handlers are registered manually in PacketRouter for now.
        // If using attributes, they would need a way to specify Connect Server context.

        // ConnectServerHandler.cs
        public Task HandleHelloAsync(Memory<byte> packet) // Usuń async, jeśli nic nie awaitujesz
        {
            _logger.LogInformation("👋 Received Hello from Connect Server.");
            // Tylko powiadom NetworkManager, że można zmienić stan
            _networkManager.ProcessHelloPacket();
            return Task.CompletedTask;
        }

        public Task HandleServerListResponseAsync(Memory<byte> packet)
        {
            _logger.LogInformation("📊 Received Server List Response.");
            try
            {
                var serverListResponse = new ServerListResponse(packet);
                var servers = new List<ServerInfo>();
                ushort serverCount = serverListResponse.ServerCount;
                _logger.LogInformation("  Server Count: {Count}", serverCount);

                for (int i = 0; i < serverCount; i++)
                {
                    var serverLoadInfo = serverListResponse[i];
                    servers.Add(new ServerInfo
                    {
                        ServerId = serverLoadInfo.ServerId,
                        LoadPercentage = serverLoadInfo.LoadPercentage
                    });
                    _logger.LogDebug("  -> Server ID: {Id}, Load: {Load}%", serverLoadInfo.ServerId, serverLoadInfo.LoadPercentage);
                }
                _networkManager.StoreServerList(servers);
            }
            catch (Exception ex) { _logger.LogError(ex, "💥 Error parsing ServerListResponse packet."); }
            return Task.CompletedTask;
        }

        public Task HandleConnectionInfoResponseAsync(Memory<byte> packet)
        {
            _logger.LogInformation("🔗 Received Connection Info Response.");
            try
            {
                var connectionInfo = new ConnectionInfo(packet);
                string ipAddress = connectionInfo.IpAddress;
                ushort port = connectionInfo.Port;
                _logger.LogInformation("  -> Game Server Address: {IP}:{Port}", ipAddress, port);
                _networkManager.SwitchToGameServer(ipAddress, port);
            }
            catch (Exception ex) { _logger.LogError(ex, "💥 Error parsing ConnectionInfoResponse packet."); }
            return Task.CompletedTask;
        }
    }
}