using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Network;
using Client.Main.Networking.PacketHandling;
using System.Threading.Tasks;
using System; // For PacketBuilder

namespace Client.Main.Networking.Services
{
    /// <summary>
    ///  Service class responsible for handling login-related operations and sending login packets to the game server.
    ///  This service encapsulates the logic for preparing and sending login requests, including encryption and version/serial information.
    /// </summary>
    public class LoginService
    {
        private readonly ConnectionManager _connectionManager; // Manages the network connection
        private readonly ILogger<LoginService> _logger; // Logger for this service
        private readonly byte[] _clientVersion; // Byte array representing the client version
        private readonly byte[] _clientSerial; // Byte array representing the client serial
        private readonly byte[] _xor3Keys; // XOR3 encryption keys for password encryption

        /// <summary>
        /// Initializes a new instance of the <see cref="LoginService"/> class.
        /// </summary>
        /// <param name="connectionManager">The connection manager instance to use for sending packets.</param>
        /// <param name="logger">The logger instance for logging service operations and errors.</param>
        /// <param name="clientVersion">Byte array representing the client version.</param>
        /// <param name="clientSerial">Byte array representing the client serial.</param>
        /// <param name="xor3Keys">XOR3 encryption keys for password encryption.</param>
        public LoginService(ConnectionManager connectionManager, ILogger<LoginService> logger, byte[] clientVersion, byte[] clientSerial, byte[] xor3Keys)
        {
            _connectionManager = connectionManager;
            _logger = logger;
            _clientVersion = clientVersion;
            _clientSerial = clientSerial;
            _xor3Keys = xor3Keys;
        }

        /// <summary>
        /// Sends a login request to the game server with the provided username and password.
        /// Encrypts the username and password before sending using XOR3 encryption.
        /// Includes client version and serial information in the login packet.
        /// </summary>
        /// <param name="username">The username for login.</param>
        /// <param name="password">The password for login.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        public async Task SendLoginRequestAsync(string username, string password) // Ta metoda już przyjmuje argumenty
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("🔒 No connection – cannot send login packet.");
                return;
            }

            _logger.LogInformation("🔑 Sending login packet for user '{Username}'...", username); // Loguj przekazany username
            try
            {
                // Użyj przekazanych username i password
                await _connectionManager.Connection.SendAsync(() =>
                    PacketBuilder.BuildLoginPacket(_connectionManager.Connection.Output, username, password, _clientVersion, _clientSerial, _xor3Keys)
                );
                _logger.LogInformation("✔️ Login packet sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error while sending login packet.");
            }
        }
    }
}