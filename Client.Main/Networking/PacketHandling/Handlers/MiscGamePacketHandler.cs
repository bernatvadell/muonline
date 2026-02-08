using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Network.Packets;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using Client.Main.Core.Utilities;
using Client.Main.Networking.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Client.Main.Core.Client;
using Client.Main.Controls.UI;
using Client.Main.Controls.UI.Game;
using Client.Main.Scenes;
using Client.Main.Models;
using Client.Main.Controls.UI.Game.Hud;

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
        private readonly ScopeManager _scopeManager;

        // ───────────────────────── Constructors ─────────────────────────
        public MiscGamePacketHandler(
            ILoggerFactory loggerFactory,
            NetworkManager networkManager,
            CharacterService characterService,
            CharacterState characterState,
            ScopeManager scopeManager,
            TargetProtocolVersion targetVersion)
        {
            _logger = loggerFactory.CreateLogger<MiscGamePacketHandler>();
            _networkManager = networkManager;
            _characterService = characterService;
            _characterState = characterState;
            _scopeManager = scopeManager;
            _targetVersion = targetVersion;
        }

        // ─────────────────────── Packet Handlers ────────────────────────

        [PacketHandler(0x50, PacketRouter.NoSubCode)] // GuildJoinRequest (S2C)
        public Task HandleGuildJoinRequestAsync(Memory<byte> packet)
        {
            try
            {
                var request = new MUnique.OpenMU.Network.Packets.ServerToClient.GuildJoinRequest(packet);
                ushort requesterId = request.RequesterId;
                if (!_scopeManager.TryGetScopeObjectName(requesterId, out string requesterName))
                {
                    requesterName = $"Player (ID: {requesterId & 0x7FFF})";
                }
                _logger.LogInformation("Received guild join request from {Name} ({Id}).", requesterName, requesterId);

                MuGame.ScheduleOnMainThread(() =>
                {
                    RequestDialog.Show(
                        $"{requesterName} has invited you to their guild.",
                        onAccept: () =>
                        {
                            _ = _characterService.SendGuildJoinResponseAsync(true, requesterId);
                            _logger.LogInformation("Accepted guild join invite from {Name} ({Id}).", requesterName, requesterId);
                        },
                        onReject: () =>
                        {
                            _ = _characterService.SendGuildJoinResponseAsync(false, requesterId);
                            _logger.LogInformation("Rejected guild join invite from {Name} ({Id}).", requesterName, requesterId);
                        }
                    );
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing GuildJoinRequest packet.");
            }
            return Task.CompletedTask;
        }

        [PacketHandler(0x36, PacketRouter.NoSubCode)] // TradeRequested (S2C)
        public Task HandleTradeRequestAsync(Memory<byte> packet)
        {
            try
            {
                var request = new TradeRequest(packet); // fallback to TradeRequest if TradeRequested does not exist
                string requesterName = request.Name;
                _logger.LogInformation("Received trade request from {Name}.", requesterName);

                MuGame.ScheduleOnMainThread(() =>
                {
                    RequestDialog.Show(
                        $"{requesterName} has requested a trade.",
                        onAccept: () =>
                        {
                            _ = _characterService.SendTradeResponseAsync(true);
                            _logger.LogInformation("Accepted trade request from {Name}.", requesterName);
                        },
                        onReject: () =>
                        {
                            _ = _characterService.SendTradeResponseAsync(false);
                            _logger.LogInformation("Rejected trade request from {Name}.", requesterName);
                        }
                    );
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing TradeRequest packet.");
            }
            return Task.CompletedTask;
        }

        [PacketHandler(0xAA, 0x02)] // DuelStartRequest (S2C)
        public Task HandleDuelStartRequestAsync(Memory<byte> packet)
        {
            try
            {
                var request = new DuelStartRequest(packet);
                ushort requesterId = request.RequesterId;
                string requesterName = request.RequesterName;
                _logger.LogInformation("Received duel request from {Name} ({Id}).", requesterName, requesterId);

                _characterState.SetDuelPlayer(CharacterState.DuelPlayerType.Enemy, requesterId, requesterName);

                MuGame.ScheduleOnMainThread(() =>
                {
                    RequestDialog.Show(
                        $"{requesterName} has challenged you to a duel.",
                        onAccept: () =>
                        {
                            _ = _characterService.SendDuelResponseAsync(true, requesterId, requesterName);
                            _logger.LogInformation("Accepted duel challenge from {Name} ({Id}).", requesterName, requesterId);
                        },
                        onReject: () =>
                        {
                            _ = _characterService.SendDuelResponseAsync(false, requesterId, requesterName);
                            _logger.LogInformation("Rejected duel challenge from {Name} ({Id}).", requesterName, requesterId);
                        }
                    );
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing DuelRequested packet.");
            }
            return Task.CompletedTask;
        }

        [PacketHandler(0xAA, 0x01)] // DuelStartResult (S2C)
        public Task HandleDuelStartResultAsync(Memory<byte> packet)
        {
            try
            {
                var resultPacket = new DuelStartResult(packet);
                var result = resultPacket.Result;
                ushort opponentId = resultPacket.OpponentId;
                string opponentName = resultPacket.OpponentName;

                _logger.LogInformation("Received DuelStartResult: {Result} vs {Opponent} ({OpponentId})", result, opponentName, opponentId);

                if (result == DuelStartResult.DuelStartResultType.Success)
                {
                    _characterState.EnableDuel(true);
                    _characterState.SetHeroAsDuelPlayer(CharacterState.DuelPlayerType.Hero);
                    _characterState.SetDuelPlayer(CharacterState.DuelPlayerType.Enemy, opponentId, opponentName);

                    ShowSystemMessage($"Duel started with {opponentName}.", MessageType.System);
                }
                else
                {
                    // Mirror SourceMain5.2 behavior: store enemy and show an error/system message.
                    _characterState.SetDuelPlayer(CharacterState.DuelPlayerType.Enemy, opponentId, opponentName);

                    string msg = result switch
                    {
                        DuelStartResult.DuelStartResultType.FailedByTooLowLevel => "Duel failed: minimum level is 30.",
                        DuelStartResult.DuelStartResultType.Refused => $"{opponentName} refused your duel request.",
                        DuelStartResult.DuelStartResultType.FailedByNoFreeRoom => "Duel failed: no free duel room.",
                        DuelStartResult.DuelStartResultType.FailedByNotEnoughMoney => "Duel failed: not enough money.",
                        DuelStartResult.DuelStartResultType.FailedByError => "Duel failed due to an error.",
                        _ => $"Duel failed: {result}.",
                    };

                    ShowSystemMessage(msg, MessageType.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing DuelStartResult packet.");
            }

            return Task.CompletedTask;
        }

        [PacketHandler(0xAA, 0x03)] // DuelEnd (S2C)
        public Task HandleDuelEndAsync(Memory<byte> packet)
        {
            try
            {
                var duelEnd = new DuelEnd(packet);
                _logger.LogInformation("Received DuelEnd: Result={Result}, Opponent={OpponentName} ({OpponentId})", duelEnd.Result, duelEnd.OpponentName, duelEnd.OpponentId);

                if (duelEnd.Result == 0)
                {
                    _characterState.EnableDuel(false);
                    _characterState.EnablePetDuel(false);
                    ShowSystemMessage("Duel ended.", MessageType.System);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing DuelEnd packet.");
            }

            return Task.CompletedTask;
        }

        [PacketHandler(0x90, PacketRouter.NoSubCode)] // DevilSquareEnterResult (S2C)
        public Task HandleDevilSquareEnterResultAsync(Memory<byte> packet)
        {
            try
            {
                var res = new DevilSquareEnterResult(packet);

                if (res.Result != DevilSquareEnterResult.EnterResult.Success)
                {
                    string msg = res.Result switch
                    {
                        DevilSquareEnterResult.EnterResult.Failed => "Failed to enter Devil Square. Check invitation and level range.",
                        DevilSquareEnterResult.EnterResult.NotOpen => "Devil Square is not open.",
                        DevilSquareEnterResult.EnterResult.CharacterLevelTooHigh => "Your level is too high for this Devil Square gate.",
                        DevilSquareEnterResult.EnterResult.CharacterLevelTooLow => "Your level is too low for this Devil Square gate.",
                        DevilSquareEnterResult.EnterResult.Full => "Devil Square is full.",
                        _ => "Failed to enter Devil Square."
                    };

                    MuGame.ScheduleOnMainThread(() =>
                    {
                        DevilSquareEnterControl.Instance.CloseWindow();
                        RequestDialog.ShowInfo(msg);
                    });
                }
                else
                {
                    MuGame.ScheduleOnMainThread(() =>
                    {
                        DevilSquareEnterControl.Instance.CloseWindow();
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing DevilSquareEnterResult packet.");
            }

            return Task.CompletedTask;
        }

        [PacketHandler(0x92, PacketRouter.NoSubCode)] // UpdateMiniGameState (S2C)
        public Task HandleUpdateMiniGameStateAsync(Memory<byte> packet)
        {
            try
            {
                var update = new UpdateMiniGameState(packet);
                if (!IsDevilSquareState(update.State))
                {
                    return Task.CompletedTask;
                }

                if (!IsInDevilSquare())
                {
                    return Task.CompletedTask;
                }

                MuGame.ScheduleOnMainThread(() =>
                {
                    DevilSquareCountdownControl.Instance.StartCountdown(update.State);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing UpdateMiniGameState packet.");
            }

            return Task.CompletedTask;
        }

        private bool IsDevilSquareState(UpdateMiniGameState.MiniGameTypeState state)
        {
            return state == UpdateMiniGameState.MiniGameTypeState.DevilSquareClosed
                   || state == UpdateMiniGameState.MiniGameTypeState.DevilSquareOpened
                   || state == UpdateMiniGameState.MiniGameTypeState.DevilSquareRunning;
        }

        private bool IsInDevilSquare()
        {
            ushort mapId = _characterState.MapId;
            return mapId == 9 || mapId == 32;
        }

        [PacketHandler(0xAA, 0x04)] // DuelScore (S2C)
        public Task HandleDuelScoreAsync(Memory<byte> packet)
        {
            try
            {
                var score = new DuelScore(packet);

                if (_characterState.IsDuelPlayer(score.Player1Id, CharacterState.DuelPlayerType.Hero))
                {
                    _characterState.SetDuelScore(CharacterState.DuelPlayerType.Hero, score.Player1Score);
                    _characterState.SetDuelScore(CharacterState.DuelPlayerType.Enemy, score.Player2Score);
                }
                else if (_characterState.IsDuelPlayer(score.Player2Id, CharacterState.DuelPlayerType.Hero))
                {
                    _characterState.SetDuelScore(CharacterState.DuelPlayerType.Hero, score.Player2Score);
                    _characterState.SetDuelScore(CharacterState.DuelPlayerType.Enemy, score.Player1Score);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing DuelScore packet.");
            }

            return Task.CompletedTask;
        }

        [PacketHandler(0xAA, 0x05)] // DuelHealthUpdate (S2C)
        public Task HandleDuelHealthUpdateAsync(Memory<byte> packet)
        {
            try
            {
                var update = new DuelHealthUpdate(packet);

                if (_characterState.IsDuelPlayer(update.Player1Id, CharacterState.DuelPlayerType.Hero))
                {
                    _characterState.SetDuelHp(CharacterState.DuelPlayerType.Hero, update.Player1HealthPercentage);
                    _characterState.SetDuelHp(CharacterState.DuelPlayerType.Enemy, update.Player2HealthPercentage);
                    _characterState.SetDuelSd(CharacterState.DuelPlayerType.Hero, update.Player1ShieldPercentage);
                    _characterState.SetDuelSd(CharacterState.DuelPlayerType.Enemy, update.Player2ShieldPercentage);
                }
                else if (_characterState.IsDuelPlayer(update.Player2Id, CharacterState.DuelPlayerType.Hero))
                {
                    _characterState.SetDuelHp(CharacterState.DuelPlayerType.Hero, update.Player2HealthPercentage);
                    _characterState.SetDuelHp(CharacterState.DuelPlayerType.Enemy, update.Player1HealthPercentage);
                    _characterState.SetDuelSd(CharacterState.DuelPlayerType.Hero, update.Player2ShieldPercentage);
                    _characterState.SetDuelSd(CharacterState.DuelPlayerType.Enemy, update.Player1ShieldPercentage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing DuelHealthUpdate packet.");
            }

            return Task.CompletedTask;
        }

        [PacketHandler(0xAA, 0x06)] // DuelStatus / DuelChannelList (S2C)
        public Task HandleDuelStatusAsync(Memory<byte> packet)
        {
            try
            {
                var status = new DuelStatus(packet);
                for (int i = 0; i < 4; i++)
                {
                    var channel = status[i];
                    _characterState.SetDuelChannel(i, channel.DuelRunning, channel.DuelOpen, channel.Player1Name, channel.Player2Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing DuelStatus packet.");
            }

            return Task.CompletedTask;
        }

        [PacketHandler(0xAA, 0x07)] // DuelInit / DuelWatchRequestReply (S2C)
        public Task HandleDuelInitAsync(Memory<byte> packet)
        {
            try
            {
                var init = new DuelInit(packet);
                _logger.LogInformation("Received DuelInit: Result={Result}, RoomIndex={RoomIndex}, P1={P1} ({P1Id}), P2={P2} ({P2Id})",
                    init.Result, init.RoomIndex, init.Player1Name, init.Player1Id, init.Player2Name, init.Player2Id);

                if (init.Result == 0)
                {
                    _characterState.SetCurrentDuelChannel(init.RoomIndex);
                    _characterState.SetDuelPlayer(CharacterState.DuelPlayerType.Hero, init.Player1Id, init.Player1Name);
                    _characterState.SetDuelPlayer(CharacterState.DuelPlayerType.Enemy, init.Player2Id, init.Player2Name);
                }
                else
                {
                    ShowSystemMessage($"Failed to join duel channel (Result={init.Result}).", MessageType.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing DuelInit packet.");
            }

            return Task.CompletedTask;
        }

        [PacketHandler(0xAA, 0x08)] // DuelSpectatorAdded (S2C)
        public Task HandleDuelSpectatorAddedAsync(Memory<byte> packet)
        {
            try
            {
                var added = new DuelSpectatorAdded(packet);
                _characterState.AddDuelWatchUser(added.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing DuelSpectatorAdded packet.");
            }
            return Task.CompletedTask;
        }

        [PacketHandler(0xAA, 0x09)] // DuelWatchEnd / Spectator quit reply (S2C)
        public Task HandleDuelWatchEndAsync(Memory<byte> packet)
        {
            try
            {
                // SourceMain5.2: if nResult == 0 => clear watcher list
                if (packet.Length >= 5 && packet.Span[4] == 0)
                {
                    _characterState.RemoveAllDuelWatchUsers();
                    _characterState.SetCurrentDuelChannel(-1);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling DuelWatchEnd packet.");
            }
            return Task.CompletedTask;
        }

        [PacketHandler(0xAA, 0x0A)] // DuelSpectatorRemoved (S2C)
        public Task HandleDuelSpectatorRemovedAsync(Memory<byte> packet)
        {
            try
            {
                var removed = new DuelSpectatorRemoved(packet);
                _characterState.RemoveDuelWatchUser(removed.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing DuelSpectatorRemoved packet.");
            }
            return Task.CompletedTask;
        }

        [PacketHandler(0xAA, 0x0B)] // DuelSpectatorList (S2C)
        public Task HandleDuelSpectatorListAsync(Memory<byte> packet)
        {
            try
            {
                var list = new DuelSpectatorList(packet);
                _characterState.RemoveAllDuelWatchUsers();
                int count = Math.Min((int)list.Count, 10);
                for (int i = 0; i < count; i++)
                {
                    _characterState.AddDuelWatchUser(list[i].Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing DuelSpectatorList packet.");
            }
            return Task.CompletedTask;
        }

        [PacketHandler(0xAA, 0x0C)] // DuelFinished / DuelResult (S2C)
        public Task HandleDuelFinishedAsync(Memory<byte> packet)
        {
            try
            {
                var finished = new DuelFinished(packet);
                ShowSystemMessage($"Duel finished: {finished.Winner} defeated {finished.Loser}.", MessageType.System);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing DuelFinished packet.");
            }
            return Task.CompletedTask;
        }

        [PacketHandler(0xAA, 0x0D)] // DuelRoundStart / HealthBarInit (S2C) - SourceMain5.2 resets HP/SD on flag 0
        public Task HandleDuelRoundStartAsync(Memory<byte> packet)
        {
            try
            {
                byte flag = 0;
                if (packet.Length >= 5)
                {
                    flag = packet.Span[4];
                }

                if (flag == 0)
                {
                    _characterState.SetDuelHp(CharacterState.DuelPlayerType.Hero, 100);
                    _characterState.SetDuelHp(CharacterState.DuelPlayerType.Enemy, 100);
                    _characterState.SetDuelSd(CharacterState.DuelPlayerType.Hero, 100);
                    _characterState.SetDuelSd(CharacterState.DuelPlayerType.Enemy, 100);
                    _characterState.SetDuelFightersRegenerated(true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling DuelRoundStart packet.");
            }
            return Task.CompletedTask;
        }

        private void ShowSystemMessage(string message, MessageType type)
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                var gameScene = MuGame.Instance?.ActiveScene as GameScene;
                gameScene?.ChatLog?.AddMessage("System", message, type);
            });
        }

        [PacketHandler(0xF1, 0x00)]  // GameServerEntered
        public Task HandleGameServerEnteredAsync(Memory<byte> packet)
        {
            try
            {
                var entered = new GameServerEntered(packet);
                // The server may set the high bit on object ids; internally we work with masked ids (0x7FFF)
                // because most world lookups and movement packets use the masked form.
                ushort maskedId = (ushort)(entered.PlayerId & 0x7FFF);
                _characterState.Id = maskedId;
                _logger.LogInformation("👋 Entered Game Server. PlayerId = {Pid:X4} (masked {Masked:X4})", entered.PlayerId, maskedId);
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

        [PacketHandler(0xF1, 0x02)]  // LogoutResponse
        public async Task HandleLogoutResponseAsync(Memory<byte> packet)
        {
            try
            {
                var response = new LogoutResponse(packet);
                _logger.LogInformation("Received LogoutResponse with type {Type}.", response.Type);
                await _networkManager.ProcessLogoutResponseAsync(response.Type);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing LogoutResponse packet.");
            }
            return;
        }

        [PacketHandler(0xF3, 0x00)]  // CharacterList
        public Task HandleCharacterListAsync(Memory<byte> packet)
        {
            try
            {
                var list = new List<(string Name, CharacterClassNumber Class, ushort Level, byte[] Appearance)>();
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
                        count = packet.Span[6];
                        offset = 8;
                        int remaining = Math.Max(0, packet.Length - offset);
                        if (count > 0)
                        {
                            if (remaining == count * 44)
                            {
                                dataSize = 44; // CharacterListExtended from ServerPackets (appearance-only)
                            }
                            else if (remaining == count * 42)
                            {
                                dataSize = 42; // SourceMain5.2 PRECEIVE_CHARACTER_LIST_EXTENDED (explicit class + 25 bytes)
                            }
                            else if (remaining == count * 34)
                            {
                                dataSize = 34; // ServerPackets CharacterList (18-byte appearance)
                            }
                            else if (remaining % count == 0)
                            {
                                int inferredSize = remaining / count;
                                dataSize = inferredSize == 44 || inferredSize == 42 || inferredSize == 34
                                    ? inferredSize
                                    : 34;
                            }
                            else
                            {
                                dataSize = 34;
                            }
                        }
                        else
                        {
                            dataSize = 34;
                        }

                        if (packet.Length < offset + (count * dataSize))
                        {
                            int maxCount = dataSize > 0 ? (packet.Length - offset) / dataSize : 0;
                            _logger.LogWarning("CharacterList (S6) too short for {Count} characters; truncating to {Max}.", count, maxCount);
                            count = (byte)Math.Max(0, maxCount);
                        }
                        _logger.LogInformation("📜 Character list (S6): {Count} entries (entry size {Size}).", count, dataSize);
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
                                {
                                    // Season 6 CharacterList Extended packet structure (from SourceMain5.2):
                                    // Offset 0: Index (1 byte)
                                    // Offset 1-10: Name (10 bytes)
                                    // Offset 11-12: Level (2 bytes, little-endian)
                                    // Offset 13: CtlCode (1 byte)
                                    // Offset 14: Class (1 byte) - SERVER_CLASS_TYPE [42-byte layout only]
                                    // Offset 15: Flags (1 byte) [42-byte layout only]
                                    // Offset 16-40: Equipment (25 bytes) [42-byte layout only]
                                    // Offset 41: GuildStatus (1 byte) [42-byte layout only]
                                    // Total: 42 bytes per character
                                    //
                                    // Alternative 44-byte layout (ServerPackets CharacterListExtended):
                                    // Offset 14: status/item-block flags
                                    // Offset 15-41: Appearance (27 bytes)
                                    //
                                    // Standard 34-byte layout (ServerPackets CharacterList):
                                    // Offset 14: status/item-block flags
                                    // Offset 15-32: Appearance (18 bytes)
                                    if (dataSize == 44)
                                    {
                                        var entry = packet.Span.Slice(pos, dataSize);
                                        name = System.Text.Encoding.UTF8.GetString(entry.Slice(1, 10)).TrimEnd('\0');
                                        level = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(entry.Slice(12, 2));

                                        // 44-byte entries are ambiguous between:
                                        // A) SourceMain5.2 padded PRECEIVE_CHARACTER_LIST_EXTENDED:
                                        //    [15]=Class, [16]=Flags, [17..41]=Equipment(25)
                                        // B) ServerPackets CharacterListExtended:
                                        //    [14]=Status/ItemBlock, [15..41]=Appearance(27)
                                        //
                                        // Choose layout by scoring class decoding + SourceMain flags plausibility.
                                        byte rawClassField = entry[15];
                                        byte rawFlagsField = entry[16];
                                        bool likelySourceMainLayout = IsLikelySourceMainEquipmentFlags(rawFlagsField);

                                        CharacterClassNumber sourceMainClass =
                                            DecodeClassFromSourceMainField(rawClassField, out int sourceMainClassScore, out string sourceMainReason);
                                        int sourceMainScore = sourceMainClassScore + (likelySourceMainLayout ? 2 : -1);
                                        ReadOnlySpan<byte> sourceMainAppearance = entry.Slice(17, 25);

                                        ReadOnlySpan<byte> serverAppearanceFull = entry.Slice(15, 27);
                                        CharacterClassNumber serverPacketsClass =
                                            DecodeClassFromAppearance(serverAppearanceFull, out int serverPacketsClassScore, out string serverPacketsReason);
                                        int serverPacketsScore = serverPacketsClassScore + (likelySourceMainLayout ? 0 : 1);

                                        bool useSourceMainLayout =
                                            sourceMainScore > serverPacketsScore
                                            || (sourceMainScore == serverPacketsScore
                                                && (sourceMainClassScore > serverPacketsClassScore
                                                    || (sourceMainClass != CharacterClassNumber.DarkWizard
                                                        && serverPacketsClass == CharacterClassNumber.DarkWizard)));

                                        if (useSourceMainLayout)
                                        {
                                            cls = sourceMainClass;
                                            appearance = sourceMainAppearance;

                                            _logger.LogInformation(
                                                "S6 Character(44-SourceMain5.2): Name='{Name}', Level={Level}, ClassRaw=0x{ClassRaw:X2}, Flags=0x{Flags:X2}, Class={Class}, Score={Score}, Reason={Reason}, AppearanceLen={AppLen}",
                                                name, level, rawClassField, rawFlagsField, cls, sourceMainScore, sourceMainReason, appearance.Length);
                                        }
                                        else
                                        {
                                            cls = serverPacketsClass;
                                            // SelectCharacter rendering expects classic appearance layout.
                                            appearance = serverAppearanceFull.Length >= 18 ? serverAppearanceFull[..18] : serverAppearanceFull;

                                            _logger.LogInformation(
                                                "S6 Character(44-ServerPackets): Name='{Name}', Level={Level}, Class={Class}, Score={Score}, Reason={Reason}, AppearanceLen={AppLen}, FullAppearanceLen={FullLen}",
                                                name, level, cls, serverPacketsScore, serverPacketsReason, appearance.Length, serverAppearanceFull.Length);
                                        }
                                    }
                                    else if (dataSize == 42)
                                    {
                                        var entry = packet.Span.Slice(pos, dataSize);
                                        name = System.Text.Encoding.UTF8.GetString(entry.Slice(1, 10)).TrimEnd('\0');
                                        level = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(entry.Slice(11, 2));

                                        // SourceMain5.2 42-byte format carries explicit class at offset 14.
                                        byte serverClass = entry[14];
                                        cls = DecodeClassFromSourceMainField(serverClass, out _, out _);
                                        appearance = entry.Slice(16, Math.Min(25, entry.Length - 16));

                                        _logger.LogInformation(
                                            "S6 Character(42): Name='{Name}', Level={Level}, ServerClass={ServerClass} -> {Class}, AppearanceLen={AppLen}",
                                            name, level, serverClass, cls, appearance.Length);
                                    }
                                    else
                                    {
                                        var d6 = new CharacterList.CharacterData(packet.Slice(pos, dataSize));
                                        name = d6.Name;
                                        level = d6.Level;
                                        appearance = d6.Appearance;
                                        cls = DecodeClassFromAppearance(appearance, out _, out _);

                                        _logger.LogInformation(
                                            "S6 Character(34): Name='{Name}', Level={Level}, ParsedClass={Class}, AppearanceLen={AppLen}",
                                            name, level, cls, appearance.Length);
                                    }
                                }
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

                        // Map class from appearance bits (legacy formats)
                        // For 18-byte appearance format: Class is in byte 0, bits 4-7 (upper 4 bits)
                        if (_targetVersion != TargetProtocolVersion.Season6 && appearance.Length > 0)
                        {
                            byte apByte = appearance[0];
                            // According to Appearance.md: Byte 0, bits 4-7 = Character class
                            int rawClassVal = (apByte >> 4) & 0x0F;
                            _logger.LogDebug(
                                "Appearance byte for {Name}: 0x{Byte:X2}, raw class {RawValue}",
                                name, apByte, rawClassVal);
                            cls = CharacterClassDatabase.MapRawClassToEnum(rawClassVal);
                        }
                        else if (_targetVersion != TargetProtocolVersion.Season6)
                        {
                            _logger.LogWarning("Empty appearance data for {Name}. Defaulting to DarkWizard.", name);
                        }

                        list.Add((name, cls, level, appearance.ToArray()));
                        _logger.LogDebug(
                            "Added character: {Name}, Class={Class}, Level={Level}, AppearanceBytes={Bytes}",
                            name, cls, level, appearance.Length);
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

        [PacketHandler(0xF3, 0x01)]  // CreateCharacterResponse
        public Task HandleCreateCharacterResponseAsync(Memory<byte> packet)
        {
            try
            {
                if (packet.Length < 4)
                {
                    _logger.LogWarning("CreateCharacterResponse packet too short.");
                    return Task.CompletedTask;
                }

                // Success packet is 42 bytes, failure packet is 5 bytes
                bool success = packet.Length == 42 && packet.Span[4] != 0;
                
                _logger.LogInformation("CreateCharacter response: Length={Length}, Success={Success}", packet.Length, success);

                if (success)
                {
                    string characterName = System.Text.Encoding.ASCII.GetString(packet.Span.Slice(5, 10)).TrimEnd('\0');
                    ushort level = (ushort)(packet.Span[16] | (packet.Span[17] << 8));
                    
                    byte apByte = packet.Span[18];
                    int rawClassVal = (apByte >> 3) & 0b1_1111;
                    CharacterClassNumber classNumber = MapClassValueToEnum(rawClassVal);
                    
                    byte[] appearanceData = ReadOnlySpan<byte>.Empty.ToArray();
                    
                    _logger.LogInformation("Created character: Name={Name}, Class={Class}, Level={Level}", 
                        characterName, classNumber, level);
                    
                    MuGame.ScheduleOnMainThread(() =>
                    {
                        MessageWindow.Show($"Character '{characterName}' created successfully!");
                        
                        var net = MuGame.Network;
                        var currentList = net.GetCachedCharacterList()?.ToList() 
                            ?? new List<(string, CharacterClassNumber, ushort, byte[])>();
                        
                        currentList.Add((characterName, classNumber, level, appearanceData));
                        
                        _logger.LogInformation("Manually updating character list: {Count} total characters", currentList.Count);
                        net.ProcessCharacterList(currentList);
                    });
                }
                else
                {
                    MessageWindow.Show("Character creation failed.\nPlease check character name and try again.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing CreateCharacterResponse packet.");
            }
            return Task.CompletedTask;
        }

        [PacketHandler(0xF3, 0x02)]  // DeleteCharacterResponse
        public Task HandleDeleteCharacterResponseAsync(Memory<byte> packet)
        {
            try
            {
                if (packet.Length < 5)
                {
                    _logger.LogWarning("DeleteCharacterResponse packet too short.");
                    return Task.CompletedTask;
                }

                byte result = packet.Span[4]; // Result code at index 4
                
                _logger.LogInformation("DeleteCharacter response: Result={Result}", result);

                if (result == 0x01) // Success
                {
                    string deletedCharName = _characterService.LastDeletedCharacterName;
                    _logger.LogInformation("Character '{Name}' deleted successfully!", deletedCharName);
                    
                    MuGame.ScheduleOnMainThread(() =>
                    {
                        MessageWindow.Show($"Character '{deletedCharName}' deleted successfully!");
                        
                        // Get current cached list and remove the deleted character
                        var currentList = _networkManager.GetCachedCharacterList()?.ToList() 
                            ?? new List<(string, CharacterClassNumber, ushort, byte[])>();
                        currentList.RemoveAll(c => c.Item1.Equals(deletedCharName, StringComparison.OrdinalIgnoreCase));
                        
                        _logger.LogInformation("Manually updating character list: {Count} total characters (removed '{Name}')", 
                            currentList.Count, deletedCharName);
                        _networkManager.ProcessCharacterList(currentList);
                        
                        // Clear the tracking field
                        _characterService.LastDeletedCharacterName = null;
                    });
                }
                else
                {
                    _logger.LogWarning("❌ Character deletion failed with code: {Code}", result);
                    
                    string errorMessage = result switch
                    {
                        0x00 => "Character deletion failed",
                        0x02 => "Invalid security code",
                        0x03 => "Character is in guild (leave guild first)",
                        0x04 => "Character not found",
                        _ => $"Unknown error (code: {result})"
                    };
                    
                    MuGame.ScheduleOnMainThread(() =>
                    {
                        MessageWindow.Show($"Character deletion failed:\n{errorMessage}");
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing DeleteCharacterResponse packet.");
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
                _characterState.SetLegacyQuestStates(qList);
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
            // According to S6 protocol, the 5-bit class value in appearance maps
            // directly to CharacterClassNumber values (including 2nd/3rd classes).
            // Values not listed below are treated as DarkWizard fallback.
            return value switch
            {
                0 or 2 or 3 or 4 or 6 or 7 or 8 or 10 or 11 or 12 or 13 or
                16 or 17 or 20 or 22 or 23 or 24 or 25 => (CharacterClassNumber)value,
                _ => CharacterClassNumber.DarkWizard
            };
        }

        private CharacterClassNumber DecodeClassFromSourceMainField(byte rawClassValue, out int score, out string reason)
        {
            if (IsKnownServerClassValue(rawClassValue))
            {
                score = 6;
                reason = "direct-server-class";
                return MapClassValueToEnum(rawClassValue);
            }

            int shiftedBy3 = (rawClassValue >> 3) & 0b1_1111;
            if (IsKnownServerClassValue(shiftedBy3))
            {
                score = 5;
                reason = "shifted-by-3";
                return MapClassValueToEnum(shiftedBy3);
            }

            int renderedClass = (rawClassValue >> 4) & 0x0F;
            if (TryMapRenderedClassToEnum(renderedClass, out var rendered))
            {
                score = 4;
                reason = "rendered-4bit";
                return rendered;
            }

            int baseClass = rawClassValue & 0x07;
            if (TryMapBaseClassToEnum(baseClass, out var baseMapped))
            {
                score = 3;
                reason = "base-3bit";
                return baseMapped;
            }

            score = 0;
            reason = "fallback-dark-wizard";
            return CharacterClassNumber.DarkWizard;
        }

        private CharacterClassNumber DecodeClassFromAppearance(ReadOnlySpan<byte> appearance, out int score, out string reason)
        {
            if (appearance.IsEmpty)
            {
                score = 0;
                reason = "empty-appearance";
                return CharacterClassNumber.DarkWizard;
            }

            byte raw = appearance[0];
            int raw5 = (raw >> 3) & 0b1_1111;
            if (IsKnownServerClassValue(raw5))
            {
                score = 4;
                reason = "appearance-shifted-by-3";
                return MapClassValueToEnum(raw5);
            }

            if (IsKnownServerClassValue(raw))
            {
                score = 3;
                reason = "appearance-direct-server-class";
                return MapClassValueToEnum(raw);
            }

            int renderedClass = (raw >> 4) & 0x0F;
            if (TryMapRenderedClassToEnum(renderedClass, out var rendered))
            {
                score = 2;
                reason = "appearance-rendered-4bit";
                return rendered;
            }

            int baseClass = raw & 0x07;
            if (TryMapBaseClassToEnum(baseClass, out var baseMapped))
            {
                score = 1;
                reason = "appearance-base-3bit";
                return baseMapped;
            }

            score = 0;
            reason = "fallback-dark-wizard";
            return CharacterClassNumber.DarkWizard;
        }

        private static bool TryMapRenderedClassToEnum(int renderedClass, out CharacterClassNumber cls)
        {
            cls = renderedClass switch
            {
                0 => CharacterClassNumber.DarkWizard,
                1 => CharacterClassNumber.DarkKnight,
                2 => CharacterClassNumber.FairyElf,
                3 => CharacterClassNumber.MagicGladiator,
                4 => CharacterClassNumber.DarkLord,
                5 => CharacterClassNumber.Summoner,
                6 => CharacterClassNumber.RageFighter,
                8 => CharacterClassNumber.SoulMaster,
                9 => CharacterClassNumber.BladeKnight,
                10 => CharacterClassNumber.MuseElf,
                11 => CharacterClassNumber.DuelMaster,
                12 => CharacterClassNumber.LordEmperor,
                13 => CharacterClassNumber.BloodySummoner,
                14 => CharacterClassNumber.FistMaster,
                15 => CharacterClassNumber.GrandMaster,
                _ => CharacterClassNumber.DarkWizard
            };

            return renderedClass is 0 or 1 or 2 or 3 or 4 or 5 or 6 or 8 or 9 or 10 or 11 or 12 or 13 or 14 or 15;
        }

        private static bool TryMapBaseClassToEnum(int baseClass, out CharacterClassNumber cls)
        {
            cls = baseClass switch
            {
                0 => CharacterClassNumber.DarkWizard,
                1 => CharacterClassNumber.DarkKnight,
                2 => CharacterClassNumber.FairyElf,
                3 => CharacterClassNumber.MagicGladiator,
                4 => CharacterClassNumber.DarkLord,
                5 => CharacterClassNumber.Summoner,
                6 => CharacterClassNumber.RageFighter,
                _ => CharacterClassNumber.DarkWizard
            };

            return baseClass is >= 0 and <= 6;
        }

        private static bool IsLikelySourceMainEquipmentFlags(byte flags)
        {
            // In SourceMain5.2 ReadEquipmentExtended(), only bits 0x10 and 0x20
            // are used from this field for extended state/extra parts.
            return (flags & 0xCF) == 0;
        }

        private static bool IsKnownServerClassValue(int value)
        {
            return value is 0 or 2 or 3 or 4 or 6 or 7 or 8 or 10 or 11 or 12 or 13
                or 16 or 17 or 20 or 22 or 23 or 24 or 25;
        }
    }
}
