using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Network;
using Client.Main.Networking.PacketHandling;
using System;
using System.Threading.Tasks;
using MUnique.OpenMU.Network.Packets.ClientToServer;
using MUnique.OpenMU.Network.Packets;
using Client.Main.Core.Client;

namespace Client.Main.Networking.Services
{
    /// <summary>
    /// Manages sending character‐related packets to the game server,
    /// including character list requests, character selection, movement, and animations.
    /// </summary>
    public class CharacterService
    {
        private readonly ConnectionManager _connectionManager;
        private readonly ILogger<CharacterService> _logger;
        
        /// <summary>
        /// Tracks the last character name that was requested for deletion.
        /// Used to update the cached character list after successful deletion.
        /// </summary>
        public string LastDeletedCharacterName { get; internal set; }

        public CharacterService(
            ConnectionManager connectionManager,
            ILogger<CharacterService> logger)
        {
            _connectionManager = connectionManager;
            _logger = logger;
        }

        /// <summary>
        /// Sends a request to drop an item from inventory onto the ground at the specified tile.
        /// </summary>
        /// <param name="tileX">Target tile X.</param>
        /// <param name="tileY">Target tile Y.</param>
        /// <param name="inventorySlot">Inventory slot index (including server offset, e.g. 12 + y*8 + x).</param>
        public async Task SendDropItemRequestAsync(byte tileX, byte tileY, byte inventorySlot)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected - cannot send drop item request.");
                return;
            }

            _logger.LogInformation("Sending drop item request: Slot={Slot}, Pos=({X},{Y})...", inventorySlot, tileX, tileY);
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                    PacketBuilder.BuildDropItemRequestPacket(_connectionManager.Connection.Output, tileX, tileY, inventorySlot));
                _logger.LogInformation("Drop item request sent: Slot={Slot}, Pos=({X},{Y}).", inventorySlot, tileX, tileY);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending drop item request for slot {Slot} at ({X},{Y}).", inventorySlot, tileX, tileY);
            }
        }

        /// <summary>
        /// Requests the list of characters for the current account.
        /// </summary>
        public async Task RequestCharacterListAsync()
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected — cannot request character list.");
                return;
            }

            _logger.LogInformation("Sending character list request...");
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                    PacketBuilder.BuildRequestCharacterListPacket(_connectionManager.Connection.Output));
                _logger.LogInformation("Character list request sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending character list request.");
            }
        }

        /// <summary>
        /// Sends a request to create a new character.
        /// </summary>
        /// <param name="characterName">The name of the new character.</param>
        /// <param name="characterClass">The class of the new character.</param>
        public async Task SendCreateCharacterRequestAsync(string characterName, CharacterClassNumber characterClass)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected — cannot create character.");
                return;
            }

            _logger.LogInformation("Sending create character request: Name={Name}, Class={Class}...", 
                characterName, characterClass);
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                    PacketBuilder.BuildCreateCharacterPacket(_connectionManager.Connection.Output, characterName, characterClass));
                _logger.LogInformation("Create character request sent: Name={Name}, Class={Class}.", 
                    characterName, characterClass);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending create character request for {Name}.", characterName);
            }
        }

        /// <summary>
        /// Sends a request to delete a character.
        /// </summary>
        /// <param name="characterName">The name of the character to delete.</param>
        /// <param name="securityCode">The security code (default empty string for no security).</param>
        public async Task SendDeleteCharacterRequestAsync(string characterName, string securityCode = "")
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected — cannot delete character.");
                return;
            }

            // Track the character name for use in the response handler
            LastDeletedCharacterName = characterName;
            
            _logger.LogInformation("Sending delete character request: Name={Name}...", characterName);
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                    PacketBuilder.BuildDeleteCharacterPacket(_connectionManager.Connection.Output, characterName, securityCode));
                _logger.LogInformation("Delete character request sent: Name={Name}.", characterName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending delete character request for {Name}.", characterName);
            }
        }

        /// <summary>
        /// Sends a party invite to another player.
        /// </summary>
        public async Task SendPartyInviteAsync(ushort targetPlayerId)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected - cannot send party invite.");
                return;
            }

            _logger.LogInformation("Sending party invite to player ID {PlayerId}", targetPlayerId);
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                {
                    var packet = new PartyInviteRequest(_connectionManager.Connection.Output.GetMemory(PartyInviteRequest.Length).Slice(0, PartyInviteRequest.Length));
                    packet.TargetPlayerId = targetPlayerId;
                    return PartyInviteRequest.Length;
                });
                _logger.LogInformation("Party invite sent to player ID {PlayerId}", targetPlayerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending party invite to player ID {PlayerId}", targetPlayerId);
            }
        }

        /// <summary>
        /// Sends a response to a party invitation.
        /// </summary>
        public async Task SendPartyResponseAsync(bool accepted, ushort requesterId)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected — cannot respond to party invite.");
                return;
            }

            _logger.LogInformation("Sending party response: Accepted={Accepted}, RequesterId={Id}", accepted, requesterId);
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                {
                    var packet = new PartyInviteResponse(_connectionManager.Connection.Output.GetMemory(PartyInviteResponse.Length).Slice(0, PartyInviteResponse.Length));
                    packet.Accepted = accepted;
                    packet.RequesterId = requesterId;
                    return PartyInviteResponse.Length;
                });
                _logger.LogInformation("Party response sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending party response.");
            }
        }

        /// <summary>
        /// Sends a request to kick a player from party (or leave party yourself).
        /// </summary>
        /// <param name="playerIndex">Index of player to kick (or your own index to leave party)</param>
        public async Task SendPartyKickRequestAsync(byte playerIndex)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected — cannot send party kick request.");
                return;
            }

            _logger.LogInformation("Sending party kick request for player index {PlayerIndex}", playerIndex);
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                {
                    var packet = new PartyPlayerKickRequest(_connectionManager.Connection.Output.GetMemory(PartyPlayerKickRequest.Length).Slice(0, PartyPlayerKickRequest.Length));
                    packet.PlayerIndex = playerIndex;
                    return PartyPlayerKickRequest.Length;
                });
                _logger.LogInformation("Party kick request sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending party kick request.");
            }
        }

        /// <summary>
        /// Sends a request for the current party list.
        /// </summary>
        public async Task RequestPartyListAsync()
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogWarning("Not connected — cannot request party list.");
                return;
            }

            _logger.LogTrace("Sending PartyListRequest..."); // Użyj LogTrace, aby nie zaśmiecać logów
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                {
                    var packet = new PartyListRequest(_connectionManager.Connection.Output.GetMemory(PartyListRequest.Length).Slice(0, PartyListRequest.Length));
                    return PartyListRequest.Length;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending PartyListRequest packet.");
            }
        }

        /// <summary>
        /// Sends a response to a guild join invitation.
        /// </summary>
        public async Task SendGuildJoinResponseAsync(bool accepted, ushort requesterId)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected — cannot respond to guild invite.");
                return;
            }

            _logger.LogInformation("Sending guild join response: Accepted={Accepted}, RequesterId={Id}", accepted, requesterId);
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                {
                    var packet = new GuildJoinResponse(_connectionManager.Connection.Output.GetMemory(GuildJoinResponse.Length).Slice(0, GuildJoinResponse.Length));
                    packet.Accepted = accepted;
                    packet.RequesterId = requesterId;
                    return GuildJoinResponse.Length;
                });
                _logger.LogInformation("Guild join response sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending guild join response.");
            }
        }

        /// <summary>
        /// Sends a response to a trade request.
        /// </summary>
        public async Task SendTradeResponseAsync(bool accepted)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected — cannot respond to trade request.");
                return;
            }

            _logger.LogInformation("Sending trade response: Accepted={Accepted}", accepted);
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                {
                    var packet = new TradeRequestResponse(_connectionManager.Connection.Output.GetMemory(TradeRequestResponse.Length).Slice(0, TradeRequestResponse.Length));
                    packet.TradeAccepted = accepted;
                    return TradeRequestResponse.Length;
                });
                _logger.LogInformation("Trade response sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending trade response.");
            }
        }

        /// <summary>
        /// Sends a trade request to another player.
        /// </summary>
        public async Task SendTradeRequestAsync(ushort targetPlayerId)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected — cannot send trade request.");
                return;
            }

            _logger.LogInformation("Sending trade request to player ID {PlayerId}", targetPlayerId);
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                {
                    var packet = new TradeRequest(_connectionManager.Connection.Output.GetMemory(TradeRequest.Length).Slice(0, TradeRequest.Length));
                    packet.PlayerId = targetPlayerId;
                    return TradeRequest.Length;
                });
                _logger.LogInformation("Trade request sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending trade request.");
            }
        }

        /// <summary>
        /// Sends a duel start request to another player.
        /// </summary>
        public async Task SendDuelStartRequestAsync(ushort targetPlayerId, string targetPlayerName)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected - cannot send duel request.");
                return;
            }

            _logger.LogInformation("Sending duel request to player ID {PlayerId}", targetPlayerId);
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                {
                    var packet = new DuelStartRequest(_connectionManager.Connection.Output.GetMemory(DuelStartRequest.Length).Slice(0, DuelStartRequest.Length));
                    packet.PlayerId = targetPlayerId;
                    packet.PlayerName = targetPlayerName ?? string.Empty;
                    return DuelStartRequest.Length;
                });
                _logger.LogInformation("Duel request sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending duel request to player ID {PlayerId}", targetPlayerId);
            }
        }

        /// <summary>
        /// Sends a guild join request to the specified guild master (target player).
        /// </summary>
        public async Task SendGuildJoinRequestAsync(ushort guildMasterPlayerId)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected - cannot send guild join request.");
                return;
            }

            _logger.LogInformation("Sending guild join request to guild master ID {PlayerId}", guildMasterPlayerId);
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                {
                    var packet = new GuildJoinRequest(_connectionManager.Connection.Output.GetMemory(GuildJoinRequest.Length).Slice(0, GuildJoinRequest.Length));
                    packet.GuildMasterPlayerId = guildMasterPlayerId;
                    return GuildJoinRequest.Length;
                });
                _logger.LogInformation("Guild join request sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending guild join request to player ID {PlayerId}", guildMasterPlayerId);
            }
        }

        /// <summary>
        /// Requests the item list of another player's personal store.
        /// </summary>
        public async Task SendPlayerStoreListRequestAsync(ushort targetPlayerId, string targetPlayerName)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected - cannot request player store.");
                return;
            }

            _logger.LogInformation("Requesting player store list from player ID {PlayerId}", targetPlayerId);
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                {
                    var packet = new PlayerShopItemListRequest(_connectionManager.Connection.Output.GetMemory(PlayerShopItemListRequest.Length).Slice(0, PlayerShopItemListRequest.Length));
                    packet.PlayerId = targetPlayerId;
                    packet.PlayerName = targetPlayerName ?? string.Empty;
                    return PlayerShopItemListRequest.Length;
                });
                _logger.LogInformation("Player store list request sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting player store list from player ID {PlayerId}", targetPlayerId);
            }
        }

        /// <summary>
        /// Sets the money amount in the trade.
        /// </summary>
        public async Task SendTradeMoneyAsync(uint amount)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected — cannot set trade money.");
                return;
            }

            _logger.LogInformation("Setting trade money to {Amount}", amount);
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                {
                    var packet = new SetTradeMoney(_connectionManager.Connection.Output.GetMemory(SetTradeMoney.Length).Slice(0, SetTradeMoney.Length));
                    packet.Amount = amount;
                    return SetTradeMoney.Length;
                });
                _logger.LogInformation("Trade money set sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending trade money.");
            }
        }

        /// <summary>
        /// Toggles the trade confirmation button (check/uncheck).
        /// </summary>
        public async Task SendTradeButtonAsync(bool pressed)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected — cannot toggle trade button.");
                return;
            }

            _logger.LogInformation("Setting trade button to {State}", pressed ? "Pressed" : "Unpressed");
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                {
                    var packet = new TradeButtonStateChange(_connectionManager.Connection.Output.GetMemory(TradeButtonStateChange.Length).Slice(0, TradeButtonStateChange.Length));
                    packet.NewState = pressed
                        ? TradeButtonState.Checked
                        : TradeButtonState.Unchecked;
                    _logger.LogDebug("TradeButtonStateChange payload: NewState={NewState} ({NewStateValue})", packet.NewState, (byte)packet.NewState);
                    try
                    {
                        Memory<byte> bytes = packet;
                        _logger.LogDebug("Sending TradeButtonStateChange ({Length} bytes): {Bytes}", bytes.Length, Convert.ToHexString(bytes.Span));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to log TradeButtonStateChange bytes.");
                    }
                    return TradeButtonStateChange.Length;
                });
                _logger.LogInformation("Trade button toggle sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending trade button toggle.");
            }
        }

        /// <summary>
        /// Cancels the active trade.
        /// </summary>
        public async Task SendTradeCancelAsync()
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected — cannot cancel trade.");
                return;
            }

            _logger.LogInformation("Cancelling trade");
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                {
                    var packet = new TradeCancel(_connectionManager.Connection.Output.GetMemory(TradeCancel.Length).Slice(0, TradeCancel.Length));
                    try
                    {
                        Memory<byte> bytes = packet;
                        _logger.LogDebug("Sending TradeCancel ({Length} bytes): {Bytes}", bytes.Length, Convert.ToHexString(bytes.Span));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to log TradeCancel bytes.");
                    }
                    return TradeCancel.Length;
                });
                _logger.LogInformation("Trade cancel sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending trade cancel.");
            }
        }

        /// <summary>
        /// Sends a response to a duel request.
        /// </summary>
        public async Task SendDuelResponseAsync(bool accepted, ushort requesterId, string requesterName)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected — cannot respond to duel request.");
                return;
            }

            _logger.LogInformation("Sending duel response: Accepted={Accepted}, RequesterId={Id}", accepted, requesterId);
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                {
                    var packet = new DuelStartResponse(_connectionManager.Connection.Output.GetMemory(DuelStartResponse.Length).Slice(0, DuelStartResponse.Length));
                    packet.Response = accepted;
                    packet.PlayerId = requesterId;
                    packet.PlayerName = requesterName;
                    return DuelStartResponse.Length;
                });
                _logger.LogInformation("Duel response sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending duel response.");
            }
        }

        /// <summary>
        /// Sends a request to stop the currently running duel.
        /// </summary>
        public async Task SendDuelStopRequestAsync()
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected — cannot send duel stop request.");
                return;
            }

            _logger.LogInformation("Sending duel stop request...");
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                {
                    var packet = new DuelStopRequest(_connectionManager.Connection.Output.GetMemory(DuelStopRequest.Length).Slice(0, DuelStopRequest.Length));
                    return DuelStopRequest.Length;
                });
                _logger.LogInformation("Duel stop request sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending duel stop request.");
            }
        }

        /// <summary>
        /// Sends a request to join a duel channel as spectator.
        /// </summary>
        public async Task SendDuelChannelJoinRequestAsync(byte channelId)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected — cannot send duel channel join request.");
                return;
            }

            _logger.LogInformation("Sending duel channel join request: ChannelId={ChannelId}", channelId);
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                {
                    var packet = new DuelChannelJoinRequest(_connectionManager.Connection.Output.GetMemory(DuelChannelJoinRequest.Length).Slice(0, DuelChannelJoinRequest.Length));
                    packet.ChannelId = channelId;
                    return DuelChannelJoinRequest.Length;
                });
                _logger.LogInformation("Duel channel join request sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending duel channel join request.");
            }
        }

        /// <summary>
        /// Sends a request to quit the duel channel spectator mode.
        /// </summary>
        public async Task SendDuelChannelQuitRequestAsync()
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected — cannot send duel channel quit request.");
                return;
            }

            _logger.LogInformation("Sending duel channel quit request...");
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                {
                    var packet = new DuelChannelQuitRequest(_connectionManager.Connection.Output.GetMemory(DuelChannelQuitRequest.Length).Slice(0, DuelChannelQuitRequest.Length));
                    return DuelChannelQuitRequest.Length;
                });
                _logger.LogInformation("Duel channel quit request sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending duel channel quit request.");
            }
        }

        public async Task SendWarpCommandRequestAsync(ushort warpInfoIndex, uint commandKey = 0)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected — cannot send warp command request.");
                return;
            }

            _logger.LogInformation("Sending Warp Command Request for index {WarpInfoIndex}...", warpInfoIndex);
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                {
                    var packet = new WarpCommandRequest(_connectionManager.Connection.Output.GetMemory(WarpCommandRequest.Length).Slice(0, WarpCommandRequest.Length));
                    packet.CommandKey = commandKey;
                    packet.WarpInfoIndex = warpInfoIndex;
                    return WarpCommandRequest.Length;
                });
                _logger.LogInformation("Warp Command Request sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending Warp Command Request.");
            }
        }

        /// <summary>
        /// Sends a logout request instructing the server how to transition the client out of the current game session.
        /// </summary>
        public async Task SendLogoutRequestAsync(LogOutType type)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected — cannot send logout request.");
                return;
            }

            _logger.LogInformation("Sending logout request with type {Type}...", type);
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                    PacketBuilder.BuildLogoutRequestPacket(_connectionManager.Connection.Output, type));
                _logger.LogInformation("Logout request ({Type}) sent.", type);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending logout request ({Type}).", type);
            }
        }

        public async Task SendClientReadyAfterMapChangeAsync()
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected — cannot send ClientReadyAfterMapChange.");
                return;
            }

            _logger.LogInformation("Sending ClientReadyAfterMapChange packet...");
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                    PacketBuilder.BuildClientReadyAfterMapChangePacket(_connectionManager.Connection.Output));
                _logger.LogInformation("ClientReadyAfterMapChange packet sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending ClientReadyAfterMapChange packet.");
            }
        }

        /// <summary>
        /// Selects the specified character on the game server.
        /// </summary>
        public async Task SelectCharacterAsync(string characterName)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected — cannot select character.");
                return;
            }

            _logger.LogInformation("Selecting character '{CharacterName}'...", characterName);
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                    PacketBuilder.BuildSelectCharacterPacket(_connectionManager.Connection.Output, characterName));
                _logger.LogInformation("Character selection packet sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending character selection packet.");
            }
        }

        /// <summary>
        /// Sends an instant move (teleport) request to the given coordinates.
        /// </summary>
        public async Task SendInstantMoveRequestAsync(byte x, byte y)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected — cannot move instantly.");
                return;
            }

            _logger.LogInformation("Sending instant move to ({X}, {Y})...", x, y);
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                    PacketBuilder.BuildInstantMoveRequestPacket(_connectionManager.Connection.Output, x, y));
                _logger.LogInformation("Instant move request sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending instant move request.");
            }
        }

        /// <summary>
        /// Sends an animation request with the specified rotation and animation number.
        /// </summary>
        public async Task SendAnimationRequestAsync(byte rotation, byte animationNumber)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected — cannot send animation request.");
                return;
            }

            _logger.LogInformation(
                "Sending animation request (rotation={Rotation}, animation={AnimationNumber})...",
                rotation, animationNumber);

            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                    PacketBuilder.BuildAnimationRequestPacket(_connectionManager.Connection.Output, rotation, animationNumber));
                _logger.LogInformation("Animation request sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending animation request.");
            }
        }

        /// <summary>
        /// Sends a walk request along a path of direction steps.
        /// </summary>
        public async Task SendWalkRequestAsync(byte startX, byte startY, byte[] path)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected — cannot send walk request.");
                return;
            }
            if (path == null || path.Length == 0)
            {
                _logger.LogWarning("Empty path — walk request not sent.");
                return;
            }

            _logger.LogInformation(
                "Sending walk request from ({StartX}, {StartY}) with {Steps} steps...",
                startX, startY, path.Length);

            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                    PacketBuilder.BuildWalkRequestPacket(
                        _connectionManager.Connection.Output,
                        startX, startY, path));
                _logger.LogInformation("Walk request sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending walk request.");
            }
        }

        /// <summary>
        /// Sends a hit request packet to the server.
        /// </summary>
        public async Task SendHitRequestAsync(ushort targetId, byte attackAnimation, byte lookingDirection)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected — cannot send hit request.");
                return;
            }

            _logger.LogInformation(
                "Sending hit request for TargetID: {TargetId}, Anim: {Animation}, Dir: {Direction}...",
                targetId, attackAnimation, lookingDirection);

            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                    PacketBuilder.BuildHitRequestPacket(_connectionManager.Connection.Output, targetId, attackAnimation, lookingDirection));
                _logger.LogInformation("Hit request sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending hit request.");
            }
        }

        /// <summary>
        /// Sends a skill usage request packet to the server.
        /// </summary>
        public async Task SendSkillRequestAsync(ushort skillId, ushort targetId)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected — cannot send skill request.");
                return;
            }

            _logger.LogInformation(
                "Sending skill request: SkillID={SkillId}, TargetID={TargetId}...",
                skillId, targetId);

            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                    PacketBuilder.BuildSkillRequestPacket(_connectionManager.Connection.Output, skillId, targetId));
                _logger.LogInformation("Skill request sent: SkillID={SkillId}, TargetID={TargetId}.", skillId, targetId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending skill request for skill {SkillId} on target {TargetId}.", skillId, targetId);
            }
        }

        /// <summary>
        /// Sends a consume item request packet to the server (potions, jewels, etc.).
        /// </summary>
        public async Task SendConsumeItemRequestAsync(byte itemSlot, byte targetSlot = 0)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected — cannot send consume item request.");
                return;
            }

            _logger.LogInformation(
                "Sending consume item request: ItemSlot={ItemSlot}, TargetSlot={TargetSlot}...",
                itemSlot, targetSlot);

            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                    PacketBuilder.BuildConsumeItemPacket(_connectionManager.Connection.Output, itemSlot, targetSlot));
                _logger.LogInformation("Consume item request sent: ItemSlot={ItemSlot}, TargetSlot={TargetSlot}.", itemSlot, targetSlot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending consume item request for slot {ItemSlot}.", itemSlot);
            }
        }

        /// <summary>
        /// Sends an area skill usage request packet to the server (buffs, area attacks, etc.).
        /// </summary>
        public async Task SendAreaSkillRequestAsync(ushort skillId, byte targetX, byte targetY, byte rotation, ushort extraTargetId = 0)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected — cannot send area skill request.");
                return;
            }

            _logger.LogInformation(
                "Sending area skill request: SkillID={SkillId}, Position=({X},{Y}), Rotation={Rotation}...",
                skillId, targetX, targetY, rotation);

            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                    PacketBuilder.BuildAreaSkillPacket(_connectionManager.Connection.Output, skillId, targetX, targetY, rotation, extraTargetId));
                _logger.LogInformation("Area skill request sent: SkillID={SkillId}.", skillId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending area skill request for skill {SkillId}.", skillId);
            }
        }

        /// <summary>
        /// Sends a request to increase a specific character stat attribute.
        /// </summary>
        /// <param name="attribute">The attribute to be increased.</param>
        public async Task SendIncreaseCharacterStatPointRequestAsync(CharacterStatAttribute attribute)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected — cannot send stat increase request.");
                return;
            }

            _logger.LogInformation("Sending stat increase request for attribute: {Attribute}...", attribute);
            try
            {
                await _connectionManager.Connection.SendAsync(() => PacketBuilder.BuildIncreaseCharacterStatPointPacket(_connectionManager.Connection.Output, attribute));
                _logger.LogInformation("Stat increase request sent for attribute: {Attribute}.", attribute);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending stat increase request for attribute {Attribute}.", attribute);
            }
        }

        /// <summary>
        /// Sends a request to pick up a dropped item or money by its network ID.
        /// </summary>
        public async Task SendPickupItemRequestAsync(ushort itemId, TargetProtocolVersion version)
        {
            ushort itemIdMasked = (ushort)(itemId & 0x7FFF);
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected - cannot send pickup item request.");
                return;
            }

            _logger.LogInformation("Sending pickup item request for itemId: {ItemId}...", itemIdMasked);
            try
            {
                // Using the ConnectionExtensions directly based on protocol version
                switch (version)
                {
                    case TargetProtocolVersion.Season6:
                    case TargetProtocolVersion.Version097:
                        await _connectionManager.Connection.SendPickupItemRequestAsync(itemIdMasked);
                        break;
                    case TargetProtocolVersion.Version075:
                        await _connectionManager.Connection.SendPickupItemRequest075Async(itemIdMasked);
                        break;
                }
                _logger.LogInformation("Pickup item request sent for itemId: {ItemId}.", itemIdMasked);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending pickup item request for itemId {ItemId}.", itemIdMasked);
            }
        }

        /// <summary>
        /// Sends a talk-to-NPC request to the server for the specified NPC network id (masked).
        /// </summary>
        public async Task SendTalkToNpcRequestAsync(ushort npcNetworkId)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected - cannot send talk to NPC request.");
                return;
            }

            // Ensure masked id (server expects 0x7FFF range)
            ushort masked = (ushort)(npcNetworkId & 0x7FFF);

            _logger.LogInformation("Sending TalkToNpcRequest for NPC {NpcId:X4}...", masked);
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                {
                    var len = TalkToNpcRequest.Length;
                    var packet = new TalkToNpcRequest(_connectionManager.Connection.Output.GetMemory(len).Slice(0, len));
                    packet.NpcId = masked; // BigEndian is handled by the struct
                    return len;
                });
                _logger.LogInformation("TalkToNpcRequest sent for NPC {NpcId:X4}.", masked);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending TalkToNpcRequest for NPC {NpcId:X4}.", masked);
            }
        }

        /// <summary>
        /// Requests the legacy quest state list (C1 A0). In the original client this is sent after entering the game world.
        /// </summary>
        public async Task RequestLegacyQuestStateListAsync()
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected - cannot request legacy quest state list.");
                return;
            }

            _logger.LogInformation("Requesting LegacyQuestStateList (0xA0) ...");
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                {
                    var len = LegacyQuestStateRequest.Length;
                    _ = new LegacyQuestStateRequest(_connectionManager.Connection.Output.GetMemory(len).Slice(0, len));
                    return len;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending LegacyQuestStateRequest (0xA0).");
            }
        }

        /// <summary>
        /// Sends a legacy quest progress request (C1 A2). SourceMain5.2 sends NewState=1 for progression.
        /// </summary>
        public async Task SendLegacyQuestProceedRequestAsync(byte questNumber)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected - cannot send legacy quest proceed request.");
                return;
            }

            _logger.LogInformation("Sending LegacyQuestStateSetRequest (0xA2): QuestNumber={QuestNumber}", questNumber);
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                {
                    var len = LegacyQuestStateSetRequest.Length;
                    var packet = new LegacyQuestStateSetRequest(_connectionManager.Connection.Output.GetMemory(len).Slice(0, len));
                    packet.QuestNumber = questNumber;
                    packet.NewState = (LegacyQuestState)1;
                    return len;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending LegacyQuestStateSetRequest (0xA2).");
            }
        }

        /// <summary>
        /// Sends EnterOnWerewolfRequest (C1 D0 07) to enter Barracks of Balgass during 3rd class quest.
        /// </summary>
        public async Task SendEnterOnWerewolfRequestAsync()
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected - cannot send EnterOnWerewolfRequest.");
                return;
            }

            _logger.LogInformation("Sending EnterOnWerewolfRequest (0xD0/0x07) ...");
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                {
                    var len = EnterOnWerewolfRequest.Length;
                    _ = new EnterOnWerewolfRequest(_connectionManager.Connection.Output.GetMemory(len).Slice(0, len));
                    return len;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending EnterOnWerewolfRequest.");
            }
        }

        /// <summary>
        /// Sends EnterOnGatekeeperRequest (C1 D0 08) to enter Balgass Refuge during 3rd class quest.
        /// </summary>
        public async Task SendEnterOnGatekeeperRequestAsync()
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected - cannot send EnterOnGatekeeperRequest.");
                return;
            }

            _logger.LogInformation("Sending EnterOnGatekeeperRequest (0xD0/0x08) ...");
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                {
                    var len = EnterOnGatekeeperRequest.Length;
                    _ = new EnterOnGatekeeperRequest(_connectionManager.Connection.Output.GetMemory(len).Slice(0, len));
                    return len;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending EnterOnGatekeeperRequest.");
            }
        }

        /// <summary>
        /// Sends a close-NPC dialog request to the server.
        /// </summary>
        public async Task SendCloseNpcRequestAsync()
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected - cannot send close NPC request.");
                return;
            }

            _logger.LogInformation("Sending CloseNpcRequest (0x31) ...");
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                {
                    var len = CloseNpcRequest.Length;
                    var packet = new CloseNpcRequest(_connectionManager.Connection.Output.GetMemory(len).Slice(0, len));
                    return len;
                });
                _logger.LogInformation("CloseNpcRequest sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending CloseNpcRequest.");
            }
        }

        /// <summary>
        /// Sends a chaos machine mix request (C1 86) with a mix type id.
        /// </summary>
        public async Task SendChaosMachineMixRequestAsync(int mixType, byte socketSlot)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected - cannot send chaos machine mix request.");
                return;
            }

            _logger.LogInformation("Sending ChaosMachineMixRequest: MixType={MixType}, SocketSlot={SocketSlot}", mixType, socketSlot);
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                {
                    var len = ChaosMachineMixRequest.Length;
                    var packet = new ChaosMachineMixRequest(_connectionManager.Connection.Output.GetMemory(len).Slice(0, len));
                    packet.MixType = (ChaosMachineMixRequest.ChaosMachineMixType)mixType;
                    packet.SocketSlot = socketSlot;
                    return len;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending ChaosMachineMixRequest.");
            }
        }

        /// <summary>
        /// Sends CraftingDialogCloseRequest (C1 87) to notify server that the crafting dialog was closed.
        /// </summary>
        public async Task SendCraftingDialogCloseRequestAsync()
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected - cannot send crafting dialog close request.");
                return;
            }

            _logger.LogInformation("Sending CraftingDialogCloseRequest (0x87) ...");
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                {
                    var len = CraftingDialogCloseRequest.Length;
                    _ = new CraftingDialogCloseRequest(_connectionManager.Connection.Output.GetMemory(len).Slice(0, len));
                    return len;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending CraftingDialogCloseRequest.");
            }
        }

        /// <summary>
        /// Requests to move money between inventory and vault storage.
        /// </summary>
        public async Task SendVaultMoveMoneyAsync(VaultMoveMoneyRequest.VaultMoneyMoveDirection direction, uint amount)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected - cannot move vault money.");
                return;
            }

            _logger.LogInformation("Sending VaultMoveMoneyRequest: Direction={Direction}, Amount={Amount}", direction, amount);
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                {
                    var packet = new VaultMoveMoneyRequest(_connectionManager.Connection.Output.GetMemory(VaultMoveMoneyRequest.Length).Slice(0, VaultMoveMoneyRequest.Length));
                    packet.Direction = direction;
                    packet.Amount = amount;
                    return VaultMoveMoneyRequest.Length;
                });
                _logger.LogInformation("VaultMoveMoneyRequest sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending VaultMoveMoneyRequest.");
            }
        }

        /// <summary>
        /// Sends a buy request for the given NPC shop slot.
        /// </summary>
        public async Task SendBuyItemFromNpcRequestAsync(byte shopSlot)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected - cannot send buy item request.");
                return;
            }

            _logger.LogInformation("Sending BuyItemFromNpcRequest for slot {Slot}...", shopSlot);
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                {
                    var len = BuyItemFromNpcRequest.Length;
                    var packet = new BuyItemFromNpcRequest(_connectionManager.Connection.Output.GetMemory(len).Slice(0, len));
                    packet.ItemSlot = shopSlot;
                    return len;
                });
                _logger.LogInformation("BuyItemFromNpcRequest sent for slot {Slot}.", shopSlot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending BuyItemFromNpcRequest for slot {Slot}.", shopSlot);
            }
        }

        /// <summary>
        /// Sends a request to sell an item from the inventory to the currently opened NPC merchant.
        /// </summary>
        public async Task SendSellItemToNpcRequestAsync(byte inventorySlot)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected - cannot send sell item request.");
                return;
            }

            _logger.LogInformation("Sending SellItemToNpcRequest for inv slot {Slot}...", inventorySlot);
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                {
                    var len = SellItemToNpcRequest.Length;
                    var packet = new SellItemToNpcRequest(_connectionManager.Connection.Output.GetMemory(len).Slice(0, len));
                    packet.ItemSlot = inventorySlot;
                    return len;
                });
                _logger.LogInformation("SellItemToNpcRequest sent for slot {Slot}.", inventorySlot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending SellItemToNpcRequest for inv slot {Slot}.", inventorySlot);
            }
        }

        /// <summary>
        /// Sends an inventory item move request (drag and drop inside inventory) using the proper packet format for the protocol version.
        /// </summary>
        /// <param name="fromSlot">Source slot index (including server offset).</param>
        /// <param name="toSlot">Destination slot index (including server offset).</param>
        /// <param name="version">Target protocol version.</param>
        /// <param name="itemData">Raw item data for Season6 (12 bytes expected). Optional for other versions.</param>
        public async Task SendItemMoveRequestAsync(byte fromSlot, byte toSlot, TargetProtocolVersion version, byte[] itemData)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected - cannot send item move request.");
                return;
            }

            const ItemStorageKind store = ItemStorageKind.Inventory;

            try
            {
                switch (version)
                {
                    case TargetProtocolVersion.Season6:
                        if (itemData == null || itemData.Length < 12)
                        {
                            _logger.LogWarning("Season6 item move missing 12-byte item data. Falling back to extended packet.");
                            await _connectionManager.Connection.SendAsync(() =>
                                PacketBuilder.BuildItemMoveRequestExtendedPacket(
                                    _connectionManager.Connection.Output, store, fromSlot, store, toSlot));
                        }
                        else
                        {
                            await _connectionManager.Connection.SendAsync(() =>
                                PacketBuilder.BuildItemMoveRequestPacket(
                                    _connectionManager.Connection.Output, store, fromSlot, itemData, store, toSlot));
                        }
                        break;

                    case TargetProtocolVersion.Version097:
                    case TargetProtocolVersion.Version075:
                    default:
                        await _connectionManager.Connection.SendAsync(() =>
                            PacketBuilder.BuildItemMoveRequestExtendedPacket(
                                _connectionManager.Connection.Output, store, fromSlot, store, toSlot));
                        break;
                }

                _logger.LogInformation("Item move request sent: {From} -> {To}", fromSlot, toSlot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending item move request from {From} to {To}.", fromSlot, toSlot);
            }
        }

        /// <summary>
        /// Sends an item move between arbitrary storages (for example, Inventory ↔ Vault or Vault ↔ Vault).
        /// </summary>
        public async Task SendStorageItemMoveAsync(
            ItemStorageKind fromStorage,
            byte fromSlot,
            ItemStorageKind toStorage,
            byte toSlot,
            TargetProtocolVersion version,
            byte[] itemData)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected - cannot send storage item move request.");
                return;
            }

            try
            {
                switch (version)
                {
                    case TargetProtocolVersion.Season6:
                        bool hasItemData = itemData != null && itemData.Length >= 12;
                        _logger.LogDebug("Storage move (S6) using {PacketType}: {FromStore}[{From}] -> {ToStore}[{To}]",
                            hasItemData ? "ItemMoveRequest" : "ItemMoveRequestExtended",
                            fromStorage, fromSlot, toStorage, toSlot);

                        if (!hasItemData)
                        {
                            await _connectionManager.Connection.SendAsync(() =>
                                PacketBuilder.BuildItemMoveRequestExtendedPacket(
                                    _connectionManager.Connection.Output,
                                    fromStorage, fromSlot, toStorage, toSlot));
                        }
                        else
                        {
                            await _connectionManager.Connection.SendAsync(() =>
                                PacketBuilder.BuildItemMoveRequestPacket(
                                    _connectionManager.Connection.Output,
                                    fromStorage, fromSlot, itemData, toStorage, toSlot));
                        }
                        break;

                    default:
                        await _connectionManager.Connection.SendAsync(() =>
                            PacketBuilder.BuildItemMoveRequestExtendedPacket(
                                _connectionManager.Connection.Output,
                                fromStorage, fromSlot, toStorage, toSlot));
                        break;
                }

                _logger.LogInformation("Storage item move sent: {FromStore}[{From}] -> {ToStore}[{To}]",
                    fromStorage, fromSlot, toStorage, toSlot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending storage item move {FromStore}[{From}] -> {ToStore}[{To}]",
                    fromStorage, fromSlot, toStorage, toSlot);
            }
        }

        public async Task SendEnterGateRequestAsync(ushort gateId)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected — cannot send enter gate request.");
                return;
            }

            _logger.LogInformation("Sending enter gate request for gate {GateId}...", gateId);
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                {
                    var packet = new EnterGateRequest(_connectionManager.Connection.Output.GetMemory(EnterGateRequest.Length).Slice(0, EnterGateRequest.Length));
                    packet.GateNumber = gateId;
                    packet.TeleportTargetX = 0;
                    packet.TeleportTargetY = 0;
                    return EnterGateRequest.Length;
                });
                _logger.LogInformation("Enter gate request sent for gate {GateId}.", gateId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending enter gate request for gate {GateId}.", gateId);
            }
        }

        /// <summary>
        /// Sends a request to an NPC to receive a buff.
        /// Must be called after opening NPC dialog with SendTalkToNpcRequestAsync.
        /// </summary>
        public async Task SendNpcBuffRequestAsync()
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected - cannot send NPC buff request.");
                return;
            }

            _logger.LogInformation("Sending NPC buff request...");
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                {
                    var packet = new NpcBuffRequest(_connectionManager.Connection.Output.GetMemory(NpcBuffRequest.Length).Slice(0, NpcBuffRequest.Length));
                    return NpcBuffRequest.Length;
                });
                _logger.LogInformation("NPC buff request sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending NPC buff request.");
            }
        }

        /// <summary>
        /// Sends a complete sequence to get buff from Elf Soldier NPC:
        /// 1. Opens dialog with NPC (TalkToNpcRequest)
        /// 2. Requests buff (NpcBuffRequest)
        /// </summary>
        /// <param name="npcId">The network ID of the Elf Soldier NPC.</param>
        public async Task SendElfSoldierBuffSequenceAsync(ushort npcId)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected - cannot send Elf Soldier buff sequence.");
                return;
            }

            _logger.LogInformation("Sending Elf Soldier buff sequence for NPC ID {NpcId}...", npcId);

            // Step 1: Open dialog with NPC
            await SendTalkToNpcRequestAsync(npcId);

            // Step 2: Small delay to ensure dialog is processed
            await Task.Delay(100);

            // Step 3: Request buff
            await SendNpcBuffRequestAsync();

            // Step 4: Close NPC dialog
            await SendCloseNpcRequestAsync();

            _logger.LogInformation("Elf Soldier buff sequence completed for NPC ID {NpcId}.", npcId);
        }

        /// <summary>
        /// Sends a repair item request to the server (C1 34).
        /// </summary>
        /// <param name="inventorySlot">The inventory slot of the item to repair. Use 0xFF for "repair all".</param>
        /// <param name="isSelfRepair">True if repairing from inventory (costs 2.5x more), false if using NPC shop.</param>
        public async Task SendRepairItemRequestAsync(byte inventorySlot, bool isSelfRepair)
        {
            if (!_connectionManager.IsConnected)
            {
                _logger.LogError("Not connected - cannot send repair item request.");
                return;
            }

            _logger.LogInformation("Sending RepairItemRequest for slot {Slot}, selfRepair={SelfRepair}...", inventorySlot, isSelfRepair);
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                {
                    var len = RepairItemRequest.Length;
                    var packet = new RepairItemRequest(_connectionManager.Connection.Output.GetMemory(len).Slice(0, len));
                    packet.ItemSlot = inventorySlot;
                    packet.IsSelfRepair = isSelfRepair;
                    return len;
                });
                _logger.LogInformation("RepairItemRequest sent for slot {Slot}.", inventorySlot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending RepairItemRequest for slot {Slot}.", inventorySlot);
            }
        }
    }
}
