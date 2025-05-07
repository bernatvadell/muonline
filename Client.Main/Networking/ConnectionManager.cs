using System;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.Network.SimpleModulus;
using MUnique.OpenMU.Network.Xor;
using Pipelines.Sockets.Unofficial; // Requires the Pipelines.Sockets.Unofficial NuGet package

namespace Client.Main.Networking
{
    /// <summary>
    /// Manages TCP network connections, including establishing, maintaining, and disconnecting connections.
    /// Handles encryption and decryption pipelines using SimpleModulus and Xor32 algorithms.
    /// Supports sequential connections to different endpoints (e.g., Connect Server then Game Server).
    /// Implements IAsyncDisposable for proper resource management.
    /// </summary>
    public class ConnectionManager : IAsyncDisposable
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<ConnectionManager> _logger;
        private readonly SimpleModulusKeys _encryptKeys; // Encryption keys for SimpleModulus
        private readonly SimpleModulusKeys _decryptKeys; // Decryption keys for SimpleModulus

        // --- State Fields ---
        // These are volatile during the connect/disconnect cycle
        private SocketConnection _socketConnection;    // Underlying socket connection
        private IConnection _connection;               // Abstraction for network connection (potentially encrypted)
        private CancellationTokenSource _receiveCts;   // Controls the receive loop for the CURRENT connection

        /// <summary>
        /// Gets the current network connection. Throws an exception if the connection is not initialized or has been disconnected.
        /// </summary>
        public IConnection Connection => _connection ?? throw new InvalidOperationException("Connection has not been initialized or is disconnected.");

        /// <summary>
        /// Gets a value indicating whether the current network connection is established and active.
        /// Checks both the connection object and its underlying Connected property.
        /// </summary>
        public bool IsConnected => _connection?.Connected ?? false;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionManager"/> class.
        /// </summary>
        /// <param name="loggerFactory">The logger factory used to create loggers.</param>
        /// <param name="encryptKeys">The SimpleModulus keys for encryption.</param>
        /// <param name="decryptKeys">The SimpleModulus keys for decryption.</param>
        public ConnectionManager(ILoggerFactory loggerFactory, SimpleModulusKeys encryptKeys, SimpleModulusKeys decryptKeys)
        {
            _loggerFactory = loggerFactory;
            _logger = _loggerFactory.CreateLogger<ConnectionManager>();
            _encryptKeys = encryptKeys;
            _decryptKeys = decryptKeys;
            _logger.LogDebug("ConnectionManager created.");
        }

        /// <summary>
        /// Establishes a TCP connection to the specified host and port.
        /// Configures the packet processing pipeline, including optional encryption.
        /// Creates connection resources but DOES NOT start the receiving loop automatically.
        /// Uses local variables for new resources and assigns them to class fields only upon full success.
        /// </summary>
        /// <param name="host">The host name or IP address of the server.</param>
        /// <param name="port">The port number of the server.</param>
        /// <param name="useEncryption">True to enable encryption (SimpleModulus and Xor32), false for a raw, unencrypted connection.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the connection attempt.</param>
        /// <returns>True if the connection infrastructure was successfully established; otherwise, false.</returns>
        public async Task<bool> ConnectAsync(string host, int port, bool useEncryption, CancellationToken cancellationToken = default)
        {
            if (IsConnected)
            {
                _logger.LogWarning("🔌 Already connected. Disconnect first before connecting to {Host}:{Port}.", host, port);
                return false;
            }

            _logger.LogInformation("🔌 Attempting connection infrastructure setup for {Host}:{Port} (Encryption: {UseEncryption})...", host, port, useEncryption);

            // --- STEP 1: Ensure previous connection state is fully cleaned up ---
            await CleanupCurrentConnectionAsync();
            _logger.LogDebug("Pre-connection cleanup completed.");

            // --- STEP 2: Create NEW resources using LOCAL variables ---
            SocketConnection newSocketConn = null;
            IConnection newConnection = null;
            CancellationTokenSource newReceiveCts = null;

            try
            {
                // Resolve host name to IP addresses and select the first IPv4 address
                var ipAddress = (await Dns.GetHostAddressesAsync(host, cancellationToken))
                    .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
                if (ipAddress == null)
                {
                    _logger.LogError("❓ Failed to resolve IPv4 address for host: {Host}", host);
                    return false;
                }
                var endPoint = new IPEndPoint(ipAddress, port);

                // Create new SocketConnection
                var pipeOptions = new PipeOptions();
                newSocketConn = await SocketConnection.ConnectAsync(endPoint, pipeOptions);
                _logger.LogInformation("✔️ Socket connected to {EndPoint}. Socket HashCode: {HashCode}", endPoint, newSocketConn.GetHashCode());

                var connectionLogger = _loggerFactory.CreateLogger<Connection>();
                IDuplexPipe transportPipe = newSocketConn;

                // Setup encryption pipeline if requested
                if (useEncryption)
                {
                    var decryptor = new PipelinedSimpleModulusDecryptor(transportPipe.Input, _decryptKeys);
                    var simpleModulusEncryptor = new PipelinedSimpleModulusEncryptor(transportPipe.Output, _encryptKeys);
                    var xor32Encryptor = new PipelinedXor32Encryptor(simpleModulusEncryptor.Writer);
                    newConnection = new Connection(transportPipe, decryptor, xor32Encryptor, connectionLogger);
                    _logger.LogInformation("🔒 Encryption pipeline established for new connection. Connection HashCode: {HashCode}", newConnection.GetHashCode());
                }
                else
                {
                    newConnection = new Connection(transportPipe, null, null, connectionLogger);
                    _logger.LogInformation("🔓 Raw (unencrypted) pipeline established for new connection. Connection HashCode: {HashCode}", newConnection.GetHashCode());
                }

                // Create a specific CancellationTokenSource for this connection's receive loop but DO NOT start the loop here.
                newReceiveCts = new CancellationTokenSource();
                _logger.LogDebug("Created new CTS HashCode {CtsHash} for Connection HashCode {ConnHash}", newReceiveCts.GetHashCode(), newConnection.GetHashCode());

                // --- STEP 3: Assign NEW resources to class fields ONLY AFTER success ---
                _socketConnection = newSocketConn;
                _connection = newConnection;
                _receiveCts = newReceiveCts;

                _logger.LogInformation("✅ Connection infrastructure established, ready to start listening.");
                return true;
            }
            catch (SocketException ex)
            {
                _logger.LogError(ex, "❌ Socket error during connection to {Host}:{Port}: {ErrorCode}", host, port, ex.SocketErrorCode);
                await CleanupTemporaryResourcesAsync(newSocketConn, newConnection, newReceiveCts);
                return false;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("🚫 Connection attempt to {Host}:{Port} cancelled by external token.", host, port);
                await CleanupTemporaryResourcesAsync(newSocketConn, newConnection, newReceiveCts);
                return false;
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogWarning(ex, "🚫 Operation cancelled during connection setup for {Host}:{Port}.", host, port);
                await CleanupTemporaryResourcesAsync(newSocketConn, newConnection, newReceiveCts);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Unexpected error while connecting to {Host}:{Port}.", host, port);
                await CleanupTemporaryResourcesAsync(newSocketConn, newConnection, newReceiveCts);
                return false;
            }
        }

        /// <summary>
        /// Starts the background packet receiving loop for the established connection.
        /// Should only be called after ConnectAsync returns true.
        /// </summary>
        /// <param name="externalCancellationToken">Optional external cancellation token to link with the internal CTS.</param>
        public void StartReceiving(CancellationToken externalCancellationToken = default)
        {
            var currentConnection = _connection;
            var currentCts = _receiveCts;

            if (currentConnection == null || currentCts == null || currentCts.IsCancellationRequested)
            {
                _logger.LogError("Cannot start receiving: Connection (IsNull={ConnNull}) or CTS (IsNull={CtsNull}, IsCancelled={CtsCancelled}) is not valid.",
                    currentConnection == null, currentCts == null, currentCts?.IsCancellationRequested);
                return;
            }

            try
            {
                var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(currentCts.Token, externalCancellationToken);
                _logger.LogDebug("Explicitly starting BeginReceiveAsync for Connection HashCode {HashCode} with linked CTS (Internal CTS HashCode: {CtsHash}).",
                    currentConnection.GetHashCode(), currentCts.GetHashCode());
                _ = currentConnection.BeginReceiveAsync();
                _logger.LogInformation("👂 Started listening for packets on connection (Connection HashCode: {ConnHash}).", currentConnection.GetHashCode());
            }
            catch (ObjectDisposedException odEx)
            {
                _logger.LogError(odEx, "💥 Failed to start BeginReceiveAsync: ObjectDisposedException. Connection or CTS might have been disposed concurrently.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Failed to start BeginReceiveAsync for connection (Connection HashCode: {ConnHash}).", currentConnection?.GetHashCode() ?? 0);
            }
        }

        /// <summary>
        /// Gracefully disconnects the current active network connection.
        /// </summary>
        public async Task DisconnectAsync()
        {
            var connectionToDisconnect = _connection;
            var socketToDisconnect = _socketConnection;
            var ctsToCancel = _receiveCts;

            if (connectionToDisconnect != null && connectionToDisconnect.Connected)
            {
                _logger.LogInformation("🔌 Disconnecting current connection (Connection HashCode: {ConnHash}, Socket HashCode: {SockHash})...",
                    connectionToDisconnect.GetHashCode(), socketToDisconnect?.GetHashCode() ?? 0);

                if (ctsToCancel != null && !ctsToCancel.IsCancellationRequested)
                {
                    _logger.LogDebug("Requesting cancellation of receive loop (CTS HashCode: {CtsHash})", ctsToCancel.GetHashCode());
                    try
                    {
                        ctsToCancel.Cancel();
                        await Task.Delay(50);
                    }
                    catch (ObjectDisposedException)
                    {
                        _logger.LogWarning("CTS (HashCode: {CtsHash}) was already disposed before cancel.", ctsToCancel.GetHashCode());
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error cancelling CTS (HashCode: {CtsHash})", ctsToCancel.GetHashCode());
                    }
                }

                try
                {
                    await connectionToDisconnect.DisconnectAsync();
                    _logger.LogInformation("✔️ Connection.DisconnectAsync() completed for Connection HashCode: {ConnHash}.", connectionToDisconnect.GetHashCode());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "💥 Error during connection.DisconnectAsync() for Connection HashCode: {ConnHash}. Proceeding with cleanup.", connectionToDisconnect.GetHashCode());
                }

                await Task.Delay(50);
            }
            else
            {
                _logger.LogInformation("🔌 No active connection found to disconnect, or already disconnected.");
            }

            await CleanupCurrentConnectionAsync();
        }

        /// <summary>
        /// Cleans up resources associated with the CURRENT connection fields.
        /// </summary>
        private async Task CleanupCurrentConnectionAsync()
        {
            _logger.LogDebug("CleanupCurrentConnectionAsync started...");

            var connectionToClean = _connection;
            var socketToClean = _socketConnection;
            var ctsToClean = _receiveCts;

            _connection = null;
            _socketConnection = null;
            _receiveCts = null;

            if (ctsToClean != null)
            {
                if (!ctsToClean.IsCancellationRequested)
                {
                    try { ctsToClean.Cancel(); } catch { }
                }
                try
                {
                    _logger.LogTrace("Cleanup: Disposing CTS HashCode: {CtsHash}", ctsToClean.GetHashCode());
                    ctsToClean.Dispose();
                }
                catch { }
            }

            if (connectionToClean != null)
            {
                _logger.LogTrace("Cleanup: Disposing Connection HashCode: {ConnHash}", connectionToClean.GetHashCode());
                if (connectionToClean is IAsyncDisposable asyncDisposableConnection)
                {
                    try { await asyncDisposableConnection.DisposeAsync(); } catch { }
                }
                else if (connectionToClean is IDisposable disposableConnection)
                {
                    try { disposableConnection.Dispose(); } catch { }
                }
            }

            if (socketToClean != null)
            {
                _logger.LogTrace("Cleanup: Disposing SocketConnection HashCode: {SockHash}", socketToClean.GetHashCode());
                try { socketToClean.Dispose(); } catch { }
            }

            _logger.LogDebug("CleanupCurrentConnectionAsync finished.");
        }

        /// <summary>
        /// Helper method to clean up resources created temporarily during a failed ConnectAsync attempt.
        /// </summary>
        private async Task CleanupTemporaryResourcesAsync(SocketConnection socketConn, IConnection conn, CancellationTokenSource cts)
        {
            _logger.LogWarning("Cleaning up temporary resources from failed connection attempt...");
            if (cts != null)
            {
                if (!cts.IsCancellationRequested) { try { cts.Cancel(); } catch { } }
                try { cts.Dispose(); } catch { }
            }
            if (conn != null)
            {
                if (conn is IAsyncDisposable ad) { try { await ad.DisposeAsync(); } catch { } }
                else if (conn is IDisposable d) { try { d.Dispose(); } catch { } }
            }
            if (socketConn != null) { try { socketConn.Dispose(); } catch { } }
            _logger.LogWarning("Temporary resource cleanup finished.");
        }

        /// <summary>
        /// Asynchronously disposes of the ConnectionManager, ensuring all resources are released.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            _logger.LogInformation("🧹 Disposing ConnectionManager...");
            await DisconnectAsync();
            _logger.LogInformation("✔️ ConnectionManager disposed.");
            GC.SuppressFinalize(this);
        }
    }
}
