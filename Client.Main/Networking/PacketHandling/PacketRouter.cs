// File: Client/Main/Networking/PacketHandling/PacketRouter.cs
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using MUnique.OpenMU.Network.Packets.ConnectServer;
using Client.Main.Configuration;     // For MuOnlineSettings
using Client.Main.Core.Utilities;    // For PacketHandlerAttribute, SubCodeHolder
using Client.Main.Networking.Services; // For CharacterService, LoginService
using Client.Main.Networking.PacketHandling.Handlers;
using Client.Main.Core.Client;

namespace Client.Main.Networking.PacketHandling
{
    /// <summary>
    /// Routes incoming network packets to the appropriate handlers
    /// for Connect Server or Game Server connections.
    /// </summary>
    public class PacketRouter
    {
        // ──────────────────────────── Constants ────────────────────────────
        public const byte NoSubCode = 0xFF;

        // ──────────────────────────── Fields ────────────────────────────
        private readonly ILogger<PacketRouter> _logger;
        private readonly NetworkManager _networkManager;
        private readonly MuOnlineSettings _settings;

        private readonly CharacterDataHandler _characterDataHandler;
        private readonly InventoryHandler _inventoryHandler;
        private readonly ScopeHandler _scopeHandler;
        private readonly ChatMessageHandler _chatMessageHandler;
        private readonly ConnectServerHandler _connectServerHandler;
        private readonly MiscGamePacketHandler _miscGamePacketHandler;

        private readonly Dictionary<(byte MainCode, byte SubCode), Func<Memory<byte>, Task>> _packetHandlers
            = new();

        private bool _isConnectServerRouting;

        // ───────────────────────── Properties ─────────────────────────
        public TargetProtocolVersion TargetVersion { get; }

        // ───────────────────────── Constructors ─────────────────────────
        public PacketRouter(
            ILoggerFactory loggerFactory,
            CharacterService characterService,
            LoginService loginService,        // (unused here, but may be needed)
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

            // Instantiate handlers
            _characterDataHandler = new CharacterDataHandler(loggerFactory, characterState, networkManager, targetVersion);
            _inventoryHandler = new InventoryHandler(loggerFactory, characterState, networkManager, targetVersion);
            _scopeHandler = new ScopeHandler(loggerFactory, scopeManager, characterState, networkManager, targetVersion);
            _chatMessageHandler = new ChatMessageHandler(loggerFactory);
            _connectServerHandler = new ConnectServerHandler(loggerFactory, networkManager);
            _miscGamePacketHandler = new MiscGamePacketHandler(loggerFactory, networkManager, characterService, characterState, targetVersion);

            RegisterAttributeBasedHandlers();
            RegisterConnectServerHandlers();
        }

        // ─────────────────────── Routing Mode ────────────────────────

        /// <summary>
        /// Switches between Connect Server and Game Server packet routing.
        /// </summary>
        public void SetRoutingMode(bool isConnectServer)
        {
            _isConnectServerRouting = isConnectServer;
            _logger.LogInformation("Routing mode set to: {Mode}", isConnectServer ? "Connect Server" : "Game Server");
        }

        // ─────────────────────── Public API ─────────────────────────

        /// <summary>
        /// Routes an incoming packet based on current routing mode.
        /// </summary>
        public Task RoutePacketAsync(ReadOnlySequence<byte> sequence)
        {
            var packet = sequence.ToArray().AsMemory();
            _logger.LogDebug("Received packet ({Length} bytes): {Data}", packet.Length, Convert.ToHexString(packet.Span));

            return _isConnectServerRouting
                ? RouteConnectServerPacketAsync(packet)
                : RouteGameServerPacketAsync(packet);
        }

        /// <summary>
        /// Handles disconnection events.
        /// </summary>
        public Task OnDisconnected()
        {
            _logger.LogWarning("Disconnected from {Server}.", _isConnectServerRouting ? "Connect Server" : "Game Server");
            // TODO: Reset client state as needed, e.g. _networkManager.SetInGameStatus(false);
            return Task.CompletedTask;
        }

        // ───────────────────────── Routing ────────────────────────────

        private Task RouteGameServerPacketAsync(Memory<byte> packet)
        {
            if (!TryParseGameServerHeader(packet.Span, out var headerType, out var code, out var subCode))
            {
                _logger.LogWarning("Failed to parse Game Server header: {Data}", Convert.ToHexString(packet.Span));
                return Task.CompletedTask;
            }

            _logger.LogDebug("GS Packet: Header={H:X2}, Code={C:X2}, Sub={S}", headerType, code, subCode);
            return DispatchPacketInternalAsync(packet, code, subCode, headerType);
        }

        private Task RouteConnectServerPacketAsync(Memory<byte> packet)
        {
            if (!TryParseConnectServerHeader(packet.Span, out var headerType, out var code, out var subCode))
            {
                _logger.LogWarning("Failed to parse Connect Server header: {Data}", Convert.ToHexString(packet.Span));
                return Task.CompletedTask;
            }

            _logger.LogDebug("CS Packet: Header={H:X2}, Code={C:X2}, Sub={S}", headerType, code, subCode);
            return DispatchPacketInternalAsync(packet, code, subCode, headerType);
        }

        // ───────────────────────── Dispatch ───────────────────────────

        private Task DispatchPacketInternalAsync(Memory<byte> packet, byte code, byte subCode, byte headerType)
        {
            _logger.LogTrace("Dispatching {Mode} packet: Code={C:X2}, Sub={S}, Len={L}",
                _isConnectServerRouting ? "CS" : "GS", code, subCode == NoSubCode ? "N/A" : subCode.ToString("X2"), packet.Length);

            if (ShouldSkipPacket(code))
                return Task.CompletedTask;

            var key = (code, subCode);
            if (_packetHandlers.TryGetValue(key, out var handler))
            {
                _logger.LogTrace("Executing handler for {C:X2}-{S}", code, subCode);
                return ExecuteHandlerAsync(handler, packet, code, subCode);
            }

            // Fallback to main-code-only handler
            if (subCode != NoSubCode && _packetHandlers.TryGetValue((code, NoSubCode), out handler))
            {
                _logger.LogTrace("Executing main-code handler for {C:X2}-FF", code);
                return ExecuteHandlerAsync(handler, packet, code, NoSubCode);
            }

            LogUnhandled(code, subCode);
            return Task.CompletedTask;
        }

        private bool ShouldSkipPacket(byte code)
        {
            if (code == WeatherStatusUpdate.Code && !_settings.PacketLogging.ShowWeather)
            {
                _logger.LogTrace("Skipping weather packet by config.");
                return true;
            }
            if (code == ObjectHit.Code && !_settings.PacketLogging.ShowDamage)
            {
                _logger.LogTrace("Skipping damage packet by config.");
                return true;
            }
            return false;
        }

        private async Task ExecuteHandlerAsync(Func<Memory<byte>, Task> handler, Memory<byte> packet, byte code, byte subCode)
        {
            try
            {
                await handler(packet);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in handler {C:X2}-{S}", code, subCode);
                if (!_isConnectServerRouting && (code == ObjectMoved.Code || code == ObjectWalked.Code))
                {
                    // Ensure walk lock is released on error
                    // _networkManager.SignalMovementHandledIfWalking();
                }
            }
        }

        // ─────────────────── Registration ───────────────────────────

        private void RegisterAttributeBasedHandlers()
        {
            var handlers = new IGamePacketHandler[]
            {
                _characterDataHandler,
                _inventoryHandler,
                _scopeHandler,
                _chatMessageHandler,
                _miscGamePacketHandler
            };

            int registered = 0;
            foreach (var instance in handlers)
            {
                var methods = instance.GetType()
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.GetCustomAttribute<PacketHandlerAttribute>() != null);

                foreach (var method in methods)
                {
                    var attr = method.GetCustomAttribute<PacketHandlerAttribute>()!;
                    if (method.ReturnType != typeof(Task)
                     || method.GetParameters().Length != 1
                     || method.GetParameters()[0].ParameterType != typeof(Memory<byte>))
                    {
                        _logger.LogWarning("Invalid handler signature: {Type}.{Method}", instance.GetType().Name, method.Name);
                        continue;
                    }

                    var del = (Func<Memory<byte>, Task>)Delegate.CreateDelegate(
                        typeof(Func<Memory<byte>, Task>), instance, method);

                    var key = (attr.MainCode, attr.SubCode);
                    if (_packetHandlers.TryAdd(key, del))
                    {
                        registered++;
                        _logger.LogTrace("Registered GS handler {C:X2}-{S} => {Type}.{Method}",
                            attr.MainCode, attr.SubCode, instance.GetType().Name, method.Name);
                    }
                    else
                    {
                        _logger.LogWarning("Duplicate handler for {C:X2}-{S}, skipping {Type}.{Method}",
                            attr.MainCode, attr.SubCode, instance.GetType().Name, method.Name);
                    }
                }
            }
            _logger.LogInformation("Registered {Count} Game Server handlers.", registered);
        }

        private void RegisterConnectServerHandlers()
        {
            _packetHandlers[(Hello.Code, Hello.SubCode)] = _connectServerHandler.HandleHelloAsync;
            _packetHandlers[(ServerListRequest.Code, ServerListResponse.SubCode)]
                = _connectServerHandler.HandleServerListResponseAsync;
            _packetHandlers[(ConnectionInfoRequest.Code, ConnectionInfo.SubCode)]
                = _connectServerHandler.HandleConnectionInfoResponseAsync;

            _logger.LogInformation("Registered Connect Server handlers.");
        }

        // ───────────────────────── Header Parsing ───────────────────────

        private bool TryParseGameServerHeader(ReadOnlySpan<byte> span, out byte headerType, out byte code, out byte subCode)
        {
            headerType = 0; code = 0; subCode = NoSubCode;
            if (span.Length < 3) return false;

            headerType = span[0];
            try
            {
                switch (headerType)
                {
                    case 0xC1:
                    case 0xC3:
                        code = span[2];
                        subCode = (span.Length >= 4 && SubCodeHolder.ContainsSubCode(code))
                                  ? span[3]
                                  : NoSubCode;
                        return true;

                    case 0xC2:
                    case 0xC4:
                        if (span.Length < 4) return false;
                        code = span[3];
                        subCode = (span.Length >= 5 && SubCodeHolder.ContainsSubCode(code))
                                  ? span[4]
                                  : NoSubCode;
                        return true;

                    default:
                        _logger.LogWarning("Unknown GS header type: {H:X2}", headerType);
                        return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing GS header: {Data}", Convert.ToHexString(span));
                return false;
            }
        }

        private bool TryParseConnectServerHeader(ReadOnlySpan<byte> span, out byte headerType, out byte code, out byte subCode)
        {
            headerType = 0; code = 0; subCode = NoSubCode;
            if (span.Length < 3) return false;

            headerType = span[0];
            try
            {
                switch (headerType)
                {
                    case 0xC1:
                        code = span[2];
                        subCode = (span.Length >= 4 && ConnectServerSubCodeHolder.ContainsSubCode(code))
                                  ? span[3]
                                  : NoSubCode;
                        return true;

                    case 0xC2:
                        if (span.Length < 4) return false;
                        code = span[3];
                        subCode = (span.Length >= 5 && ConnectServerSubCodeHolder.ContainsSubCode(code))
                                  ? span[4]
                                  : NoSubCode;
                        return true;

                    default:
                        _logger.LogWarning("Unknown CS header type: {H:X2}", headerType);
                        return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing CS header: {Data}", Convert.ToHexString(span));
                return false;
            }
        }

        // ────────────────────────── Logging ────────────────────────────

        private void LogUnhandled(byte code, byte subCode)
        {
            _logger.LogDebug("Unhandled {Mode} packet: Code={C:X2}, Sub={S}",
                _isConnectServerRouting ? "CS" : "GS",
                code, subCode == NoSubCode ? "N/A" : subCode.ToString("X2"));
        }
    }
}
