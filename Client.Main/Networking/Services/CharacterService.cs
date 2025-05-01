using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Network;
using Client.Main.Networking.PacketHandling;
using System.Threading.Tasks;
using System; // For PacketBuilder

namespace Client.Main.Networking.Services
{
    /// <summary>
    ///  Service class responsible for handling and sending character-related network packets to the game server.
    ///  This includes requests for character lists, character selection, movement commands, and animation requests.
    /// </summary>
    public class CharacterService
    {
        private readonly ConnectionManager _connectionManager; // Manages the network connection
        private readonly ILogger<CharacterService> _logger; // Logger for this service

        /// <summary>
        /// Initializes a new instance of the <see cref="CharacterService"/> class.
        /// </summary>
        /// <param name="connectionManager">The connection manager instance to use for sending packets.</param>
        /// <param name="logger">The logger instance for logging service operations and errors.</param>
        public CharacterService(ConnectionManager connectionManager, ILogger<CharacterService> logger)
        {
            _connectionManager = connectionManager;
            _logger = logger;
        }

        /// <summary>
        /// Sends a request to the server to retrieve the list of characters for the logged-in account.
        /// </summary>
        /// <returns>A Task representing the asynchronous operation.</returns>
        public async Task RequestCharacterListAsync()
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("🔒 No connection – cannot send character list request.");
                return; // Exit if not connected
            }

            _logger.LogInformation("📜 Sending RequestCharacterList packet...");
            try
            {
                // Use PacketBuilder to create and send the RequestCharacterList packet
                await _connectionManager.Connection.SendAsync(() =>
                    PacketBuilder.BuildRequestCharacterListPacket(_connectionManager.Connection.Output)
                );
                _logger.LogInformation("✔️ RequestCharacterList packet sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error while sending RequestCharacterList packet.");
            }
        }

        /// <summary>
        /// Sends a request to the server to select a character with the given name.
        /// </summary>
        /// <param name="characterName">The name of the character to select.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        public async Task SelectCharacterAsync(string characterName)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("🔒 No connection – cannot send character selection request.");
                return; // Exit if not connected
            }

            _logger.LogInformation("👤 Sending SelectCharacter packet for character '{CharacterName}'...", characterName);
            try
            {
                // Use PacketBuilder to create and send the SelectCharacter packet
                await _connectionManager.Connection.SendAsync(() =>
                    PacketBuilder.BuildSelectCharacterPacket(_connectionManager.Connection.Output, characterName)
                );
                _logger.LogInformation("✔️ SelectCharacter packet sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error while sending SelectCharacter packet.");
            }
        }

        /// <summary>
        /// Sends a request to the server for an instant move to the specified coordinates.
        /// </summary>
        /// <param name="x">The target X-coordinate.</param>
        /// <param name="y">The target Y-coordinate.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        public async Task SendInstantMoveRequestAsync(byte x, byte y)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("🔒 No connection – cannot send move request.");
                return; // Exit if not connected
            }
            _logger.LogInformation("🏃 Sending InstantMove packet to ({X},{Y})...", x, y);
            try
            {
                // Use PacketBuilder to create and send the InstantMoveRequest packet
                await _connectionManager.Connection.SendAsync(() =>
                    PacketBuilder.BuildInstantMoveRequestPacket(_connectionManager.Connection.Output, x, y)
                );
                _logger.LogInformation("✔️ InstantMove packet sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error while sending InstantMove packet.");
            }
        }

        /// <summary>
        /// Sends a request to the server to play an animation.
        /// </summary>
        /// <param name="rotation">The rotation direction for the animation.</param>
        /// <param name="animationNumber">The animation number to play.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        public async Task SendAnimationRequestAsync(byte rotation, byte animationNumber)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("🔒 No connection – cannot send animation request.");
                return; // Exit if not connected
            }
            _logger.LogInformation("🔄 Sending AnimationRequest packet (Rot: {Rot}, Anim: {Anim})...", rotation, animationNumber);
            try
            {
                // Use PacketBuilder to create and send the AnimationRequest packet
                await _connectionManager.Connection.SendAsync(() =>
                    PacketBuilder.BuildAnimationRequestPacket(_connectionManager.Connection.Output, rotation, animationNumber)
                );
                _logger.LogInformation("✔️ AnimationRequest packet sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error while sending AnimationRequest packet.");
            }
        }

        /// <summary>
        /// Sends a request to the server to initiate a walk action with a given path.
        /// </summary>
        /// <param name="startX">The starting X-coordinate of the walk.</param>
        /// <param name="startY">The starting Y-coordinate of the walk.</param>
        /// <param name="path">An array of bytes representing the path directions.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        public async Task SendWalkRequestAsync(byte startX, byte startY, byte[] path)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("🔒 No connection – cannot send walk request.");
                return; // Exit if not connected
            }

            if (path == null || path.Length == 0)
            {
                _logger.LogWarning("🚶 Empty path – walk request not sent.");
                return; // Do not send walk request if path is empty
            }

            _logger.LogInformation("🚶 Sending WalkRequest packet with start ({StartX},{StartY}), {Steps} steps...", startX, startY, path.Length);
            try
            {
                // Use PacketBuilder to create and send the WalkRequest packet
                await _connectionManager.Connection.SendAsync(() =>
                    PacketBuilder.BuildWalkRequestPacket(_connectionManager.Connection.Output, startX, startY, path)
                );
                _logger.LogInformation("✔️ WalkRequest packet sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error while sending WalkRequest packet.");
            }
        }
    }
}