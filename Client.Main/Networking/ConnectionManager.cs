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
        private SocketConnection? _socketConnection;    // Underlying socket connection
        private IConnection? _connection;               // Abstraction for network connection (potentially encrypted)
        private CancellationTokenSource? _receiveCts;   // Controls the receive loop for the CURRENT connection

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
            if (IsConnected) // Check if already connected using the property
            {
                _logger.LogWarning("🔌 Already connected. Disconnect first before connecting to {Host}:{Port}.", host, port);
                return false; // Do not attempt to connect if already connected
            }

            _logger.LogInformation("🔌 Attempting connection infrastructure setup for {Host}:{Port} (Encryption: {UseEncryption})...", host, port, useEncryption);

            // --- STEP 1: Ensure previous connection state is fully cleaned up ---
            await CleanupCurrentConnectionAsync();
            _logger.LogDebug("Pre-connection cleanup completed.");

            // --- STEP 2: Create NEW resources using LOCAL variables ---
            SocketConnection? newSocketConn = null;
            IConnection? newConnection = null;
            CancellationTokenSource? newReceiveCts = null;

            try
            {
                // Resolve host name to IP addresses and select the first IPv4 address
                var ipAddress = (await Dns.GetHostAddressesAsync(host, cancellationToken))
                    .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork); // Prefer IPv4
                if (ipAddress == null)
                {
                    _logger.LogError("❓ Failed to resolve IPv4 address for host: {Host}", host);
                    return false; // Abort if address cannot be resolved
                }
                var endPoint = new IPEndPoint(ipAddress, port);

                // Create new SocketConnection
                var pipeOptions = new PipeOptions(); // Create default PipeOptions
                newSocketConn = await SocketConnection.ConnectAsync(endPoint, pipeOptions);
                _logger.LogInformation("✔️ Socket connected to {EndPoint}. Socket HashCode: {HashCode}", endPoint, newSocketConn.GetHashCode());

                var connectionLogger = _loggerFactory.CreateLogger<Connection>(); // Logger for the MUnique Connection object
                IDuplexPipe transportPipe = newSocketConn; // Start with the raw socket pipe

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
                    // Use raw transport pipe for unencrypted connection
                    newConnection = new Connection(transportPipe, null, null, connectionLogger);
                    _logger.LogInformation("🔓 Raw (unencrypted) pipeline established for new connection. Connection HashCode: {HashCode}", newConnection.GetHashCode());
                }

                // Create a specific CancellationTokenSource for this connection's receive loop
                // but DO NOT start the loop here.
                newReceiveCts = new CancellationTokenSource();
                _logger.LogDebug("Created new CTS HashCode {CtsHash} for Connection HashCode {ConnHash}", newReceiveCts.GetHashCode(), newConnection.GetHashCode());


                // --- STEP 3: Assign NEW resources to class fields ONLY AFTER success ---
                _socketConnection = newSocketConn;
                _connection = newConnection;
                _receiveCts = newReceiveCts; // Assign the CTS that controls this specific connection

                _logger.LogInformation("✅ Connection infrastructure established, ready to start listening."); // Updated log
                return true; // Connection infrastructure setup successful

            }
            catch (SocketException ex)
            {
                _logger.LogError(ex, "❌ Socket error during connection to {Host}:{Port}: {ErrorCode}", host, port, ex.SocketErrorCode);
                // Cleanup partially created resources if connection failed
                await CleanupTemporaryResourcesAsync(newSocketConn, newConnection, newReceiveCts);
                return false;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("🚫 Connection attempt to {Host}:{Port} cancelled by external token.", host, port);
                await CleanupTemporaryResourcesAsync(newSocketConn, newConnection, newReceiveCts);
                return false;
            }
            catch (OperationCanceledException ex) // Catch cancellation from linkedCts potentially during BeginReceive setup
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
            // Capture current state to local variables for thread safety check
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
                // Link the connection's specific CTS with an optional external one
                // If external token is cancelled, the linked token will also be cancelled.
                var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(currentCts.Token, externalCancellationToken);

                _logger.LogDebug("Explicitly starting BeginReceiveAsync for Connection HashCode {HashCode} with linked CTS (Internal CTS HashCode: {CtsHash}).",
                    currentConnection.GetHashCode(), currentCts.GetHashCode());

                // Start the receive loop in the background using the linked token
                // The loop runs until the linked token is cancelled (either internally via _receiveCts or externally).
                _ = currentConnection.BeginReceiveAsync();

                _logger.LogInformation("👂 Started listening for packets on connection (Connection HashCode: {ConnHash}).", currentConnection.GetHashCode());
            }
            catch (ObjectDisposedException odEx) // Catch if the connection or CTS was disposed between check and use
            {
                _logger.LogError(odEx, "💥 Failed to start BeginReceiveAsync: ObjectDisposedException. Connection or CTS might have been disposed concurrently.");
                // Consider triggering cleanup again if this happens
                // Task.Run(async () => await CleanupCurrentConnectionAsync());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Failed to start BeginReceiveAsync for connection (Connection HashCode: {ConnHash}).", currentConnection?.GetHashCode() ?? 0);
                // Consider triggering cleanup if starting fails critically
                // Task.Run(async () => await CleanupCurrentConnectionAsync());
            }
        }


        /// <summary>
        /// Gracefully disconnects the current active network connection.
        /// </summary>
        /// <returns>A Task representing the asynchronous disconnect operation.</returns>
        public async Task DisconnectAsync()
        {
            var connectionToDisconnect = _connection;
            var socketToDisconnect = _socketConnection;
            var ctsToCancel = _receiveCts;

            if (connectionToDisconnect != null && connectionToDisconnect.Connected)
            {
                _logger.LogInformation("🔌 Disconnecting current connection (Connection HashCode: {ConnHash}, Socket HashCode: {SockHash})...",
                    connectionToDisconnect.GetHashCode(), socketToDisconnect?.GetHashCode() ?? 0);

                // 1. Anuluj CancellationTokenSource dla pętli odbierającej
                if (ctsToCancel != null && !ctsToCancel.IsCancellationRequested)
                {
                    _logger.LogDebug("Requesting cancellation of receive loop (CTS HashCode: {CtsHash})", ctsToCancel.GetHashCode());
                    try
                    {
                        ctsToCancel.Cancel();
                        // POCZEKAJ CHWILĘ, aby dać szansę pętli BeginReceiveAsync na reakcję na anulowanie
                        // Zwiększmy ten czas na próbę
                        await Task.Delay(50); // Zwiększone z Yield do 50ms
                    }
                    catch (ObjectDisposedException) { _logger.LogWarning("CTS (HashCode: {CtsHash}) was already disposed before cancel.", ctsToCancel.GetHashCode()); }
                    catch (Exception ex) { _logger.LogError(ex, "Error cancelling CTS (HashCode: {CtsHash})", ctsToCancel.GetHashCode()); }
                }

                // 2. Wywołaj DisconnectAsync na IConnection - to powinno zakończyć potoki
                try
                {
                    await connectionToDisconnect.DisconnectAsync();
                    _logger.LogInformation("✔️ Connection.DisconnectAsync() completed for Connection HashCode: {ConnHash}.", connectionToDisconnect.GetHashCode());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "💥 Error during connection.DisconnectAsync() for Connection HashCode: {ConnHash}. Proceeding with cleanup.", connectionToDisconnect.GetHashCode());
                }

                // 3. Poczekaj jeszcze chwilę po DisconnectAsync, zanim zaczniesz sprzątać resztę
                await Task.Delay(50); // Dodatkowa pauza
            }
            else
            {
                _logger.LogInformation("🔌 No active connection found to disconnect, or already disconnected.");
            }

            // 4. Zawsze wykonaj pełne sprzątanie zasobów
            await CleanupCurrentConnectionAsync();
        }

        /// <summary>
        /// Cleans up resources associated with the CURRENT connection fields (_connection, _socketConnection, _receiveCts).
        /// This should be called after disconnection or before attempting a new connection.
        /// Sets fields to null to prevent reuse of disposed objects.
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

            // Anuluj CTS (jeśli jeszcze nie anulowano) i ZAWSZE Dispose
            if (ctsToClean != null)
            {
                if (!ctsToClean.IsCancellationRequested)
                {
                    try { ctsToClean.Cancel(); } catch { /* Ignore */ }
                }
                try
                {
                    _logger.LogTrace("Cleanup: Disposing CTS HashCode: {CtsHash}", ctsToClean.GetHashCode());
                    ctsToClean.Dispose();
                }
                catch (ObjectDisposedException) { }
                catch (Exception ex) { _logger.LogWarning(ex, "Cleanup: Error disposing CTS HashCode: {CtsHash}", ctsToClean.GetHashCode()); }
            }

            // Dispose IConnection
            if (connectionToClean != null)
            {
                // Najpierw spróbuj DisconnectAsync, jeśli jeszcze nie było wywołane
                // (Chociaż powinno być wywołane w DisconnectAsync powyżej)
                // Można to pominąć, jeśli jesteśmy pewni, że DisconnectAsync zawsze jest wołane przed Cleanup
                /*
                if (connectionToClean.Connected) {
                    try {
                        _logger.LogTrace("Cleanup: Calling DisconnectAsync again for Connection HashCode: {ConnHash}", connectionToClean.GetHashCode());
                        await connectionToClean.DisconnectAsync();
                    } catch (Exception ex) {
                        _logger.LogWarning(ex, "Cleanup: Error during secondary DisconnectAsync for Connection HashCode: {ConnHash}", connectionToClean.GetHashCode());
                    }
                }
                */

                _logger.LogTrace("Cleanup: Disposing Connection HashCode: {ConnHash}", connectionToClean.GetHashCode());
                if (connectionToClean is IAsyncDisposable asyncDisposableConnection)
                {
                    try { await asyncDisposableConnection.DisposeAsync(); } catch (ObjectDisposedException) { } catch (Exception ex) { _logger.LogWarning(ex, "Cleanup: Error during IAsyncDisposable Connection cleanup HashCode: {ConnHash}", connectionToClean.GetHashCode()); }
                }
                else if (connectionToClean is IDisposable disposableConnection)
                {
                    try { disposableConnection.Dispose(); } catch (ObjectDisposedException) { } catch (Exception ex) { _logger.LogWarning(ex, "Cleanup: Error during IDisposable Connection cleanup HashCode: {ConnHash}", connectionToClean.GetHashCode()); }
                }
            }

            // Dispose SocketConnection
            if (socketToClean != null)
            {
                _logger.LogTrace("Cleanup: Disposing SocketConnection HashCode: {SockHash}", socketToClean.GetHashCode());
                try
                {
                    // Można spróbować zamknąć socket przed Dispose, chociaż Dispose powinno to robić
                    // socketToClean.Shutdown(SocketShutdown.Both);
                    socketToClean.Dispose();
                }
                catch (ObjectDisposedException) { }
                catch (Exception ex) { _logger.LogWarning(ex, "Cleanup: Error during SocketConnection cleanup HashCode: {SockHash}", socketToClean.GetHashCode()); }
            }

            _logger.LogDebug("CleanupCurrentConnectionAsync finished.");
        }

        /// <summary>
        /// Helper method to clean up resources created temporarily during a failed ConnectAsync attempt.
        /// </summary>
        private async Task CleanupTemporaryResourcesAsync(SocketConnection? socketConn, IConnection? conn, CancellationTokenSource? cts)
        {
            _logger.LogWarning("Cleaning up temporary resources from failed connection attempt...");
            // Order: CTS, IConnection (pipes), SocketConnection (socket)
            if (cts != null) { if (!cts.IsCancellationRequested) { try { cts.Cancel(); } catch { /* Ignore */ } } try { cts.Dispose(); } catch { /* Ignore */ } }
            if (conn != null) { if (conn is IAsyncDisposable ad) { try { await ad.DisposeAsync(); } catch { } } else if (conn is IDisposable d) { try { d.Dispose(); } catch { } } }
            if (socketConn != null) { try { socketConn.Dispose(); } catch { /* Ignore */ } }
            _logger.LogWarning("Temporary resource cleanup finished.");
        }


        /// <summary>
        /// Asynchronously disposes of the ConnectionManager, ensuring all resources are released.
        /// </summary>
        /// <returns>A ValueTask representing the completion of the disposal.</returns>
        public async ValueTask DisposeAsync()
        {
            _logger.LogInformation("🧹 Disposing ConnectionManager...");
            // Ensure disconnect and cleanup is performed on the *current* state fields.
            await DisconnectAsync();
            _logger.LogInformation("✔️ ConnectionManager disposed.");
            GC.SuppressFinalize(this); // Prevent finalizer from running after manual disposal
        }
    }
}