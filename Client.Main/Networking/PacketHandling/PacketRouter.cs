using System.Buffers;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.Network.Packets;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using MUnique.OpenMU.Network.Packets.ConnectServer;
using Client.Main.Client; // For SimpleLoginClient, CharacterState, ScopeManager, TargetProtocolVersion
using Client.Main.Configuration; // For MuOnlineSettings
using Client.Main.Core.Models; // For ServerInfo
using Client.Main.Core.Utilities; // For PacketHandlerAttribute, SubCodeHolder, ItemDatabase
using Client.Main.Networking.Services; // For CharacterService, LoginService
using Client.Main.Networking.PacketHandling.Handlers;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using System.Linq; // For the handler classes

namespace Client.Main.Networking.PacketHandling
{
    /// <summary>
    /// Routes incoming network packets to their respective handlers based on whether the current connection is to a Connect Server or Game Server.
    /// Utilizes a dictionary for efficient lookup of packet handlers and supports attribute-based handler registration.
    /// </summary>
    public class PacketRouter
    {
        /// <summary>
        /// Constant representing no sub-code for packet handlers that do not use sub-codes.
        /// </summary>
        public const byte NoSubCode = 0xFF;

        private readonly ILogger<PacketRouter> _logger;
        private readonly NetworkManager _networkManager; // Client instance for state management and UI interactions
        private readonly MuOnlineSettings _settings; // Application settings for packet logging and filtering

        // Handler instances for different packet types, injected through constructor
        private readonly CharacterDataHandler _characterDataHandler;
        private readonly InventoryHandler _inventoryHandler;
        private readonly ScopeHandler _scopeHandler;
        private readonly ChatMessageHandler _chatMessageHandler;
        private readonly ConnectServerHandler _connectServerHandler;
        private readonly MiscGamePacketHandler _miscGamePacketHandler;

        /// <summary>
        /// Gets the target protocol version for packet handling, set during initialization.
        /// </summary>
        public TargetProtocolVersion TargetVersion { get; }

        // Dictionary to store registered packet handlers. Key is a tuple of (MainCode, SubCode), Value is the handler delegate.
        private readonly Dictionary<(byte MainCode, byte SubCode), Func<Memory<byte>, Task>> _packetHandlers = new();
        private bool _isConnectServerRouting = false; // Flag to indicate if currently routing Connect Server packets

        /// <summary>
        /// /// Initializes a new instance of the <see cref="PacketRouter"/> class, registering packet handlers and setting up dependencies.
        /// </summary>
        /// <param name="loggerFactory">The logger factory for creating loggers.</param>
        /// <param name="characterService">The character service for character-related operations.</param>
        /// <param name="loginService">The login service for authentication and login processes.</param>
        /// <param name="targetVersion">The target protocol version of the client.</param>
        /// <param name="client">The SimpleLoginClient instance for client state management.</param>
        /// <param name="characterState">The character state instance for accessing and modifying character information.</param>
        /// <param name="scopeManager">The scope manager instance for managing objects in the player's scope.</param>
        /// <param name="settings">The application settings for packet logging and filtering.</param>
        public PacketRouter(
            ILoggerFactory loggerFactory, // Use ILoggerFactory for dependency injection
            CharacterService characterService,
            LoginService loginService,
            TargetProtocolVersion targetVersion,
            NetworkManager networkManager,
            CharacterState characterState,
            ScopeManager scopeManager,
            MuOnlineSettings settings)
        {
            _logger = loggerFactory.CreateLogger<PacketRouter>();
            TargetVersion = targetVersion;
            _networkManager = networkManager;
            _settings = settings;

            // Initialize packet handlers, injecting necessary services and state managers
            _characterDataHandler = new CharacterDataHandler(loggerFactory, characterState, networkManager, targetVersion);
            _inventoryHandler = new InventoryHandler(loggerFactory, characterState, networkManager, targetVersion);
            _scopeHandler = new ScopeHandler(loggerFactory, scopeManager, characterState, networkManager, targetVersion);
            _chatMessageHandler = new ChatMessageHandler(loggerFactory);
            _connectServerHandler = new ConnectServerHandler(loggerFactory, networkManager);
            _miscGamePacketHandler = new MiscGamePacketHandler(loggerFactory, networkManager, characterService, targetVersion);

            RegisterAttributeBasedHandlers(); // Automatically register handlers using attributes
            RegisterConnectServerHandlers(); // Manually register Connect Server handlers
        }

        /// <summary>
        /// Sets the packet routing mode to either Connect Server or Game Server.
        /// </summary>
        /// <param name="isConnectServer">True to set routing mode to Connect Server, false for Game Server.</param>
        public void SetRoutingMode(bool isConnectServer)
        {
            _isConnectServerRouting = isConnectServer;
            _logger.LogInformation("🔄 Packet routing mode set to: {Mode}", isConnectServer ? "Connect Server" : "Game Server");
        }

        /// <summary>
        /// Routes the incoming packet data based on the current routing mode (Connect Server or Game Server).
        /// </summary>
        /// <param name="sequence">The read-only sequence of bytes representing the incoming packet.</param>
        /// <returns>A Task representing the asynchronous packet routing operation.</returns>
        public Task RoutePacketAsync(ReadOnlySequence<byte> sequence)
        {
            var packetMemory = sequence.ToArray(); // Convert ReadOnlySequence to Memory<byte> for easier processing
            _logger.LogDebug("📬 Received packet ({Length} bytes): {Data}", packetMemory.Length, Convert.ToHexString(packetMemory));

            if (_isConnectServerRouting)
            {
                return RouteConnectServerPacketAsync(packetMemory); // Route as Connect Server packet
            }
            else
            {
                return RouteGameServerPacketAsync(packetMemory); // Route as Game Server packet
            }
        }

        /// <summary>
        /// Routes a packet specifically for Game Server processing.
        /// </summary>
        /// <param name="packet">The memory buffer containing the packet data.</param>
        /// <returns>A Task representing the asynchronous packet routing operation.</returns>
        private Task RouteGameServerPacketAsync(Memory<byte> packet)
        {
            // Parse Game Server packet header to extract header type, main code, and sub-code
            if (!TryParseGameServerHeader(packet.Span, out byte headerType, out byte code, out byte? subCode))
            {
                _logger.LogWarning("❓ Failed to parse Game Server packet header: {Data}", Convert.ToHexString(packet.Span));
                return Task.CompletedTask;
            }

            _logger.LogDebug("🔎 Parsing GS Packet: Header={HeaderType:X2}, Code={Code:X2}, SubCode={SubCode}",
                headerType, code, subCode.HasValue ? subCode.Value.ToString("X2") : "N/A");

            return DispatchPacketInternalAsync(packet, code, subCode, headerType); // Dispatch packet to handler
        }

        /// <summary>
        /// Routes a packet specifically for Connect Server processing.
        /// </summary>
        /// <param name="packet">The memory buffer containing the packet data.</param>
        /// <returns>A Task representing the asynchronous packet routing operation.</returns>
        private Task RouteConnectServerPacketAsync(Memory<byte> packet)
        {
            // Parse Connect Server packet header to extract header type, main code, and sub-code
            if (!TryParseConnectServerHeader(packet.Span, out byte headerType, out byte code, out byte? subCode))
            {
                _logger.LogWarning("❓ Failed to parse Connect Server packet header: {Data}", Convert.ToHexString(packet.Span));
                return Task.CompletedTask;
            }

            _logger.LogDebug("🔎 Parsing CS Packet: Header={HeaderType:X2}, Code={Code:X2}, SubCode={SubCode}",
                            headerType, code, subCode.HasValue ? subCode.Value.ToString("X2") : "N/A");

            return DispatchPacketInternalAsync(packet, code, subCode, headerType); // Dispatch packet to handler
        }

        /// <summary>
        /// Dispatches the packet to the appropriate handler based on the extracted code and sub-code.
        /// </summary>
        /// <param name="packet">The memory buffer containing the packet data.</param>
        /// <param name="code">The main code of the packet.</param>
        /// <param name="subCode">The sub-code of the packet (nullable).</param>
        /// <param name="headerType">The header type of the packet.</param>
        /// <returns>A Task representing the asynchronous packet dispatch operation.</returns>
        private Task DispatchPacketInternalAsync(Memory<byte> packet, byte code, byte? subCode, byte headerType)
        {
            byte lookupSubCode = subCode ?? NoSubCode; // Use NoSubCode if subCode is null for dictionary lookup

            // Check if packet should be skipped based on logging settings
            if (ShouldSkipPacket(code, subCode))
            {
                return Task.CompletedTask;
            }

            var handlerKey = (code, lookupSubCode); // Create handler key tuple

            // Try to get a specific handler for the packet code and sub-code
            if (_packetHandlers.TryGetValue(handlerKey, out var specificHandler))
            {
                return ExecuteHandler(specificHandler, packet, code, lookupSubCode); // Execute the specific handler
            }

            // If no specific handler found, try to get a main code handler (for packets with sub-codes but no specific sub-code handler)
            if (lookupSubCode != NoSubCode && _packetHandlers.TryGetValue((code, NoSubCode), out var mainCodeHandler))
            {
                _logger.LogTrace("No specific handler for {Code:X2}-{SubCode:X2}, using main code handler {Code:X2}-FF.", code, lookupSubCode, code);
                return ExecuteHandler(mainCodeHandler, packet, code, NoSubCode); // Execute the main code handler
            }

            LogUnhandled(code, subCode); // Log unhandled packet if no handler is found
            return Task.CompletedTask;
        }

        /// <summary>
        /// Determines if a packet should be skipped based on configured packet logging settings.
        /// </summary>
        /// <param name="code">The main code of the packet.</param>
        /// <param name="subCode">The sub-code of the packet (nullable).</param>
        /// <returns><c>true</c> if the packet should be skipped; otherwise, <c>false</c>.</returns>
        private bool ShouldSkipPacket(byte code, byte? subCode)
        {
            // Skip WeatherStatusUpdate packets if ShowWeather logging is disabled in settings
            if (code == WeatherStatusUpdate.Code && _settings?.PacketLogging?.ShowWeather == false)
            {
                _logger.LogTrace("Skipping Weather packet due to configuration.");
                return true;
            }

            // Skip ObjectHit (damage) packets if ShowDamage logging is disabled in settings
            if (code == ObjectHit.Code && _settings?.PacketLogging?.ShowDamage == false)
            {
                _logger.LogTrace("Skipping Damage packet due to configuration.");
                return true;
            }

            // Add more packet filtering rules here as needed based on settings

            return false; // Default to not skipping the packet if no filter rule applies
        }

        /// <summary>
        /// Executes a packet handler delegate and handles any exceptions that occur during execution.
        /// </summary>
        /// <param name="handler">The packet handler delegate to execute.</param>
        /// <param name="packet">The memory buffer containing the packet data.</param>
        /// <param name="code">The main code of the packet.</param>
        /// <param name="subCode">The sub-code of the packet.</param>
        /// <returns>A Task representing the asynchronous handler execution.</returns>
        private async Task ExecuteHandler(Func<Memory<byte>, Task> handler, Memory<byte> packet, byte code, byte subCode)
        {
            try
            {
                await handler(packet); // Execute the packet handler
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Exception dispatching handler for {MainCode:X2}-{SubCode:X2}.", code, subCode);
                // Specific error handling for movement-related packets to ensure walk lock is released in case of handler failure
                if (!_isConnectServerRouting && (code == ObjectMoved.Code || code == ObjectWalked.Code)) // Check for movement packets
                {
                    // _networkManager.SignalMovementHandledIfWalking(); // Signal movement handling to release walk lock
                }
            }
        }

        /// <summary>
        /// Handles disconnection from the server, resetting client state and logging the disconnection event.
        /// </summary>
        /// <returns>A Task representing the asynchronous disconnection handling operation.</returns>
        public Task OnDisconnected()
        {
            _logger.LogWarning("🔌 Disconnected from server.");
            // Update client state to indicate not in game, and clear scope
            // _networkManager.SetInGameStatus(false);

            if (!_isConnectServerRouting)
            {
                _logger.LogInformation("🔌 Disconnected from Game Server. State reset.");
            }
            else
            {
                _logger.LogInformation("🔌 Disconnected from Connect Server.");
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Registers packet handlers based on the <see cref="PacketHandlerAttribute"/> applied to methods in handler classes.
        /// </summary>
        private void RegisterAttributeBasedHandlers()
        {
            // Array of handler instances to scan for attribute-based handlers
            var handlerInstances = new IGamePacketHandler[]
            {
                _characterDataHandler,
                _inventoryHandler,
                _scopeHandler,
                _chatMessageHandler,
                _miscGamePacketHandler
                // ConnectServerHandler is registered manually as it does not use attributes
            };

            int count = 0; // Counter for registered handlers
            foreach (var handlerInstance in handlerInstances)
            {
                // Get all methods in the handler class that are public or non-public instance methods and have the PacketHandlerAttribute
                var methods = handlerInstance.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                  .Where(m => m.GetCustomAttribute<PacketHandlerAttribute>() != null);

                foreach (var method in methods)
                {
                    var attr = method.GetCustomAttribute<PacketHandlerAttribute>()!; // Get the PacketHandlerAttribute
                    var parameters = method.GetParameters(); // Get method parameters

                    // Validate handler method signature: must return Task and accept Memory<byte> as parameter
                    if (method.ReturnType != typeof(Task) || parameters.Length != 1 || parameters[0].ParameterType != typeof(Memory<byte>))
                    {
                        _logger.LogWarning("⚠️ Invalid packet handler signature for method {MethodName} in handler {HandlerType}. Skipping registration.", method.Name, handlerInstance.GetType().Name);
                        continue;
                    }
                    try
                    {
                        // Create a delegate for the handler method
                        var handlerDelegate = (Func<Memory<byte>, Task>)Delegate.CreateDelegate(typeof(Func<Memory<byte>, Task>), handlerInstance, method);
                        var handlerKey = (attr.MainCode, attr.SubCode); // Create handler key from attribute values

                        // Try to add handler to the dictionary, check for duplicates
                        if (_packetHandlers.TryAdd(handlerKey, handlerDelegate))
                        {
                            count++; // Increment handler count
                            _logger.LogTrace("Registered GS handler for {MainCode:X2}-{SubCode:X2}: {HandlerType}.{MethodName}", attr.MainCode, attr.SubCode, handlerInstance.GetType().Name, method.Name);
                        }
                        else
                        {
                            // Log warning if duplicate handler registration is attempted
                            var existingHandlerType = _packetHandlers[handlerKey].Target?.GetType().Name ?? "Unknown";
                            _logger.LogWarning("⚠️ Duplicate packet handler registration attempted for {MainCode:X2}-{SubCode:X2}. Method {HandlerType}.{MethodName} ignored. Already registered by {ExistingHandler}.", attr.MainCode, attr.SubCode, handlerInstance.GetType().Name, method.Name, existingHandlerType);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "💥 Failed to create delegate for handler method {HandlerType}.{MethodName}. Skipping registration.", handlerInstance.GetType().Name, method.Name);
                    }
                }
            }
            _logger.LogInformation("✅ Game Server packet handler registration complete. {Count} handlers registered.", count);
        }

        /// <summary>
        /// Manually registers Connect Server packet handlers, associating packet codes with their handler methods in <see cref="_connectServerHandler"/>.
        /// </summary>
        private void RegisterConnectServerHandlers()
        {
            // Manually register Connect Server packet handlers and their corresponding handler methods in ConnectServerHandler
            _packetHandlers[(Hello.Code, 0x01)] = _connectServerHandler.HandleHelloAsync;
            _packetHandlers[(ServerListRequest.Code, ServerListResponse.SubCode)] = _connectServerHandler.HandleServerListResponseAsync;
            _packetHandlers[(ConnectionInfoRequest.Code, ConnectionInfo.SubCode)] = _connectServerHandler.HandleConnectionInfoResponseAsync;

            _logger.LogInformation("✅ Connect Server packet handler registration complete.");
        }

        /// <summary>
        /// Tries to parse the header of a Game Server packet to extract header type, code, and sub-code.
        /// </summary>
        /// <param name="packet">The read-only span of bytes representing the packet data.</param>
        /// <param name="headerType">When this method returns, contains the header type byte.</param>
        /// <param name="code">When this method returns, contains the main code byte.</param>
        /// <param name="subCode">When this method returns, contains the sub-code byte, or null if no sub-code is present.</param>
        /// <returns><c>true</c> if the header was successfully parsed; otherwise, <c>false</c>.</returns>
        private bool TryParseGameServerHeader(ReadOnlySpan<byte> packet, out byte headerType, out byte code, out byte? subCode)
        {
            headerType = 0; code = 0; subCode = null; // Initialize output parameters

            if (packet.Length < 3) return false; // Minimum packet length to read header

            headerType = packet[0]; // Header type is always the first byte
            try
            {
                switch (headerType)
                {
                    case 0xC1: // Fixed-size header type
                    case 0xC3: // Fixed-size header type
                        code = packet[2]; // Code is at index 2
                        subCode = packet.Length >= 4 && SubCodeHolder.ContainsSubCode(code) ? packet[3] : null; // SubCode at index 3 if present and expected
                        return true; // Header parsing successful
                    case 0xC2: // Variable-size header type
                    case 0xC4: // Variable-size header type
                        if (packet.Length < 4) return false; // Minimum length for variable-size header
                        code = packet[3]; // Code is at index 3
                        subCode = packet.Length >= 5 && SubCodeHolder.ContainsSubCode(code) ? packet[4] : null; // SubCode at index 4 if present and expected
                        return true; // Header parsing successful
                    default:
                        _logger.LogWarning("❓ Unknown Game Server header type: {HeaderType:X2}", headerType);
                        return false; // Unknown header type
                }
            }
            catch (IndexOutOfRangeException ex)
            {
                _logger.LogError(ex, "💥 Index error during GS header parsing for packet: {Data}", Convert.ToHexString(packet));
                return false; // Index out of range error during parsing
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 General error during GS header parsing.");
                return false; // General exception during parsing
            }
        }

        /// <summary>
        /// Tries to parse the header of a Connect Server packet to extract header type, code, and sub-code.
        /// </summary>
        /// <param name="packet">The read-only span of bytes representing the packet data.</param>
        /// <param name="headerType">When this method returns, contains the header type byte.</param>
        /// <param name="code">When this method returns, contains the main code byte.</param>
        /// <param name="subCode">When this method returns, contains the sub-code byte, or null if no sub-code is present.</param>
        /// <returns><c>true</c> if the header was successfully parsed; otherwise, <c>false</c>.</returns>
        private bool TryParseConnectServerHeader(ReadOnlySpan<byte> packet, out byte headerType, out byte code, out byte? subCode)
        {
            headerType = 0; code = 0; subCode = null; // Initialize output parameters
            if (packet.Length < 3) return false; // Minimum packet length for header

            headerType = packet[0]; // Header type is the first byte
            try
            {
                switch (headerType)
                {
                    case 0xC1: // Fixed-size header type
                        code = packet[2]; // Code at index 2
                        subCode = packet.Length >= 4 && ConnectServerSubCodeHolder.ContainsSubCode(code) ? packet[3] : null; // SubCode at index 3 if present and expected
                        return true; // Parsing successful
                    case 0xC2: // Variable-size header type
                        if (packet.Length < 4) return false; // Minimum length for variable header
                        code = packet[3]; // Code at index 3
                        subCode = packet.Length >= 5 && ConnectServerSubCodeHolder.ContainsSubCode(code) ? packet[4] : null; // SubCode at index 4 if present and expected
                        return true; // Parsing successful
                    default:
                        _logger.LogWarning("❓ Unknown Connect Server header type: {HeaderType:X2}", headerType);
                        return false; // Unknown header type
                }
            }
            catch (IndexOutOfRangeException ex)
            {
                _logger.LogError(ex, "💥 Index error during CS header parsing for packet: {Data}", Convert.ToHexString(packet));
                return false; // Index out of range error
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 General error during CS header parsing.");
                return false; // General parsing error
            }
        }

        /// <summary>
        /// Logs a warning message for unhandled packets, including packet mode (CS/GS), code, and sub-code.
        /// </summary>
        /// <param name="code">The main code of the unhandled packet.</param>
        /// <param name="subCode">The sub-code of the unhandled packet (nullable).</param>
        private void LogUnhandled(byte code, byte? subCode)
        {
            _logger.LogWarning("⚠️ Unhandled packet ({Mode}): Code={Code:X2} SubCode={SubCode}",
                _isConnectServerRouting ? "CS" : "GS", // Indicate packet mode in log message
                code, subCode.HasValue ? subCode.Value.ToString("X2") : "N/A"); // Log code and sub-code in hex format
        }

        // --- Packet Handler Methods are now moved to separate Handler classes ---
    }
}