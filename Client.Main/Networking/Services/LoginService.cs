using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Network;
using Client.Main.Networking.PacketHandling;
using System.Threading.Tasks;
using System;

namespace Client.Main.Networking.Services
{
    /// <summary>
    /// Handles login requests: encrypts credentials and sends the login packet
    /// including client version and serial information.
    /// </summary>
    public class LoginService
    {
        // ───────────────────────── Fields ─────────────────────────

        private readonly ConnectionManager _connectionManager;
        private readonly ILogger<LoginService> _logger;
        private readonly byte[] _clientVersion;
        private readonly byte[] _clientSerial;
        private readonly byte[] _xor3Keys;

        // ─────────────────────── Constructor ───────────────────────

        public LoginService(
            ConnectionManager connectionManager,
            ILogger<LoginService> logger,
            byte[] clientVersion,
            byte[] clientSerial,
            byte[] xor3Keys)
        {
            _connectionManager = connectionManager;
            _logger = logger;
            _clientVersion = clientVersion;
            _clientSerial = clientSerial;
            _xor3Keys = xor3Keys;
        }

        // ──────────────────────── Public API ───────────────────────

        /// <summary>
        /// Sends a login request using the specified username and password.
        /// Credentials are encrypted with XOR3 before sending.
        /// </summary>
        /// <param name="username">The account username.</param>
        /// <param name="password">The account password.</param>
        public async Task SendLoginRequestAsync(string username, string password)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("No connection – cannot send login packet.");
                return;
            }

            _logger.LogInformation("Sending login packet for user '{Username}'...", username);
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                    PacketBuilder.BuildLoginPacket(
                        _connectionManager.Connection.Output,
                        username,
                        password,
                        _clientVersion,
                        _clientSerial,
                        _xor3Keys));
                _logger.LogInformation("Login packet sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while sending login packet.");
            }
        }
    }
}
