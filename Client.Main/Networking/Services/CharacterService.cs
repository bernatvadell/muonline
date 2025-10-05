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
        /// Sends an inventory item move request (drag & drop within inventory).
        /// Selects packet format based on protocol version.
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
        /// Sends an item move between arbitrary storages (e.g., Inventory <-> Vault, Vault <-> Vault).
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
                        if (itemData == null || itemData.Length < 12)
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
    }
}
