using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Network.Packets;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using Client.Main.Client; // For SimpleLoginClient
using Client.Main.Core.Utilities; // For PacketHandlerAttribute
using Client.Main.Networking.Services;
using System;
using System.Threading.Tasks;
using System.Collections.Generic; // For CharacterService

namespace Client.Main.Networking.PacketHandling.Handlers
{
    /// <summary>
    /// Handles miscellaneous game server packets that don't fit into other categories.
    /// </summary>
    public class MiscGamePacketHandler : IGamePacketHandler
    {
        private readonly ILogger<MiscGamePacketHandler> _logger;
        private readonly NetworkManager _networkManager; // Needed for state transitions etc.
        private readonly CharacterService _characterService; // Needed for char list request
        private readonly TargetProtocolVersion _targetVersion; // Store the version locally

        public MiscGamePacketHandler(ILoggerFactory loggerFactory, NetworkManager networkManager, CharacterService characterService, TargetProtocolVersion targetVersion)
        {
            _logger = loggerFactory.CreateLogger<MiscGamePacketHandler>();
            _networkManager = networkManager;
            _characterService = characterService;
            _targetVersion = targetVersion; // Store it
        }

        [PacketHandler(0xF1, 0x00)] // GameServerEntered
        public Task HandleGameServerEnteredAsync(Memory<byte> packet)
        {
            try // Dodaj try-catch dla bezpieczeństwa
            {
                var entered = new GameServerEntered(packet); // Możesz odczytać dane, jeśli potrzebujesz
                _logger.LogInformation("➡️🚪 Received GameServerEntered (F1, 00) from GS (PlayerID: {PlayerId}). Requesting Login...", entered.PlayerId);
                // Wywołaj SendLoginRequest w kliencie
                // _networkManager.SendLoginRequest();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error processing GameServerEntered (F1, 00).");
                // Możesz rozważyć ustawienie stanu na błąd lub rozłączenie
                // _networkManager.ViewModel.AddLogMessage("Error processing server welcome packet.", LogLevel.Error);
            }
            return Task.CompletedTask;
        }

        [PacketHandler(0xF1, 0x01)] // LoginResponse
        public Task HandleLoginResponseAsync(Memory<byte> packet)
        {
            try
            {
                var response = new LoginResponse(packet);
                _logger.LogInformation("🔑 Received LoginResponse: Result={Result} ({ResultByte:X2})", response.Success, (byte)response.Success);
                if (response.Success == LoginResponse.LoginResult.Okay)
                {
                    _logger.LogInformation("✅ Login successful! Requesting character list...");
                    Task.Run(() => _characterService.RequestCharacterListAsync());
                }
                else { _logger.LogWarning("❌ Login failed: {Reason}", response.Success); }
            }
            catch (Exception ex) { _logger.LogError(ex, "💥 Error parsing LoginResponse."); }
            return Task.CompletedTask;
        }

        [PacketHandler(0xF3, 0x00)] // CharacterList
        public Task HandleCharacterListAsync(Memory<byte> packet)
        {
            try
            {
                var characters = new List<(string Name, CharacterClassNumber Class)>();
                int characterDataSize = 0;
                int firstCharacterOffset = 0;
                byte characterCount = 0;

                // Determine sizes and offsets based on version (code unchanged here)
                switch (_targetVersion)
                {
                    case TargetProtocolVersion.Season6:
                        var charListS6 = new CharacterList(packet);
                        characterCount = charListS6.CharacterCount;
                        characterDataSize = MUnique.OpenMU.Network.Packets.ServerToClient.CharacterList.CharacterData.Length;
                        firstCharacterOffset = 8;
                        _logger.LogInformation("📜 Received character list (S6 Format): {Count} characters.", characterCount);
                        break;
                    case TargetProtocolVersion.Version097:
                        var charList097 = new CharacterList095(packet);
                        characterCount = charList097.CharacterCount;
                        characterDataSize = MUnique.OpenMU.Network.Packets.ServerToClient.CharacterList095.CharacterData.Length;
                        firstCharacterOffset = 5;
                        _logger.LogInformation("📜 Received character list (0.97/0.95 Format): {Count} characters.", characterCount);
                        break;
                    case TargetProtocolVersion.Version075:
                        var charList075 = new CharacterList075(packet);
                        characterCount = charList075.CharacterCount;
                        characterDataSize = MUnique.OpenMU.Network.Packets.ServerToClient.CharacterList075.CharacterData.Length;
                        firstCharacterOffset = 5;
                        _logger.LogInformation("📜 Received character list (0.75 Format): {Count} characters.", characterCount);
                        break;
                    default:
                        _logger.LogWarning("❓ Unsupported protocol version ({Version}) for CharacterList.", _targetVersion);
                        return Task.CompletedTask;
                }

                // Iterate and parse each character
                for (int i = 0; i < characterCount; i++)
                {
                    int offset = firstCharacterOffset + (i * characterDataSize);
                    if (offset + characterDataSize > packet.Length) { /* ... logging ... */ break; }
                    var characterDataMem = packet.Slice(offset, characterDataSize);
                    string name = "Unknown";
                    CharacterClassNumber parsedClass = CharacterClassNumber.DarkWizard;
                    int appearanceOffsetInData = 0;
                    int appearanceLength = 0;
                    int classValue = -1;

                    try
                    {
                        // Extract name and appearance info (code unchanged here)
                        switch (_targetVersion)
                        {
                            case TargetProtocolVersion.Season6:
                                var charDataS6 = new CharacterList.CharacterData(characterDataMem);
                                name = charDataS6.Name; appearanceOffsetInData = 15; appearanceLength = MUnique.OpenMU.Network.Packets.ServerToClient.CharacterList.CharacterData.Length - appearanceOffsetInData - 1; break;
                            case TargetProtocolVersion.Version097:
                                var charData097 = new CharacterList095.CharacterData(characterDataMem);
                                name = charData097.Name; appearanceOffsetInData = 15; appearanceLength = MUnique.OpenMU.Network.Packets.ServerToClient.CharacterList095.CharacterData.Length - appearanceOffsetInData; break;
                            case TargetProtocolVersion.Version075:
                                var charData075 = new CharacterList075.CharacterData(characterDataMem);
                                name = charData075.Name; appearanceOffsetInData = 14; appearanceLength = MUnique.OpenMU.Network.Packets.ServerToClient.CharacterList075.CharacterData.Length - appearanceOffsetInData; break;
                        }

                        if (appearanceOffsetInData + appearanceLength <= characterDataMem.Length && appearanceLength > 0)
                        {
                            ReadOnlySpan<byte> appearance = characterDataMem.Span.Slice(appearanceOffsetInData, appearanceLength);
                            byte appearanceByte0 = appearance[0];

                            // ***** POPRAWKA PARSOWANIA KLASY - ODCZYT BITÓW 3-7 *****
                            classValue = (appearanceByte0 >> 3) & 0b11111; // Przesuń o 3, maska 5 bitów
                            parsedClass = Map5BitClassValueToEnum(classValue); // Użyj nowego mapowania
                            // ***** KONIEC POPRAWKI PARSOWANIA KLASY *****

                            _logger.LogDebug("  -> Parsed Character: {Name} (Appearance[0]: {Byte0:X2}, ClassValue: {Value}, ParsedClass: {Parsed})", name, appearanceByte0, classValue, parsedClass);
                        }
                        else { _logger.LogWarning("Appearance data invalid for character '{Name}' at index {Index}...", name, i); }

                        characters.Add((name, parsedClass));
                        _logger.LogDebug("  -> Added Character: {Name} ({Class})", name, CharacterClassDatabase.GetClassName(parsedClass));
                    }
                    catch (Exception ex) { /* ... logging ... */ }
                } // Koniec pętli for

                if (characters.Count > 0)
                {
                    // Task.Run(() => _networkManager.SelectCharacterInteractivelyAsync(characters));
                }
                else { _logger.LogWarning("👤 No characters found on the account or failed to parse any."); }
            }
            catch (Exception ex) { _logger.LogError(ex, "💥 Error processing CharacterList packet (F3, 00)."); }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Mapuje 5-bitową wartość liczbową klasy (z bitów 3-7 Appearance[0]) na enum CharacterClassNumber.
        /// Używa wartości liczbowych z CharacterClassNumber enum.
        /// </summary>
        private CharacterClassNumber Map5BitClassValueToEnum(int classValue)
        {
            // Wartości liczbowe klas bazowych z CharacterClassNumber enum:
            // DW = 0 (00000b)
            // DK = 4 (00100b)
            // Elf = 8 (01000b)
            // MG = 12 (01100b)
            // DL = 16 (10000b)
            // SU = 20 (10100b)
            // RF = 24 (11000b)
            // SM = 2 (00010b) - druga klasa DW
            // BK = 6 (00110b) - druga klasa DK
            // ME = 10 (01010b) - druga klasa Elf
            // BS = 22 (10110b) - druga klasa SU
            // etc.

            // Mapowanie bierze pod uwagę wartości liczbowe z ENUM, a nie tylko indeksy bazowe 0-6
            return classValue switch
            {
                (int)CharacterClassNumber.DarkWizard => CharacterClassNumber.DarkWizard,
                (int)CharacterClassNumber.SoulMaster => CharacterClassNumber.SoulMaster,
                (int)CharacterClassNumber.GrandMaster => CharacterClassNumber.GrandMaster,

                (int)CharacterClassNumber.DarkKnight => CharacterClassNumber.DarkKnight,
                (int)CharacterClassNumber.BladeKnight => CharacterClassNumber.BladeKnight,
                (int)CharacterClassNumber.BladeMaster => CharacterClassNumber.BladeMaster,

                (int)CharacterClassNumber.FairyElf => CharacterClassNumber.FairyElf,
                (int)CharacterClassNumber.MuseElf => CharacterClassNumber.MuseElf,
                (int)CharacterClassNumber.HighElf => CharacterClassNumber.HighElf,

                (int)CharacterClassNumber.MagicGladiator => CharacterClassNumber.MagicGladiator,
                (int)CharacterClassNumber.DuelMaster => CharacterClassNumber.DuelMaster,

                (int)CharacterClassNumber.DarkLord => CharacterClassNumber.DarkLord,
                (int)CharacterClassNumber.LordEmperor => CharacterClassNumber.LordEmperor,

                (int)CharacterClassNumber.Summoner => CharacterClassNumber.Summoner,
                (int)CharacterClassNumber.BloodySummoner => CharacterClassNumber.BloodySummoner,
                (int)CharacterClassNumber.DimensionMaster => CharacterClassNumber.DimensionMaster,

                (int)CharacterClassNumber.RageFighter => CharacterClassNumber.RageFighter,
                (int)CharacterClassNumber.FistMaster => CharacterClassNumber.FistMaster,

                // Jeśli wartość nie pasuje do żadnej znanej klasy, zwróć domyślną
                _ => CharacterClassNumber.DarkWizard
            };
        }

        [PacketHandler(0x0F, PacketRouter.NoSubCode)] // WeatherStatusUpdate
        public Task HandleWeatherStatusUpdateAsync(Memory<byte> packet)
        {
            try
            {
                var weather = new WeatherStatusUpdate(packet);
                _logger.LogInformation("☀️ Weather: {Weather}, Variation: {Variation}", weather.Weather, weather.Variation);
            }
            catch (Exception ex) { _logger.LogError(ex, "💥 Error parsing WeatherStatusUpdate (0F)."); }
            return Task.CompletedTask;
        }

        [PacketHandler(0x0B, PacketRouter.NoSubCode)] // MapEventState
        public Task HandleMapEventStateAsync(Memory<byte> packet)
        {
            try
            {
                var eventState = new MapEventState(packet);
                _logger.LogInformation("🎉 Map Event State: Event={Event}, Enabled={Enabled}", eventState.Event, eventState.Enable);
            }
            catch (Exception ex) { _logger.LogError(ex, "💥 Error parsing MapEventState (0B)."); }
            return Task.CompletedTask;
        }

        [PacketHandler(0xC0, PacketRouter.NoSubCode)] // MessengerInitialization
        public Task HandleMessengerInitializationAsync(Memory<byte> packet)
        {
            try
            {
                var init = new MessengerInitialization(packet);
                _logger.LogInformation("✉️ Received MessengerInitialization: Letters={Letters}/{MaxLetters}, Friends={Friends}", init.LetterCount, init.MaximumLetterCount, init.FriendCount);
            }
            catch (Exception ex) { _logger.LogError(ex, "💥 Error parsing MessengerInitialization (C0)."); }
            return Task.CompletedTask;
        }

        [PacketHandler(0xA0, PacketRouter.NoSubCode)] // LegacyQuestStateList
        public Task HandleLegacyQuestStateListAsync(Memory<byte> packet)
        {
            try
            {
                var questList = new LegacyQuestStateList(packet);
                _logger.LogInformation("📜 Received LegacyQuestStateList: {Count} quests.", questList.QuestCount);
            }
            catch (Exception ex) { _logger.LogError(ex, "💥 Error parsing LegacyQuestStateList (A0)."); }
            return Task.CompletedTask;
        }

        [PacketHandler(0xF6, 0x1A)] // QuestStateList
        public Task HandleQuestStateListAsync(Memory<byte> packet)
        {
            try
            {
                var stateList = new QuestStateList(packet);
                _logger.LogInformation("❓ Received QuestStateList: {Count} active/completed quests.", stateList.QuestCount);
            }
            catch (Exception ex) { _logger.LogError(ex, "💥 Error parsing QuestStateList (F6, 1A)."); }
            return Task.CompletedTask;
        }

        // Add other miscellaneous handlers here (e.g., Guild related, Event related if not complex enough for own handler)
    }
}