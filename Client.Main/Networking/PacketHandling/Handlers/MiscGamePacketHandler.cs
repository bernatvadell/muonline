using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Network.Packets;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using Client.Main.Client; // For CharacterState, NetworkManager, TargetProtocolVersion
using Client.Main.Core.Utilities; // For PacketHandlerAttribute, CharacterClassDatabase
using Client.Main.Networking.Services;
using System;
using System.Threading.Tasks;
using System.Collections.Generic; // For List
using System.Linq; // For Any()

namespace Client.Main.Networking.PacketHandling.Handlers
{
    public class MiscGamePacketHandler : IGamePacketHandler
    {
        private readonly ILogger<MiscGamePacketHandler> _logger;
        private readonly NetworkManager _networkManager;
        private readonly CharacterService _characterService;
        private readonly TargetProtocolVersion _targetVersion;

        public MiscGamePacketHandler(ILoggerFactory loggerFactory, NetworkManager networkManager, CharacterService characterService, TargetProtocolVersion targetVersion)
        {
            _logger = loggerFactory.CreateLogger<MiscGamePacketHandler>();
            _networkManager = networkManager;
            _characterService = characterService;
            _targetVersion = targetVersion;
        }

        [PacketHandler(0xF1, 0x00)] // GameServerEntered
        public Task HandleGameServerEnteredAsync(Memory<byte> packet)
        {
            try
            {
                var entered = new GameServerEntered(packet);
                _logger.LogInformation(">>> HandleGameServerEnteredAsync: Received F1 00 from GS (PlayerID: {PlayerId}). Calling NetworkManager.ProcessGameServerEntered...", entered.PlayerId);
                // Wywołaj metodę w NetworkManager
                _networkManager.ProcessGameServerEntered();
                _logger.LogInformation("<<< HandleGameServerEnteredAsync: NetworkManager.ProcessGameServerEntered called.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error processing GameServerEntered (F1, 00).");
                // _networkManager.OnErrorOccurred("Error processing server welcome packet."); // Powiadom o błędzie
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

                // **** ZMIANA: Wywołaj metodę w NetworkManager zamiast bezpośrednio serwisu ****
                _networkManager.ProcessLoginResponse(response.Success);
                // **** KONIEC ZMIANY ****

                // Usunięto logikę stąd:
                // if (response.Success == LoginResponse.LoginResult.Okay) { ... } else { ... }
            }
            catch (Exception ex) { _logger.LogError(ex, "💥 Error parsing LoginResponse."); }
            return Task.CompletedTask;
        }

        [PacketHandler(0xF3, 0x00)] // CharacterList
        public Task HandleCharacterListAsync(Memory<byte> packet)
        {
            try
            {
                var characters = new List<(string Name, CharacterClassNumber Class, ushort Level)>();
                int characterDataSize = 0;
                int firstCharacterOffset = 0;
                byte characterCount = 0;

                // Define minimum header sizes BEFORE count field
                const int MinHeaderSizeS6 = 7;
                const int MinHeaderSizeLegacy = 5;

                // --- Logika określania rozmiaru i offsetu (bez zmian) ---
                switch (_targetVersion)
                {
                    case TargetProtocolVersion.Season6:
                        if (packet.Length < MinHeaderSizeS6 + 1) { _logger.LogWarning("CharacterList (S6) packet too short for header."); return Task.CompletedTask; }
                        var charListS6 = new CharacterListRef(packet.Span);
                        characterCount = charListS6.CharacterCount;
                        characterDataSize = CharacterList.CharacterData.Length;
                        firstCharacterOffset = 8;
                        if (packet.Length < CharacterListRef.GetRequiredSize(characterCount)) { _logger.LogWarning("CharacterList (S6) packet too short for {Count} characters.", characterCount); characterCount = 0; }
                        _logger.LogInformation("📜 Received character list (S6 Format): {Count} characters.", characterCount);
                        break;
                    case TargetProtocolVersion.Version097:
                        if (packet.Length < MinHeaderSizeLegacy + 1) { _logger.LogWarning("CharacterList (0.97) packet too short for header."); return Task.CompletedTask; }
                        var charList097 = new CharacterList095Ref(packet.Span);
                        characterCount = charList097.CharacterCount;
                        characterDataSize = CharacterList095.CharacterData.Length;
                        firstCharacterOffset = 5;
                        if (packet.Length < CharacterList095Ref.GetRequiredSize(characterCount)) { _logger.LogWarning("CharacterList (0.97) packet too short for {Count} characters.", characterCount); characterCount = 0; }
                        _logger.LogInformation("📜 Received character list (0.97/0.95 Format): {Count} characters.", characterCount);
                        break;
                    case TargetProtocolVersion.Version075:
                        if (packet.Length < MinHeaderSizeLegacy + 1) { _logger.LogWarning("CharacterList (0.75) packet too short for header."); return Task.CompletedTask; }
                        var charList075 = new CharacterList075Ref(packet.Span);
                        characterCount = charList075.CharacterCount;
                        characterDataSize = CharacterList075.CharacterData.Length;
                        firstCharacterOffset = 5;
                        if (packet.Length < CharacterList075Ref.GetRequiredSize(characterCount)) { _logger.LogWarning("CharacterList (0.75) packet too short for {Count} characters.", characterCount); characterCount = 0; }
                        _logger.LogInformation("📜 Received character list (0.75 Format): {Count} characters.", characterCount);
                        break;
                    default:
                        _logger.LogWarning("❓ Unsupported protocol version ({Version}) for CharacterList.", _targetVersion);
                        return Task.CompletedTask;
                }

                // --- Iteracja i parsowanie ---
                for (int i = 0; i < characterCount; i++)
                {
                    int offset = firstCharacterOffset + (i * characterDataSize);
                    if (offset + characterDataSize > packet.Length)
                    {
                        _logger.LogWarning("CharacterList packet too short while calculating slice for character at index {Index}.", i);
                        break;
                    }
                    var characterDataMem = packet.Slice(offset, characterDataSize);
                    string name = "Error";
                    ushort level = 0;
                    CharacterClassNumber parsedClass = CharacterClassNumber.DarkWizard; // Default
                    ReadOnlySpan<byte> appearanceData = ReadOnlySpan<byte>.Empty;

                    try
                    {
                        // Odczytaj dane specyficzne dla wersji
                        switch (_targetVersion)
                        {
                            case TargetProtocolVersion.Season6:
                                var dataS6 = new CharacterList.CharacterData(characterDataMem);
                                name = dataS6.Name;
                                level = dataS6.Level;
                                appearanceData = dataS6.Appearance;
                                break;
                            case TargetProtocolVersion.Version097:
                                var data097 = new CharacterList095.CharacterData(characterDataMem);
                                name = data097.Name;
                                level = data097.Level;
                                appearanceData = data097.Appearance;
                                break;
                            case TargetProtocolVersion.Version075:
                                var data075 = new CharacterList075.CharacterData(characterDataMem);
                                name = data075.Name;
                                level = data075.Level;
                                appearanceData = data075.Appearance;
                                break;
                        }

                        // *** NOWA LOGIKA PARSOWANIA KLASY ***
                        if (appearanceData.Length > 0)
                        {
                            // Załóżmy, że pierwszy bajt (index 0) danych Appearance zawiera informacje o klasie.
                            // Górne 5 bitów (bits 3-7) często reprezentuje klasę bazową.
                            byte appearanceByte = appearanceData[0];
                            int classValue = (appearanceByte >> 3) & 0b11111; // Przesuń o 3, maska 5 bitów

                            _logger.LogDebug("  -> Appearance Byte[0] for {Name}: {ByteValue:X2}, Extracted Raw Class Value: {RawValue}", name, appearanceByte, classValue);

                            parsedClass = MapClassValueToEnum(classValue); // Użyj funkcji mapującej
                        }
                        else
                        {
                            _logger.LogWarning("Appearance data is empty for character {Name}. Defaulting to DW.", name);
                        }
                        // *** KONIEC NOWEJ LOGIKI PARSOWANIA KLASY ***


                        characters.Add((name, parsedClass, level));
                        _logger.LogDebug("  -> Added Character: {Name} (Final Parsed Class: {ParsedClass}), Lv: {Level}", name, parsedClass, level);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error parsing character data at index {Index} for version {Version}", i, _targetVersion);
                    }
                } // End of loop

                _networkManager.ProcessCharacterList(characters); // Przekaż listę do NetworkManager
                _logger.LogInformation("<<< HandleCharacterListAsync: NetworkManager.ProcessCharacterList called with {Count} characters.", characters.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error processing CharacterList packet (F3, 00).");
            }
            return Task.CompletedTask;
        }

        // *** ZMIENIONA NAZWA I POTENCJALNIE LOGIKA MAPOWANIA ***
        /// <summary>
        /// Maps the extracted class value (based on bit analysis) to the CharacterClassNumber enum.
        /// **VERIFY THIS MAPPING AGAINST YOUR SERVER/CLIENT VERSION.**
        /// </summary>
        private CharacterClassNumber MapClassValueToEnum(int classValue)
        {
            // Ta funkcja mapuje wartość liczbową (0-31 potencjalnie) na enum.
            // Musisz dostosować `case` do wartości liczbowych odpowiadających klasom w TWOJEJ wersji gry.
            // Poniżej jest przykład bazujący na typowych wartościach dla 5 bitów.
            return classValue switch
            {
                0 => CharacterClassNumber.DarkWizard,      // 00000
                1 => CharacterClassNumber.DarkWizard,      // 00001 (często 2nd stage DW) -> SM
                2 => CharacterClassNumber.SoulMaster,      // 00010
                3 => CharacterClassNumber.GrandMaster,     // 00011

                4 => CharacterClassNumber.DarkKnight,      // 00100
                5 => CharacterClassNumber.DarkKnight,      // 00101 (często 2nd stage DK) -> BK
                6 => CharacterClassNumber.BladeKnight,     // 00110
                7 => CharacterClassNumber.BladeMaster,     // 00111

                8 => CharacterClassNumber.FairyElf,        // 01000
                9 => CharacterClassNumber.FairyElf,        // 01001 (często 2nd stage Elf) -> ME
                10 => CharacterClassNumber.MuseElf,        // 01010
                11 => CharacterClassNumber.HighElf,        // 01011

                12 => CharacterClassNumber.MagicGladiator, // 01100
                13 => CharacterClassNumber.DuelMaster,     // 01101

                16 => CharacterClassNumber.DarkLord,       // 10000
                17 => CharacterClassNumber.LordEmperor,    // 10001

                20 => CharacterClassNumber.Summoner,       // 10100
                21 => CharacterClassNumber.Summoner,       // 10101 (często 2nd stage Sum) -> BS
                22 => CharacterClassNumber.BloodySummoner, // 10110
                23 => CharacterClassNumber.DimensionMaster,// 10111

                24 => CharacterClassNumber.RageFighter,    // 11000
                25 => CharacterClassNumber.FistMaster,     // 11001

                // Dodaj inne klasy jeśli istnieją w CharacterClassNumber i znasz ich wartości bitowe

                _ => CharacterClassNumber.DarkWizard      // Domyślna wartość, jeśli nie pasuje
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