using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Network.Packets;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using Client.Main.Core.Utilities;
using Client.Main.Networking.Services;     // For CharacterService
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Client.Main.Core.Client;

namespace Client.Main.Networking.PacketHandling.Handlers
{
    /// <summary>
    /// Handles miscellaneous game packets such as login, character listing, weather, quests, and messenger initialization.
    /// </summary>
    public class MiscGamePacketHandler : IGamePacketHandler
    {
        // ──────────────────────────── Fields ────────────────────────────
        private readonly ILogger<MiscGamePacketHandler> _logger;
        private readonly NetworkManager _networkManager;
        private readonly CharacterService _characterService;
        private readonly CharacterState _characterState;
        private readonly TargetProtocolVersion _targetVersion;

        // ───────────────────────── Constructors ─────────────────────────
        public MiscGamePacketHandler(
            ILoggerFactory loggerFactory,
            NetworkManager networkManager,
            CharacterService characterService,
            CharacterState characterState,
            TargetProtocolVersion targetVersion)
        {
            _logger = loggerFactory.CreateLogger<MiscGamePacketHandler>();
            _networkManager = networkManager;
            _characterService = characterService;
            _characterState = characterState;
            _targetVersion = targetVersion;
        }

        // ─────────────────────── Packet Handlers ────────────────────────

        [PacketHandler(0xF1, 0x00)]  // GameServerEntered
        public Task HandleGameServerEnteredAsync(Memory<byte> packet)
        {
            try
            {
                var entered = new GameServerEntered(packet);
                _characterState.Id = entered.PlayerId;
                _logger.LogInformation("👋 Entered Game Server. PlayerId = {Pid:X4}", entered.PlayerId);
                _networkManager.ProcessGameServerEntered();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing GameServerEntered packet.");
            }
            return Task.CompletedTask;
        }

        [PacketHandler(0xF1, 0x01)]  // LoginResponse
        public Task HandleLoginResponseAsync(Memory<byte> packet)
        {
            try
            {
                var response = new LoginResponse(packet); // This is MUnique.OpenMU.Network.Packets.ServerToClient.LoginResponse
                _logger.LogInformation("🔑 LoginResponse: Success={Success} (0x{Code:X2})", response.Success, (byte)response.Success);

                LoginResponse.LoginResult clientResult;
                try
                {
                    clientResult = (LoginResponse.LoginResult)response.Success;
                }
                catch (InvalidCastException) // Or check Enum.IsDefined if you prefer
                {
                    _logger.LogWarning("Received unknown LoginResult value from server: {ServerValue}. Defaulting to ConnectionError.", (byte)response.Success);
                    clientResult = LoginResponse.LoginResult.ConnectionError;
                }

                _networkManager.ProcessLoginResponse(clientResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing LoginResponse packet.");
            }
            return Task.CompletedTask;
        }

        [PacketHandler(0xF3, 0x00)]  // CharacterList
        public Task HandleCharacterListAsync(Memory<byte> packet)
        {
            try
            {
                var list = new List<(string Name, CharacterClassNumber Class, ushort Level)>();
                int dataSize = 0;
                int offset = 0;
                byte count = 0;

                // Determine header format by protocol version
                const int MinHeaderS6 = 7;
                const int MinHeaderLegacy = 5;

                switch (_targetVersion)
                {
                    case TargetProtocolVersion.Season6:
                        if (packet.Length < MinHeaderS6 + 1)
                        {
                            _logger.LogWarning("CharacterList (S6) packet too short for header.");
                            return Task.CompletedTask;
                        }
                        var refS6 = new CharacterListRef(packet.Span);
                        count = refS6.CharacterCount;
                        dataSize = CharacterList.CharacterData.Length;
                        offset = 8;
                        if (packet.Length < CharacterListRef.GetRequiredSize(count))
                        {
                            _logger.LogWarning("CharacterList (S6) too short for {Count} characters.", count);
                            count = 0;
                        }
                        _logger.LogInformation("📜 Character list (S6): {Count} entries.", count);
                        break;

                    case TargetProtocolVersion.Version097:
                        if (packet.Length < MinHeaderLegacy + 1)
                        {
                            _logger.LogWarning("CharacterList (0.97) packet too short for header.");
                            return Task.CompletedTask;
                        }
                        var ref97 = new CharacterList095Ref(packet.Span);
                        count = ref97.CharacterCount;
                        dataSize = CharacterList095.CharacterData.Length;
                        offset = 5;
                        if (packet.Length < CharacterList095Ref.GetRequiredSize(count))
                        {
                            _logger.LogWarning("CharacterList (0.97) too short for {Count} characters.", count);
                            count = 0;
                        }
                        _logger.LogInformation("📜 Character list (0.97): {Count} entries.", count);
                        break;

                    case TargetProtocolVersion.Version075:
                        if (packet.Length < MinHeaderLegacy + 1)
                        {
                            _logger.LogWarning("CharacterList (0.75) packet too short for header.");
                            return Task.CompletedTask;
                        }
                        var ref75 = new CharacterList075Ref(packet.Span);
                        count = ref75.CharacterCount;
                        dataSize = CharacterList075.CharacterData.Length;
                        offset = 5;
                        if (packet.Length < CharacterList075Ref.GetRequiredSize(count))
                        {
                            _logger.LogWarning("CharacterList (0.75) too short for {Count} characters.", count);
                            count = 0;
                        }
                        _logger.LogInformation("📜 Character list (0.75): {Count} entries.", count);
                        break;

                    default:
                        _logger.LogWarning("Unsupported protocol version ({Version}) for CharacterList.", _targetVersion);
                        return Task.CompletedTask;
                }

                // Parse each character entry
                for (int i = 0; i < count; i++)
                {
                    int pos = offset + i * dataSize;
                    if (pos + dataSize > packet.Length)
                    {
                        _logger.LogWarning("CharacterList too short slicing character {Index}.", i);
                        break;
                    }

                    var span = packet.Slice(pos, dataSize).Span;
                    string name = "Error";
                    ushort level = 0;
                    CharacterClassNumber cls = CharacterClassNumber.DarkWizard;
                    ReadOnlySpan<byte> appearance = ReadOnlySpan<byte>.Empty;

                    try
                    {
                        // Extract fields by version
                        switch (_targetVersion)
                        {
                            case TargetProtocolVersion.Season6:
                                var d6 = new CharacterList.CharacterData(packet.Slice(pos, dataSize));
                                name = d6.Name;
                                level = d6.Level;
                                appearance = d6.Appearance;
                                break;
                            case TargetProtocolVersion.Version097:
                                var d97 = new CharacterList095.CharacterData(packet.Slice(pos, dataSize));
                                name = d97.Name;
                                level = d97.Level;
                                appearance = d97.Appearance;
                                break;
                            case TargetProtocolVersion.Version075:
                                var d75 = new CharacterList075.CharacterData(packet.Slice(pos, dataSize));
                                name = d75.Name;
                                level = d75.Level;
                                appearance = d75.Appearance;
                                break;
                        }

                        // Map class from appearance bits
                        if (appearance.Length > 0)
                        {
                            byte apByte = appearance[0];
                            int rawClassVal = (apByte >> 3) & 0b1_1111;
                            _logger.LogDebug(
                                "Appearance byte for {Name}: 0x{Byte:X2}, raw class {RawValue}",
                                name, apByte, rawClassVal);
                            cls = MapClassValueToEnum(rawClassVal);
                        }
                        else
                        {
                            _logger.LogWarning("Empty appearance data for {Name}. Defaulting to DarkWizard.", name);
                        }

                        list.Add((name, cls, level));
                        _logger.LogDebug(
                            "Added character: {Name}, Class={Class}, Level={Level}",
                            name, cls, level);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error parsing character at index {Index}.", i);
                    }
                }

                _networkManager.ProcessCharacterList(list);
                _logger.LogInformation(
                    "Finished CharacterList: passed {Count} entries to NetworkManager.",
                    list.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing CharacterList packet.");
            }
            return Task.CompletedTask;
        }

        [PacketHandler(0x0F, PacketRouter.NoSubCode)]  // WeatherStatusUpdate
        public Task HandleWeatherStatusUpdateAsync(Memory<byte> packet)
        {
            try
            {
                var weather = new WeatherStatusUpdate(packet);
                _logger.LogInformation(
                    "Weather update: {Weather}, variation {Variation}",
                    weather.Weather, weather.Variation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing WeatherStatusUpdate packet.");
            }
            return Task.CompletedTask;
        }

        [PacketHandler(0x0B, PacketRouter.NoSubCode)]  // MapEventState
        public Task HandleMapEventStateAsync(Memory<byte> packet)
        {
            try
            {
                var state = new MapEventState(packet);
                _logger.LogInformation(
                    "Map event: {Event}, enabled={Enabled}",
                    state.Event, state.Enable);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing MapEventState packet.");
            }
            return Task.CompletedTask;
        }

        [PacketHandler(0xC0, PacketRouter.NoSubCode)]  // MessengerInitialization
        public Task HandleMessengerInitializationAsync(Memory<byte> packet)
        {
            try
            {
                var init = new MessengerInitialization(packet);
                _logger.LogInformation(
                    "Messenger initialized: {Letters}/{MaxLetters} letters, {Friends} friends",
                    init.LetterCount, init.MaximumLetterCount, init.FriendCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing MessengerInitialization packet.");
            }
            return Task.CompletedTask;
        }

        [PacketHandler(0xA0, PacketRouter.NoSubCode)]  // LegacyQuestStateList
        public Task HandleLegacyQuestStateListAsync(Memory<byte> packet)
        {
            try
            {
                var qList = new LegacyQuestStateList(packet);
                _logger.LogInformation(
                    "Legacy quest list received: {Count} entries",
                    qList.QuestCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing LegacyQuestStateList packet.");
            }
            return Task.CompletedTask;
        }

        [PacketHandler(0xF6, 0x1A)]             // QuestStateList
        public Task HandleQuestStateListAsync(Memory<byte> packet)
        {
            try
            {
                var qState = new QuestStateList(packet);
                _logger.LogInformation(
                    "Quest state list: {Count} entries",
                    qState.QuestCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing QuestStateList packet.");
            }
            return Task.CompletedTask;
        }

        // ────────────────────────── Helpers ────────────────────────────

        /// <summary>
        /// Maps a raw 5-bit class value to the CharacterClassNumber enum.
        /// Update mappings to match your server's definitions.
        /// </summary>
        private CharacterClassNumber MapClassValueToEnum(int value)
        {
            return value switch
            {
                0 => CharacterClassNumber.DarkWizard,
                1 => CharacterClassNumber.SoulMaster,
                2 => CharacterClassNumber.SoulMaster,
                3 => CharacterClassNumber.GrandMaster,
                4 => CharacterClassNumber.DarkKnight,
                5 => CharacterClassNumber.BladeKnight,
                6 => CharacterClassNumber.BladeKnight,
                7 => CharacterClassNumber.BladeMaster,
                8 => CharacterClassNumber.FairyElf,
                9 => CharacterClassNumber.MuseElf,
                10 => CharacterClassNumber.MuseElf,
                11 => CharacterClassNumber.HighElf,
                12 => CharacterClassNumber.MagicGladiator,
                13 => CharacterClassNumber.DuelMaster,
                16 => CharacterClassNumber.DarkLord,
                17 => CharacterClassNumber.LordEmperor,
                20 => CharacterClassNumber.Summoner,
                21 => CharacterClassNumber.BloodySummoner,
                22 => CharacterClassNumber.BloodySummoner,
                23 => CharacterClassNumber.DimensionMaster,
                24 => CharacterClassNumber.RageFighter,
                25 => CharacterClassNumber.FistMaster,
                _ => CharacterClassNumber.DarkWizard
            };
        }
    }
}
