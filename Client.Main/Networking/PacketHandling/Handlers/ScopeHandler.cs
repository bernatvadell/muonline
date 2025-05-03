using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using Client.Main.Client;
using Client.Main.Core.Utilities;
using Client.Main.Core.Models;
using System;
using System.Threading.Tasks;
using System.Linq;
using MUnique.OpenMU.Network.Packets;
using Client.Main.Objects.Player;
using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Client.Main.Models;
using System.Diagnostics;

namespace Client.Main.Networking.PacketHandling.Handlers
{
    /// <summary>
    /// Handles packets related to objects entering/leaving scope, moving, and dying.
    /// </summary>
    public class ScopeHandler : IGamePacketHandler
    {
        private readonly ILogger<ScopeHandler> _logger;
        private readonly ScopeManager _scopeManager;
        private readonly CharacterState _characterState;
        private readonly NetworkManager _networkManager;
        private readonly TargetProtocolVersion _targetVersion;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScopeHandler"/> class.
        /// </summary>
        /// <param name="loggerFactory">The logger factory.</param>
        /// <param name="scopeManager">The scope manager.</param>
        /// <param name="characterState">The character state.</param>
        /// <param name="client">The simple login client instance.</param>
        /// <param name="targetVersion">The target protocol version.</param>
        public ScopeHandler(ILoggerFactory loggerFactory, ScopeManager scopeManager, CharacterState characterState, NetworkManager networkManager, TargetProtocolVersion targetVersion)
        {
            _logger = loggerFactory.CreateLogger<ScopeHandler>();
            _scopeManager = scopeManager;
            _characterState = characterState;
            _networkManager = networkManager;
            _targetVersion = targetVersion;
        }

        [PacketHandler(0x12, PacketRouter.NoSubCode)] // AddCharacterToScope
        public Task HandleAddCharacterToScopeAsync(Memory<byte> packet)
        {
            try
            {
                ParseAndAddCharactersToScope(packet);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing AddCharactersToScope (12).");
            }
            return Task.CompletedTask;
        }

        private static readonly List<PlayerScopeObject> _pendingPlayers = new();

        /// <summary>
        /// Podaj bufor do GameScene po załadowaniu świata.
        /// </summary>
        internal static List<PlayerScopeObject> TakePendingPlayers()
        {
            lock (_pendingPlayers)
            {
                var copy = _pendingPlayers.ToList();
                _pendingPlayers.Clear();
                return copy;
            }
        }
        /* -------------------------------------------------------------- */


        /* -------------------- Uaktualniony parser 0x12 ---------------- */
        private void ParseAndAddCharactersToScope(Memory<byte> packet)
        {
            var scope = new AddCharactersToScopeRef(packet.Span);

            for (int i = 0; i < scope.CharacterCount; i++)
            {
                var c = scope[i];
                CharacterClassNumber cls = ClassFromAppearance(c.Appearance);
                ushort raw = c.Id;
                ushort masked = (ushort)(raw & 0x7FFF);

                // ───────────── POMIŃ SWOJEGO GRACZA ─────────────
                if (masked == _characterState.Id)
                {
                    // tylko zaktualizuj pozycję w ScopeManagerze
                    _scopeManager.AddOrUpdatePlayerInScope(masked, raw, c.CurrentPositionX, c.CurrentPositionY, c.Name);
                    continue;
                }

                byte x = c.CurrentPositionX;
                byte y = c.CurrentPositionY;
                string name = c.Name;

                _scopeManager.AddOrUpdatePlayerInScope(masked, raw, x, y, name);

                /* Spawn – zależnie od tego, czy świat już istnieje */
                if (MuGame.Instance.ActiveScene?.World is WalkableWorldControl w &&
                    w.Status == GameControlStatus.Ready)
                {
                    SpawnRemotePlayerIntoWorld(w, masked, x, y, name, cls);
                }
                else
                {
                    lock (_pendingPlayers)
                        _pendingPlayers.Add(
                            new PlayerScopeObject(masked, raw, x, y, name, cls));
                }
            }
        }

        private static CharacterClassNumber ClassFromAppearance(ReadOnlySpan<byte> app)
        {
            if (app.Length == 0) return CharacterClassNumber.DarkWizard;
            int raw = (app[0] >> 3) & 0b1_1111;
            return raw switch
            {
                0 => CharacterClassNumber.DarkWizard,
                1 => CharacterClassNumber.SoulMaster,
                2 => CharacterClassNumber.GrandMaster,
                4 => CharacterClassNumber.DarkKnight,
                6 => CharacterClassNumber.BladeKnight,
                8 => CharacterClassNumber.FairyElf,
                10 => CharacterClassNumber.MuseElf,
                12 => CharacterClassNumber.MagicGladiator,
                16 => CharacterClassNumber.DarkLord,
                20 => CharacterClassNumber.Summoner,
                24 => CharacterClassNumber.RageFighter,
                _ => CharacterClassNumber.DarkWizard
            };
        }

        private static void SpawnRemotePlayerIntoWorld(
       WalkableWorldControl world,
       ushort maskedId, byte x, byte y, string name,
       CharacterClassNumber cls = CharacterClassNumber.DarkWizard)
        {
            // JEŚLI maskedId == Walker bieżącej mapy ⇒ to jest gracz lokalny – pomijamy
            if (world.Walker is PlayerObject local && local.NetworkId == maskedId)
                return;

            MuGame.ScheduleOnMainThread(async () =>
            {
                if (world.Objects.OfType<PlayerObject>().Any(p => p.NetworkId == maskedId))
                    return;   // już istnieje

                var p = new PlayerObject
                {
                    NetworkId = maskedId,
                    CharacterClass = cls,
                    Name = name,
                    Location = new Vector2(x, y)
                };

                world.Objects.Add(p);
                await p.Load();        // tylko Load (bez Initialize)
            });
        }

        [PacketHandler(0x13, PacketRouter.NoSubCode)] // AddNpcToScope
        public Task HandleAddNpcToScopeAsync(Memory<byte> packet)
        {
            try
            {
                ParseAndAddNpcsToScope(packet);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing AddNpcToScope (13).");
            }
            return Task.CompletedTask;
        }

        private void ParseAndAddNpcsToScope(Memory<byte> packet)
        {
            switch (_targetVersion)
            {
                case TargetProtocolVersion.Season6:
                    var scopeS6 = new AddNpcsToScope(packet);
                    // Packet length check for S6 needed
                    _logger.LogInformation("Received AddNpcToScope (S6): {Count} NPC(s).", scopeS6.NpcCount);
                    for (int i = 0; i < scopeS6.NpcCount; i++)
                    {
                        var npc = scopeS6[i];
                        ushort rawIdS6 = npc.Id;
                        ushort maskedIdS6 = (ushort)(rawIdS6 & 0x7FFF);
                        ushort typeNumber = npc.TypeNumber;
                        byte currentX = npc.CurrentPositionX;
                        byte currentY = npc.CurrentPositionY;
                        _logger.LogDebug("Parsed AddNpc: ID={MaskedId:X4}, Type={Type}, ParsedPos=({X},{Y})", maskedIdS6, typeNumber, currentX, currentY);

                        _scopeManager.AddOrUpdateNpcInScope(maskedIdS6, rawIdS6, currentX, currentY, typeNumber);
                        string npcName = NpcDatabase.GetNpcName(typeNumber);
                        var npcObj = new NpcScopeObject(maskedIdS6, rawIdS6, currentX, currentY, typeNumber, npcName);
                        // _networkManager.ViewModel.AddOrUpdateMapObject(npcObj);
                        _logger.LogInformation("👀 {NpcDesignation} (ID: {MaskedId:X4}) appeared at ({X},{Y}).", npcName, maskedIdS6, currentX, currentY);
                    }
                    break;
                case TargetProtocolVersion.Version097:
                    var scope097 = new AddNpcsToScope095(packet);
                    // Packet length check for 0.97 needed
                    _logger.LogInformation("Received AddNpcToScope (0.97): {Count} NPC(s).", scope097.NpcCount);
                    for (int i = 0; i < scope097.NpcCount; i++)
                    {
                        var npc = scope097[i];
                        ushort rawId097 = npc.Id;
                        ushort maskedId097 = (ushort)(rawId097 & 0x7FFF);
                        ushort typeNumber = npc.TypeNumber;
                        byte currentX = npc.CurrentPositionX;
                        byte currentY = npc.CurrentPositionY;
                        _logger.LogDebug("Parsed AddNpc: ID={MaskedId:X4}, Type={Type}, ParsedPos=({X},{Y})", maskedId097, typeNumber, currentX, currentY);

                        _scopeManager.AddOrUpdateNpcInScope(maskedId097, rawId097, currentX, currentY, typeNumber);
                        string npcName = NpcDatabase.GetNpcName(typeNumber);
                        var npcObj = new NpcScopeObject(maskedId097, rawId097, currentX, currentY, typeNumber, npcName);
                        // _networkManager.ViewModel.AddOrUpdateMapObject(npcObj);
                        _logger.LogInformation("👀 {NpcDesignation} (ID: {MaskedId:X4}) appeared at ({X},{Y}).", npcName, maskedId097, currentX, currentY);
                    }
                    break;
                case TargetProtocolVersion.Version075:
                    var scope075 = new AddNpcsToScope075(packet);
                    // Packet length check for 0.75 needed
                    _logger.LogInformation("Received AddNpcToScope (0.75): {Count} NPC(s).", scope075.NpcCount);
                    for (int i = 0; i < scope075.NpcCount; i++)
                    {
                        var npc = scope075[i];
                        ushort rawId075 = npc.Id;
                        ushort maskedId075 = (ushort)(rawId075 & 0x7FFF);
                        ushort typeNumber = npc.TypeNumber;
                        byte currentX = npc.CurrentPositionX;
                        byte currentY = npc.CurrentPositionY;
                        _logger.LogDebug("Parsed AddNpc: ID={MaskedId:X4}, Type={Type}, ParsedPos=({X},{Y})", maskedId075, typeNumber, currentX, currentY);

                        _scopeManager.AddOrUpdateNpcInScope(maskedId075, rawId075, currentX, currentY, typeNumber);
                        string npcName = NpcDatabase.GetNpcName(typeNumber);
                        var npcObj = new NpcScopeObject(maskedId075, rawId075, currentX, currentY, typeNumber, npcName);
                        // _networkManager.ViewModel.AddOrUpdateMapObject(npcObj);
                        _logger.LogInformation("👀 {NpcDesignation} (ID: {MaskedId:X4}) appeared at ({X},{Y}).", npcName, maskedId075, currentX, currentY);
                    }
                    break;
                default:
                    _logger.LogWarning("Unsupported protocol version ({Version}) for AddNpcToScope.", _targetVersion);
                    break;
            }
            // _networkManager.ViewModel.UpdateScopeDisplay();
        }

        [PacketHandler(0x20, PacketRouter.NoSubCode)] // ItemsDropped / MoneyDropped075
        public Task HandleItemsDroppedAsync(Memory<byte> packet)
        {
            try
            {
                ParseAndAddDroppedItemsToScope(packet);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing ItemsDropped (20).");
            }
            return Task.CompletedTask;
        }

        private void ParseAndAddDroppedItemsToScope(Memory<byte> packet)
        {
            const int ItemsDroppedFixedHeaderSize = 4; // Header P(size) C(code) count
            const int ItemsDroppedFixedPrefixSize = ItemsDroppedFixedHeaderSize + 1; // +1 for the count byte itself

            if (_targetVersion >= TargetProtocolVersion.Season6)
            {
                if (packet.Length < ItemsDroppedFixedPrefixSize)
                {
                    _logger.LogWarning("ItemsDropped packet (0x20, S6+) too short for header. Length: {Length}", packet.Length);
                    return;
                }
                var droppedItems = new ItemsDropped(packet);
                _logger.LogInformation("Received ItemsDropped (S6+): {Count} item(s).", droppedItems.ItemCount);

                // Determine the actual item data length for the specific version
                // S6 items are typically 12 bytes. Older versions might be 7 or 8.
                // This could be derived from the packet structure definition if available.
                // Hardcoding 12 for S6 example, but this might need refinement per version.
                int actualItemDataLength = 12;
                int expectedItemStructSize = ItemsDropped.DroppedItem.GetRequiredSize(actualItemDataLength);

                int currentOffset = ItemsDroppedFixedPrefixSize;
                for (int i = 0; i < droppedItems.ItemCount; i++)
                {
                    if (currentOffset + expectedItemStructSize > packet.Length)
                    {
                        _logger.LogWarning("Packet too short for DroppedItem {Index} (Offset: {Offset}, Size: {Size}, PacketLen: {PacketLen}).", i, currentOffset, expectedItemStructSize, packet.Length);
                        break;
                    }

                    var itemMemory = packet.Slice(currentOffset, expectedItemStructSize);
                    var item = new ItemsDropped.DroppedItem(itemMemory);
                    ushort rawId = item.Id;
                    ushort maskedId = (ushort)(rawId & 0x7FFF);
                    byte x = item.PositionX;
                    byte y = item.PositionY;
                    ReadOnlySpan<byte> itemData = item.ItemData; // Get ReadOnlySpan from item data

                    _logger.LogDebug("Parsed DroppedItem/Money: ID={MaskedId:X4}, ParsedPos=({X},{Y})", maskedId, x, y);

                    // Check if it's Zen based on common item data structure (Group 14, Type 15)
                    bool isMoney = itemData.Length >= 6 && itemData[0] == 15 && (itemData[5] >> 4) == 14;
                    uint moneyAmount = 0;
                    ScopeObject dropObj;

                    if (isMoney)
                    {
                        // Money amount is typically at index 4 in the item data bytes for Zen
                        if (itemData.Length >= 5)
                        {
                            moneyAmount = itemData[4];
                        }
                        else
                        {
                            _logger.LogWarning("Item data too short to read money amount for ID {MaskedId:X4}.", maskedId);
                            // Default to 0 or log error? For now, proceed with amount 0.
                        }

                        dropObj = new MoneyScopeObject(maskedId, rawId, x, y, moneyAmount);
                        _scopeManager.AddOrUpdateMoneyInScope(maskedId, rawId, x, y, moneyAmount);
                        _logger.LogDebug("Dropped Money (S6+): Amount={Amount}, RawID={RawId:X4}, MaskedID={MaskedId:X4}, Pos=({X},{Y})", moneyAmount, rawId, maskedId, x, y);
                    }
                    else
                    {
                        dropObj = new ItemScopeObject(maskedId, rawId, x, y, itemData.ToArray()); // Store a copy of item data
                        _scopeManager.AddOrUpdateItemInScope(maskedId, rawId, x, y, itemData.ToArray());
                        _logger.LogDebug("Dropped Item (S6+): RawID={RawId:X4}, MaskedID={MaskedId:X4}, Pos=({X},{Y}), DataLen={DataLen}", rawId, maskedId, x, y, itemData.Length);
                    }

                    currentOffset += expectedItemStructSize;
                    // _networkManager.ViewModel.AddOrUpdateMapObject(dropObj);
                }
            }
            else if (_targetVersion == TargetProtocolVersion.Version075)
            {
                // Version 0.75 uses a different packet structure for dropped items (0x20 is often only for single items/money)
                if (packet.Length < MoneyDropped075.Length)
                {
                    _logger.LogWarning("Dropped Object packet (0.75, 0x20) too short. Length: {Length}", packet.Length);
                    return;
                }
                var droppedObjectLegacy = new MoneyDropped075(packet);
                _logger.LogInformation("Received Dropped Object (0.75): Count={Count}.", droppedObjectLegacy.ItemCount);

                // 0.75 packet structure for 0x20 seems to only contain one object
                if (droppedObjectLegacy.ItemCount == 1)
                {
                    ushort rawId = droppedObjectLegacy.Id;
                    ushort maskedId = (ushort)(rawId & 0x7FFF);
                    byte x = droppedObjectLegacy.PositionX;
                    byte y = droppedObjectLegacy.PositionY;
                    _logger.LogDebug("Parsed DroppedItem/Money (0.75): ID={MaskedId:X4}, ParsedPos=({X},{Y})", maskedId, x, y);

                    ScopeObject dropObj;
                    // Check if it's money based on Group and Type in the packet itself
                    if (droppedObjectLegacy.MoneyGroup == 14 && droppedObjectLegacy.MoneyNumber == 15)
                    {
                        uint amount = droppedObjectLegacy.Amount;
                        dropObj = new MoneyScopeObject(maskedId, rawId, x, y, amount);
                        _scopeManager.AddOrUpdateMoneyInScope(maskedId, rawId, x, y, amount);
                        _logger.LogDebug("Dropped Money (0.75): RawID={RawId:X4}, MaskedID={MaskedId:X4}, Pos=({X},{Y}), Amount={Amount}", rawId, maskedId, x, y, amount);
                    }
                    else
                    {
                        // Item data for 0.75 is in the same packet structure
                        const int itemDataOffset = 9; // Offset where 0.75 item data starts
                        const int itemDataLength075 = 7; // Length of 0.75 item data bytes
                        if (packet.Length >= itemDataOffset + itemDataLength075)
                        {
                            ReadOnlySpan<byte> itemData = packet.Span.Slice(itemDataOffset, itemDataLength075);
                            dropObj = new ItemScopeObject(maskedId, rawId, x, y, itemData.ToArray());
                            _scopeManager.AddOrUpdateItemInScope(maskedId, rawId, x, y, itemData.ToArray());
                            _logger.LogDebug("Dropped Item (0.75): RawID={RawId:X4}, MaskedID={MaskedId:X4}, Pos=({X},{Y}), DataLen={DataLen}", rawId, maskedId, x, y, itemData.Length);
                        }
                        else
                        {
                            _logger.LogWarning("Could not extract expected item data from Dropped Object packet (0.75).");
                            return; // Cannot create map object without data
                        }
                    }
                    // _networkManager.ViewModel.AddOrUpdateMapObject(dropObj); // Add to map only if created
                }
                else
                {
                    _logger.LogWarning("Dropped Object (0.75): Multiple objects in one packet not handled (Count={Count}).", droppedObjectLegacy.ItemCount);
                }
            }
            else
            {
                _logger.LogWarning("Unsupported protocol version ({Version}) for ItemsDropped (20).", _targetVersion);
            }
            // _networkManager.ViewModel.UpdateScopeDisplay();
        }

        [PacketHandler(0x21, PacketRouter.NoSubCode)] // ItemDropRemoved
        public Task HandleItemDropRemovedAsync(Memory<byte> packet)
        {
            try
            {
                ParseAndRemoveDroppedItemsFromScope(packet);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing ItemDropRemoved (21).");
            }
            return Task.CompletedTask;
        }

        private void ParseAndRemoveDroppedItemsFromScope(Memory<byte> packet)
        {
            const int ItemDropRemovedFixedHeaderSize = 4; // P(size) C(code) ...
            const int ItemDropRemovedFixedPrefixSize = ItemDropRemovedFixedHeaderSize + 1; // +1 for count byte

            if (packet.Length < ItemDropRemovedFixedPrefixSize)
            {
                _logger.LogWarning("ItemDropRemoved packet (0x21) too short for header. Length: {Length}", packet.Length);
                return;
            }

            var itemDropRemoved = new ItemDropRemoved(packet);
            byte count = itemDropRemoved.ItemCount;
            _logger.LogInformation("Received ItemDropRemoved: {Count} item(s).", count);

            const int objectIdSize = 2; // Each removed object is identified by a 2-byte ID
            int expectedTotalLength = ItemDropRemovedFixedPrefixSize + count * objectIdSize;

            // Safety check for packet length consistency with reported count
            if (packet.Length < expectedTotalLength)
            {
                _logger.LogWarning("ItemDropRemoved packet (0x21) seems too short for {Count} items. Length: {Length}, Expected: {Expected}", count, packet.Length, expectedTotalLength);
                // Adjust count to the maximum possible based on packet length
                count = (byte)((packet.Length - ItemDropRemovedFixedPrefixSize) / objectIdSize);
                _logger.LogWarning("Adjusting count to {AdjustedCount} based on length.", count);
            }

            for (int i = 0; i < count; i++)
            {
                try
                {
                    // Accessing items via indexer might throw if index is out of bounds
                    // The packet structure provides an indexer that reads from the Memory<byte>
                    var droppedItemIdStruct = itemDropRemoved[i];
                    ushort idFromServerRaw = droppedItemIdStruct.Id;
                    ushort idFromServerMasked = (ushort)(idFromServerRaw & 0x7FFF);

                    // Attempt to get the object's name from scope before removing it
                    string objectName = "Object";
                    if (_scopeManager.TryGetScopeObjectName(idFromServerRaw, out var name))
                    {
                        objectName = name ?? objectName; // Use found name, fallback to "Object"
                    }

                    // Remove the object from the scope manager and log success/failure
                    if (_scopeManager.RemoveObjectFromScope(idFromServerMasked))
                    {
                        _logger.LogInformation("💨 {ObjectName} (ID: {MaskedId:X4}) disappeared from view.", objectName, idFromServerMasked);
                    }
                    else
                    {
                        _logger.LogDebug("Attempted to remove object {MaskedId:X4} from scope (ItemDropRemoved packet), but it was not found.", idFromServerMasked);
                    }

                    // Remove the object from the map display in the UI
                    // _networkManager.ViewModel.RemoveMapObject(idFromServerMasked);
                }
                catch (IndexOutOfRangeException ex)
                {
                    _logger.LogError(ex, "Index out of range while processing item removal at index {Index} in ItemDropRemoved (21). Packet length: {PacketLen}, Count: {Count}", i, packet.Length, count);
                    break; // Stop processing if index is invalid
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing item removal at index {Index} in ItemDropRemoved (21).", i);
                }
            }
            // _networkManager.ViewModel.UpdateScopeDisplay();
        }

        [PacketHandler(0x2F, PacketRouter.NoSubCode)] // MoneyDroppedExtended (Season 6+)
        public Task HandleMoneyDroppedExtendedAsync(Memory<byte> packet)
        {
            // This packet is typically for Zen dropped directly (e.g., from character inventory)
            try
            {
                if (packet.Length < MoneyDroppedExtended.Length)
                {
                    _logger.LogWarning("MoneyDroppedExtended packet (0x2F) too short. Length: {Length}", packet.Length);
                    return Task.CompletedTask;
                }
                var moneyDrop = new MoneyDroppedExtended(packet);
                ushort rawId = moneyDrop.Id;
                ushort maskedId = (ushort)(rawId & 0x7FFF);
                uint amount = moneyDrop.Amount;
                byte x = moneyDrop.PositionX;
                byte y = moneyDrop.PositionY;

                _scopeManager.AddOrUpdateMoneyInScope(maskedId, rawId, x, y, amount);

                var moneyObj = new MoneyScopeObject(maskedId, rawId, x, y, amount);
                // _networkManager.ViewModel.AddOrUpdateMapObject(moneyObj);

                _logger.LogInformation("💰 Received MoneyDroppedExtended (2F): RawID={RawId:X4}, MaskedID={MaskedId:X4}, Amount={Amount}, Pos=({X},{Y})", rawId, maskedId, amount, x, y);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing MoneyDroppedExtended (2F).");
            }
            // _networkManager.ViewModel.UpdateScopeDisplay(); // Money drop should also update scope display
            return Task.CompletedTask;
        }

        [PacketHandler(0x14, PacketRouter.NoSubCode)]
        public Task HandleMapObjectOutOfScopeAsync(Memory<byte> packet)
        {
            var outPkt = new MapObjectOutOfScope(packet);
            int count = outPkt.ObjectCount;

            MuGame.ScheduleOnMainThread(() =>
            {
                if (MuGame.Instance.ActiveScene?.World is not WalkableWorldControl world) return;

                for (int i = 0; i < count; i++)
                {
                    ushort raw = outPkt[i].Id;
                    ushort id = (ushort)(raw & 0x7FFF);

                    // 1) usuwamy z ScopeManagera
                    _scopeManager.RemoveObjectFromScope(id);

                    // 2) usuwamy z listy renderowanej
                    var remote = world.Objects
                        .OfType<PlayerObject>()
                        .FirstOrDefault(p => p.NetworkId == id);
                    if (remote != null)
                    {
                        world.Objects.Remove(remote);
                    }
                }
            });

            return Task.CompletedTask;
        }

        [PacketHandler(0x15, PacketRouter.NoSubCode)] // ObjectMoved (Instant Move / Teleport)
        public Task HandleObjectMovedAsync(Memory<byte> packet)
        {
            ushort objectIdMasked = 0xFFFF;
            try
            {
                if (packet.Length < ObjectMoved.Length)
                {
                    _logger.LogWarning("ObjectMoved packet (0x15) too short. Length: {Length}", packet.Length);
                    return Task.CompletedTask;
                }

                var move = new ObjectMoved(packet);
                ushort objectIdRaw = move.ObjectId;
                objectIdMasked = (ushort)(objectIdRaw & 0x7FFF);
                byte x = move.PositionX;
                byte y = move.PositionY;
                _logger.LogDebug("Parsed ObjectMoved: ID={MaskedId:X4}, ParsedPos=({X},{Y})", objectIdMasked, x, y);

                // Update position in the scope manager
                if (_scopeManager.TryUpdateScopeObjectPosition(objectIdMasked, x, y))
                {
                    _logger.LogTrace("Updated position for {Id:X4} in scope.", objectIdMasked);
                }
                else
                {
                    _logger.LogTrace("Object {Id:X4} not found in scope for position update (or not tracked).", objectIdMasked);
                }

                // Update position on the map display
                // _networkManager.ViewModel.UpdateMapObjectPosition(objectIdMasked, x, y);

                // If it's our character
                if (objectIdMasked == _characterState.Id)
                {
                    _logger.LogInformation("🏃‍♂️ Character teleported/moved to ({X}, {Y}) via 0x15", x, y);
                    _characterState.UpdatePosition(x, y);
                    // _networkManager.UpdateConsoleTitle();
                    // _networkManager.ViewModel.UpdateCharacterStateDisplay();
                    // _networkManager.SignalMovementHandled(); // Signal that movement is complete
                }
                // Update scope display in UI
                // _networkManager.ViewModel.UpdateScopeDisplay();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing ObjectMoved (15).");
            }
            finally
            {
                // Ensure movement lock is released if this was our character's move packet
                // This is a safeguard; SignalMovementHandled() is the primary mechanism.
                if (objectIdMasked == _characterState.Id)
                {
                    // _networkManager.SignalMovementHandledIfWalking(); // This method checks the state before signaling
                }
            }
            return Task.CompletedTask;
        }


        [PacketHandler(0xD4, PacketRouter.NoSubCode)]
        public Task HandleObjectWalkedAsync(Memory<byte> packet)
        {
            const int ExpectedLen = 7;
            if (packet.Length < ExpectedLen)
                return Task.CompletedTask;

            var walk = new ObjectWalked(packet);
            ushort raw = walk.ObjectId;
            ushort id = (ushort)(raw & 0x7FFF);
            byte tx = walk.TargetX;
            byte ty = walk.TargetY;

            MuGame.ScheduleOnMainThread(() =>
            {
                if (MuGame.Instance.ActiveScene?.World is not WalkableWorldControl world)
                    return;

                // jeżeli to nie jest lokalny gracz – aktualizujemy tylko wizualnie
                var remote = world.Objects
                                  .OfType<PlayerObject>()
                                  .FirstOrDefault(p => p.NetworkId == id);

                if (remote != null)
                {
                    remote.MoveTo(new Vector2(tx, ty), sendToServer: false);
                }
                // lokalny gracz?  pozycję zaktualizuje CharacterService
            });

            return Task.CompletedTask;
        }

        [PacketHandler(0x17, PacketRouter.NoSubCode)] // ObjectGotKilled
        public Task HandleObjectGotKilledAsync(Memory<byte> packet)
        {
            try
            {
                if (packet.Length < ObjectGotKilled.Length)
                {
                    _logger.LogWarning("Received ObjectGotKilled packet (0x17) with unexpected length {Length}.", packet.Length);
                    return Task.CompletedTask;
                }
                var deathInfo = new ObjectGotKilled(packet);
                ushort killedIdRaw = deathInfo.KilledId;
                ushort killerIdRaw = deathInfo.KillerId;
                ushort killedIdMasked = (ushort)(killedIdRaw & 0x7FFF);
                ushort killerIdMasked = (ushort)(killerIdRaw & 0x7FFF); // Mask killer ID too

                // Try to get names from scope (might not exist if they were out of scope when packet was sent)
                string killerName = _scopeManager.TryGetScopeObjectName(killerIdRaw, out var kn) ? (kn ?? "Unknown") : "Unknown Killer";
                string killedName = _scopeManager.TryGetScopeObjectName(killedIdRaw, out var kdn) ? (kdn ?? "Unknown Object") : "Unknown Object";

                // Log the death event
                if (killedIdMasked == _characterState.Id)
                {
                    _logger.LogWarning("💀 YOU DIED! Killed by {KillerName} (ID: {KillerId:X4}).", killerName, killerIdRaw);
                    _characterState.UpdateCurrentHealthShield(0, 0); // Set HP to 0
                    // _networkManager.SignalMovementHandledIfWalking(); // Release movement lock on death
                    // _networkManager.UpdateConsoleTitle();
                }
                else
                {
                    _logger.LogInformation("💀 {KilledName} (ID: {KilledId:X4}) died. Killed by {KillerName} (ID: {KillerId:X4}).", killedName, killedIdRaw, killerName, killerIdRaw);
                }

                // Remove the killed object from scope manager and map display
                _scopeManager.RemoveObjectFromScope(killedIdMasked);
                // _networkManager.ViewModel.RemoveMapObject(killedIdMasked);

                // Update UI displays that might be affected by HP change or scope change
                // _networkManager.ViewModel.UpdateCharacterStateDisplay();
                // _networkManager.ViewModel.UpdateScopeDisplay();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing ObjectGotKilled (0x17).");
            }
            return Task.CompletedTask;
        }


        [PacketHandler(0x18, PacketRouter.NoSubCode)]          // ObjectAnimation
        public Task HandleObjectAnimationAsync(Memory<byte> packet)
        {
            try
            {
                if (packet.Length < ObjectAnimation.Length)
                {
                    _logger.LogWarning("ObjectAnimation packet (0x18) too short. Length: {Length}", packet.Length);
                    return Task.CompletedTask;
                }

                var anim = new ObjectAnimation(packet);
                ushort id = (ushort)(anim.ObjectId & 0x7FFF);

                // 0 = stop, wszystko inne = ruch (MU tak właśnie nadaje)
                bool walking = anim.Animation != 0;

                // --- LOCAL PLAYER ------------------------------------------------
                if (id == _characterState.Id)
                {
                    // wystarczy zaktualizować CharacterState,
                    // PlayerObject localny sam dobierze animację w Update()
                    _logger.LogInformation("🏃‍♂️ Local animation → {Anim} (dir {Dir})", anim.Animation, anim.Direction);
                    return Task.CompletedTask;
                }

                // --- REMOTE PLAYER ----------------------------------------------
                // szukamy obiektu w aktualnym świecie
                if (MuGame.Instance.ActiveScene?.World is WalkableWorldControl world)
                {
                    var remote = world.Objects
                                      .OfType<PlayerObject>()
                                      .FirstOrDefault(p => p.NetworkId == id);

                    if (remote != null)
                    {
                        remote.CurrentAction = walking
                            ? PlayerAction.WalkMale     // lub Run/Fly → w razie potrzeby mapuj kody animacji
                            : PlayerAction.StopMale;

                        // jeżeli serwer przesyła kierunek, ustaw go również
                        remote.Direction = (Models.Direction)anim.Direction;
                    }
                }

                _logger.LogDebug("Remote {Id:X4} anim={Anim} dir={Dir}", id, anim.Animation, anim.Direction);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing ObjectAnimation (0x18).");
            }

            return Task.CompletedTask;
        }

        [PacketHandler(0x65, PacketRouter.NoSubCode)] // AssignCharacterToGuild
        public Task HandleAssignCharacterToGuildAsync(Memory<byte> packet)
        {
            try
            {
                var assign = new AssignCharacterToGuild(packet);
                _logger.LogInformation("🛡️ Received AssignCharacterToGuild: {Count} players.", assign.PlayerCount);
                for (int i = 0; i < assign.PlayerCount; i++)
                {
                    var relation = assign[i];
                    ushort playerIdRaw = relation.PlayerId;
                    ushort playerIdMasked = (ushort)(playerIdRaw & 0x7FFF);
                    _logger.LogDebug("Player {PlayerId:X4} (Raw: {RawId:X4}) assigned to Guild {GuildId}, Role {Role}", playerIdMasked, playerIdRaw, relation.GuildId, relation.Role);
                    // TODO: Update player's guild info in ScopeManager if needed
                    // Example: _scopeManager.UpdatePlayerGuildInfo(playerIdMasked, relation.GuildId, relation.Role);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing AssignCharacterToGuild (65).");
            }
            return Task.CompletedTask;
        }

        [PacketHandler(0x5D, PacketRouter.NoSubCode)] // GuildMemberLeftGuild
        public Task HandleGuildMemberLeftGuildAsync(Memory<byte> packet)
        {
            try
            {
                if (packet.Length < GuildMemberLeftGuild.Length)
                {
                    _logger.LogWarning("GuildMemberLeftGuild packet (0x5D) too short. Length: {Length}", packet.Length);
                    return Task.CompletedTask;
                }
                var left = new GuildMemberLeftGuild(packet);
                ushort playerIdRaw = left.PlayerId;
                ushort playerIdMasked = (ushort)(playerIdRaw & 0x7FFF);
                _logger.LogInformation("🚶 Player {PlayerId:X4} (Raw: {RawId:X4}) left guild (Is GM: {IsGM}).", playerIdMasked, playerIdRaw, left.IsGuildMaster);
                // TODO: Update player's guild info in ScopeManager if needed (e.g., set GuildId to 0, Role to Undefined)
                // Example: _scopeManager.UpdatePlayerGuildInfo(playerIdMasked, 0, GuildMemberRole.Undefined);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing GuildMemberLeftGuild (5D).");
            }
            return Task.CompletedTask;
        }

        // Add other scope-related handlers here (e.g., 1F AddSummonedMonstersToScope)
        // For monsters (0x16 AddMonstersToScope), similar logic to AddNpcToScope would apply.
        // Need to parse the monster packet structure and add them to ScopeManager and ViewModel.
    }
}