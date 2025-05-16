using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Network;
using Client.Main.Networking.PacketHandling;
using System;
using System.Threading.Tasks;
using MUnique.OpenMU.Network.Packets.ClientToServer;

namespace Client.Main.Networking.Services
{
    /// <summary>
    /// Manages sending character‐related packets to the game server,
    /// including character list requests, character selection, movement, and animations.
    /// </summary>
    public class CharacterService
    {
        private readonly ConnectionManager _connectionManager;
        private readonly ILogger<CharacterService> _logger;

        public CharacterService(
            ConnectionManager connectionManager,
            ILogger<CharacterService> logger)
        {
            _connectionManager = connectionManager;
            _logger = logger;
        }

        /// <summary>
        /// Requests the list of characters for the current account.
        /// </summary>
        public async Task RequestCharacterListAsync()
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected — cannot request character list.");
                return;
            }

            _logger.LogInformation("Sending character list request...");
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                    PacketBuilder.BuildRequestCharacterListPacket(_connectionManager.Connection.Output));
                _logger.LogInformation("Character list request sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending character list request.");
            }
        }

        public async Task SendWarpCommandRequestAsync(ushort warpInfoIndex, uint commandKey = 0)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected — cannot send warp command request.");
                return;
            }

            _logger.LogInformation("Sending Warp Command Request for index {WarpInfoIndex}...", warpInfoIndex);
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                {
                    var packet = new WarpCommandRequest(_connectionManager.Connection.Output.GetMemory(WarpCommandRequest.Length).Slice(0, WarpCommandRequest.Length));
                    packet.CommandKey = commandKey;
                    packet.WarpInfoIndex = warpInfoIndex;
                    return WarpCommandRequest.Length;
                });
                _logger.LogInformation("Warp Command Request sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending Warp Command Request.");
            }
        }

        public async Task SendClientReadyAfterMapChangeAsync()
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected — cannot send ClientReadyAfterMapChange.");
                return;
            }

            _logger.LogInformation("Sending ClientReadyAfterMapChange packet...");
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                    PacketBuilder.BuildClientReadyAfterMapChangePacket(_connectionManager.Connection.Output));
                _logger.LogInformation("ClientReadyAfterMapChange packet sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending ClientReadyAfterMapChange packet.");
            }
        }

        /// <summary>
        /// Selects the specified character on the game server.
        /// </summary>
        public async Task SelectCharacterAsync(string characterName)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected — cannot select character.");
                return;
            }

            _logger.LogInformation("Selecting character '{CharacterName}'...", characterName);
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                    PacketBuilder.BuildSelectCharacterPacket(_connectionManager.Connection.Output, characterName));
                _logger.LogInformation("Character selection packet sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending character selection packet.");
            }
        }

        /// <summary>
        /// Sends an instant move (teleport) request to the given coordinates.
        /// </summary>
        public async Task SendInstantMoveRequestAsync(byte x, byte y)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected — cannot move instantly.");
                return;
            }

            _logger.LogInformation("Sending instant move to ({X}, {Y})...", x, y);
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                    PacketBuilder.BuildInstantMoveRequestPacket(_connectionManager.Connection.Output, x, y));
                _logger.LogInformation("Instant move request sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending instant move request.");
            }
        }

        /// <summary>
        /// Sends an animation request with the specified rotation and animation number.
        /// </summary>
        public async Task SendAnimationRequestAsync(byte rotation, byte animationNumber)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected — cannot send animation request.");
                return;
            }

            _logger.LogInformation(
                "Sending animation request (rotation={Rotation}, animation={AnimationNumber})...",
                rotation, animationNumber);

            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                    PacketBuilder.BuildAnimationRequestPacket(_connectionManager.Connection.Output, rotation, animationNumber));
                _logger.LogInformation("Animation request sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending animation request.");
            }
        }

        /// <summary>
        /// Sends a walk request along a path of direction steps.
        /// </summary>
        public async Task SendWalkRequestAsync(byte startX, byte startY, byte[] path)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected — cannot send walk request.");
                return;
            }
            if (path == null || path.Length == 0)
            {
                _logger.LogWarning("Empty path — walk request not sent.");
                return;
            }

            _logger.LogInformation(
                "Sending walk request from ({StartX}, {StartY}) with {Steps} steps...",
                startX, startY, path.Length);

            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                    PacketBuilder.BuildWalkRequestPacket(
                        _connectionManager.Connection.Output,
                        startX, startY, path));
                _logger.LogInformation("Walk request sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending walk request.");
            }
        }
    }
}
