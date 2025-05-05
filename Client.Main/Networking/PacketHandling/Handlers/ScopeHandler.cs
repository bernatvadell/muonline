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
using Client.Main.Objects.Monsters;
using Client.Main.Objects.NPCS;
using Client.Main.Objects;
using Client.Main.Objects.Effects;

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
        private readonly TargetProtocolVersion _targetVersion;
        private static readonly List<NpcScopeObject> _pendingNpcsMonsters = new();


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
                ushort raw = c.Id;
                ushort masked = (ushort)(raw & 0x7FFF);
                CharacterClassNumber cls = ClassFromAppearance(c.Appearance);

                // Pomiń własnego gracza
                if (masked == _characterState.Id)
                {
                    _scopeManager.AddOrUpdatePlayerInScope(masked, raw, c.CurrentPositionX, c.CurrentPositionY, c.Name);
                    continue;
                }

                // Dodaj do scope zawsze po masked ID
                _scopeManager.AddOrUpdatePlayerInScope(masked, raw, c.CurrentPositionX, c.CurrentPositionY, c.Name);

                /* Spawn – zależnie od tego, czy świat już istnieje */
                if (MuGame.Instance.ActiveScene?.World is WalkableWorldControl w &&
                    w.Status == GameControlStatus.Ready)
                {
                    SpawnRemotePlayerIntoWorld(w, masked, c.CurrentPositionX, c.CurrentPositionY, c.Name, cls);
                }
                else
                {
                    lock (_pendingPlayers)
                        _pendingPlayers.Add(
                            new PlayerScopeObject(masked, raw, c.CurrentPositionX, c.CurrentPositionY, c.Name, cls));
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
            // Run the parsing and staggered spawning on a background thread
            _ = Task.Run(() => ParseAndAddNpcsToScopeWithStaggering(packet.ToArray()));
            return Task.CompletedTask;
        }

        [PacketHandler(0x16, PacketRouter.NoSubCode)]   // AddMonstersToScope
        public Task HandleAddMonstersToScopeAsync(Memory<byte> packet)
        {
            _ = Task.Run(() => ParseAndAddNpcsToScopeWithStaggering(packet.ToArray()));
            return Task.CompletedTask;
        }

        private async Task ParseAndAddNpcsToScopeWithStaggering(byte[] packetData)
        {
            Memory<byte> packet = packetData;
            int npcCount = 0, firstNpcOffset = 0, npcDataSize = 0;
            Func<Memory<byte>, (ushort id, ushort type, byte x, byte y)> readNpc = null!;

            // ---------- nagłówek zależny od wersji ---------------------------------
            switch (_targetVersion)
            {
                case TargetProtocolVersion.Season6:
                    var s6 = new AddNpcsToScope(packet);
                    npcCount = s6.NpcCount;
                    firstNpcOffset = 5;
                    npcDataSize = AddNpcsToScope.NpcData.Length;
                    readNpc = m => { var d = new AddNpcsToScope.NpcData(m); return (d.Id, d.TypeNumber, d.CurrentPositionX, d.CurrentPositionY); };
                    break;

                case TargetProtocolVersion.Version097:
                    var v97 = new AddNpcsToScope095(packet);
                    npcCount = v97.NpcCount;
                    firstNpcOffset = 5;
                    npcDataSize = AddNpcsToScope095.NpcData.Length;
                    readNpc = m => { var d = new AddNpcsToScope095.NpcData(m); return (d.Id, d.TypeNumber, d.CurrentPositionX, d.CurrentPositionY); };
                    break;

                case TargetProtocolVersion.Version075:
                    var v75 = new AddNpcsToScope075(packet);
                    npcCount = v75.NpcCount;
                    firstNpcOffset = 5;
                    npcDataSize = AddNpcsToScope075.NpcData.Length;
                    readNpc = m => { var d = new AddNpcsToScope075.NpcData(m); return (d.Id, d.TypeNumber, d.CurrentPositionX, d.CurrentPositionY); };
                    break;

                default:
                    _logger.LogWarning("Unsupported protocol version {Version} for AddNpcToScope.", _targetVersion);
                    return;
            }
            _logger.LogInformation("AddNpcToScope → {Count} obiektów.", npcCount);

            // ---------- iteracja po obiektach ---------------------------------------
            int offset = firstNpcOffset;
            for (int i = 0; i < npcCount; i++, offset += npcDataSize)
            {
                var (rawId, type, x, y) = readNpc(packet.Slice(offset));
                ushort maskedId = (ushort)(rawId & 0x7FFF);
                string name = NpcDatabase.GetNpcName(type);

                _scopeManager.AddOrUpdateNpcInScope(maskedId, rawId, x, y, type, name);

                WorldControl w = null;
                bool worldReady = MuGame.Instance.ActiveScene?.World is WorldControl worldControl && (w = worldControl).Status == GameControlStatus.Ready;

                if (worldReady && NpcDatabase.TryGetNpcType(type, out var cls))
                {
                    //  SPOWN OD RAZU NA MAPIE  ------------------------------------------------
                    var worldRef = w; // capture for lambda
                    MuGame.ScheduleOnMainThread(async () =>
                    {
                        if (worldRef.Objects.OfType<WalkerObject>().Any(o => o.NetworkId == maskedId))
                            return;

                        if (Activator.CreateInstance(cls) is WalkerObject obj)
                        {
                            obj.NetworkId = maskedId;
                            obj.Location = new Vector2(x, y);
                            worldRef.Objects.Add(obj);
                            await obj.Load();
                        }
                    });
                    await Task.Delay(50);     // delikatne rozłożenie w czasie
                }
                else
                {
                    //  MAPA JESZCZE SIĘ ŁADUJE → wrzuć do bufora ------------------------
                    lock (_pendingNpcsMonsters)
                        _pendingNpcsMonsters.Add(new NpcScopeObject(maskedId, rawId, x, y, type, name));
                }
            }
        }

        [PacketHandler(0x11, PacketRouter.NoSubCode)] // ObjectHit / ObjectGotHit
        public Task HandleObjectHitAsync(Memory<byte> packet)
        {
            try
            {
                // 1) wstępne parsowanie pakietu
                if (packet.Length < ObjectHit.Length)
                {
                    _logger.LogWarning("ObjectHit packet (0x11) too short. Length: {Length}", packet.Length);
                    return Task.CompletedTask;
                }

                var hitInfo = new ObjectHit(packet);
                ushort rawId = hitInfo.ObjectId;
                ushort maskedId = (ushort)(rawId & 0x7FFF); // Zawsze maskujemy ID do logowania i porównań
                uint healthDmg = hitInfo.HealthDamage;
                uint shieldDmg = hitInfo.ShieldDamage;
                uint totalDmg = healthDmg + shieldDmg;

                // 2) logujemy w konsoli (używamy maskedId do wyszukania nazwy)
                string objectName = _scopeManager.TryGetScopeObjectName(maskedId, out var nm) // Szukamy po maskedId
                                        ? (nm ?? "Object")
                                        : "Object";
                _logger.LogInformation("💥 {ObjectName} (ID: {Id:X4}) received hit! HP Dmg: {HpDmg}, SD Dmg: {SdDmg}",
                                         objectName, maskedId, healthDmg, shieldDmg);

                // 3) tworzymy tekst obrażeń na głowie ofiary (na głównym wątku)
                MuGame.ScheduleOnMainThread(() =>
                {
                    // Pobierz świat wewnątrz akcji na głównym wątku
                    if (MuGame.Instance.ActiveScene?.World is not WorldControl world)
                    {
                        _logger.LogWarning("Cannot show damage text: Active world is null or not WorldControl.");
                        return;
                    }

                    WalkerObject target = null;

                    // --- POPRAWIONA LOGIKA WYSZUKIWANIA ---
                    // Sprawdź NAJPIERW, czy ID pasuje do ID lokalnego gracza ZAPISANEGO W STANIE
                    if (maskedId == _characterState.Id && world is WalkableWorldControl walkableWorld)
                    {
                        // Jeśli tak, użyj referencji do lokalnego gracza
                        target = walkableWorld.Walker;
                        if (target == null)
                        {
                            _logger.LogWarning("Local player (ID {Id:X4}) hit, but world.Walker is null.", maskedId);
                            return; // Nie można kontynuować bez obiektu gracza
                        }
                        _logger.LogTrace("Damage target identified as local player (ID {Id:X4}).", maskedId);
                    }
                    else
                    {
                        // Jeśli to nie lokalny gracz, spróbuj znaleźć inny obiekt WalkerObject po ID
                        if (!world.TryGetWalkerById(maskedId, out target)) // Używamy metody pomocniczej
                        {
                            _logger.LogWarning("Cannot find Walker {Id:X4} using TryGetWalkerById to show damage text.", maskedId);
                            return; // Nie znaleziono obiektu
                        }
                        _logger.LogTrace("Damage target identified as remote walker (ID {Id:X4}, Type: {Type}).", maskedId, target.GetType().Name);
                    }
                    // --- KONIEC POPRAWIONEJ LOGIKI WYSZUKIWANIA ---

                    // Jeśli mamy poprawny cel (target != null)
                    // pozycja nieco nad głową
                    var headPos = target.WorldPosition.Translation
                                + Vector3.UnitZ * (target.BoundingBoxWorld.Max.Z - target.WorldPosition.Translation.Z + 30f);

                    var txt = new DamageTextObject(
                        totalDmg == 0 ? "Miss" : totalDmg.ToString(),
                        headPos,
                        totalDmg == 0 ? Color.White : Color.Red);

                    world.Objects.Add(txt); // Dodaj obiekt tekstu obrażeń do świata
                    _logger.LogDebug("Spawned DamageTextObject '{Text}' for target ID {Id:X4}", txt.Text, maskedId);
                });

                // 4) aktualizacja stanu HP/SD jeśli to nasz gracz (bez zmian)
                if (maskedId == _characterState.Id)
                {
                    uint newHp = (uint)Math.Max(0, (int)_characterState.CurrentHealth - (int)healthDmg);
                    uint newSd = (uint)Math.Max(0, (int)_characterState.CurrentShield - (int)shieldDmg);
                    _characterState.UpdateCurrentHealthShield(newHp, newSd);
                }
                else
                {
                    // ewentualna animacja hit dla NPC/Potwora... (bez zmian)
                    MuGame.ScheduleOnMainThread(() =>
                    {
                        if (MuGame.Instance.ActiveScene?.World is WorldControl world
                            && world.TryGetWalkerById(maskedId, out var walker))
                        {
                            // tu można zmienić walker.CurrentAction na animację otrzymania ciosu
                            _logger.LogDebug("Triggering hit animation for {Type} {Id:X4}", walker.GetType().Name, maskedId);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing ObjectHit (11).");
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
                if (MuGame.Instance.ActiveScene?.World is not WalkableWorldControl world)
                    return;

                for (int i = 0; i < count; i++)
                {
                    ushort id = outPkt[i].Id;                 // <--- rawId
                    _scopeManager.RemoveObjectFromScope(id);

                    var obj = world.Objects
                                   .OfType<WalkerObject>()
                                   .FirstOrDefault(w => w.NetworkId == id);
                    if (obj != null)
                    {
                        world.Objects.Remove(obj);
                        obj.Dispose();
                        _logger.LogDebug("Removed {Type} {Id:X4} from world.", obj.GetType().Name, id);
                    }
                    else
                    {
                        _logger.LogTrace("Object {Id:X4} not found in world for removal.", id);
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

                // **** ADDED: Update visual position on main thread ****
                MuGame.ScheduleOnMainThread(() =>
                {
                    if (MuGame.Instance.ActiveScene?.World is WalkableWorldControl world)
                    {
                        var objToMove = world.Objects.OfType<WalkerObject>().FirstOrDefault(w => w.NetworkId == objectIdMasked);
                        if (objToMove != null)
                        {
                            objToMove.Location = new Vector2(x, y); // Directly set location for instant move
                            _logger.LogDebug("Updated visual position for {ObjectType} {Id:X4} to ({X},{Y})", objToMove.GetType().Name, objectIdMasked, x, y);
                        }
                    }
                });
                // **** END ADDED ****

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


        // ScopeHandler.cs
        // ---------------  zastąp cały dotychczasowy handler 0xD4 tym kodem ---------------

        [PacketHandler(0xD4, PacketRouter.NoSubCode)]     // ObjectWalked (step-by-step)
        public Task HandleObjectWalkedAsync(Memory<byte> packet)
        {
            const int MinSize = 7;
            if (packet.Length < MinSize)
                return Task.CompletedTask;

            var walk = new ObjectWalked(packet);
            ushort rawId = walk.ObjectId;
            ushort maskedId = (ushort)(rawId & 0x7FFF);  // <-- masked
            byte x = walk.TargetX;
            byte y = walk.TargetY;

            _scopeManager.TryUpdateScopeObjectPosition(maskedId, x, y);

            MuGame.ScheduleOnMainThread(() =>
            {

                if (MuGame.Instance.ActiveScene?.World is not WalkableWorldControl world)
                return;

                /* ---------- GRACZ ------------------------------------------------- */
                var player = world.Objects.OfType<PlayerObject>()
                               .FirstOrDefault(p => p.NetworkId == maskedId);
                if (player != null)
                {
                    bool wasMoving = player.IsMoving;                 // <—  zapamiętaj

                    player.MoveTo(new Vector2(x, y), sendToServer: false);

                    const byte walkActionPlayer = 1;                  // indeks „walk” gracza
                    if (!wasMoving                                           // zaczynał stać
                        && player.Model?.Actions?.Length > walkActionPlayer) // ma taką akcję
                    {
                        player.CurrentAction = (PlayerAction)walkActionPlayer;
                    }
                    return;
                }

                /* ---------- NPC / POTWÓR ----------------------------------------- */
                var walker = world.Objects
                                  .OfType<WalkerObject>()
                                  .FirstOrDefault(w => w.NetworkId == maskedId);
                if (walker != null)
                {
                    bool wasMoving = walker.IsMoving;                 // <—  zapamiętaj

                    walker.MoveTo(new Vector2(x, y), sendToServer: false);

                    const byte walkActionNpc = 2;                     // indeks „walk” monstra
                    if (!wasMoving                                           // dopiero ruszył
                        && walker.Model?.Actions?.Length > walkActionNpc)    // ma taką akcję
                    {
                        walker.CurrentAction = walkActionNpc;
                    }
                }
            });

            return Task.CompletedTask;
        }

        [PacketHandler(0x17, PacketRouter.NoSubCode)]
        public Task HandleObjectGotKilledAsync(Memory<byte> packet)
        {
            try
            {
                if (packet.Length < ObjectGotKilled.Length)
                {
                    _logger.LogWarning("ObjectGotKilled too short: {Length}", packet.Length);
                    return Task.CompletedTask;
                }

                var death = new ObjectGotKilled(packet);
                ushort killedId = death.KilledId;           // <--- rawId
                ushort killerId = death.KillerId;           // <--- rawId

                string killerName = _scopeManager.TryGetScopeObjectName(killerId, out var kn) ? (kn ?? "Unknown") : "Unknown";
                string killedName = _scopeManager.TryGetScopeObjectName(killedId, out var kd) ? (kd ?? "Unknown") : "Unknown";

                if (killedId == _characterState.Id)
                {
                    _logger.LogWarning("💀 YOU DIED! Killed by {Killer}", killerName);
                    _characterState.UpdateCurrentHealthShield(0, 0);
                }
                else
                {
                    _logger.LogInformation("💀 {Killed} died. Killed by {Killer}", killedName, killerName);
                }

                _scopeManager.RemoveObjectFromScope(killedId);  // <--- rawId

                MuGame.ScheduleOnMainThread(() =>
                {
                    if (MuGame.Instance.ActiveScene?.World is WalkableWorldControl world)
                    {
                        var obj = world.Objects
                                       .OfType<WalkerObject>()
                                       .FirstOrDefault(w => w.NetworkId == killedId);
                        if (obj != null)
                        {
                            world.Objects.Remove(obj);
                            obj.Dispose();
                            _logger.LogDebug("Removed killed {Type} {Id:X4}", obj.GetType().Name, killedId);
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in HandleObjectGotKilledAsync");
            }
            return Task.CompletedTask;
        }


        internal static List<NpcScopeObject> TakePendingNpcsMonsters()
        {
            lock (_pendingNpcsMonsters)
            {
                var copy = _pendingNpcsMonsters.ToList();
                _pendingNpcsMonsters.Clear();
                return copy;
            }
        }

        /* ScopeHandler.cs  ─────────────────────────────────────────── */
        [PacketHandler(0x18, PacketRouter.NoSubCode)]
        public Task HandleObjectAnimationAsync(Memory<byte> packet)
        {
            var anim = new ObjectAnimation(packet);
            ushort id = (ushort)(anim.ObjectId & 0x7FFF);

            MuGame.ScheduleOnMainThread(() =>
            {
                if (MuGame.Instance.ActiveScene?.World is not WorldControl world) return;
                if (!world.TryGetWalkerById(id, out var walker)) return;

                byte raw = anim.Animation;           // pełny bajt z serwera
                byte dir = anim.Direction;

                // ── WYDZIEL numer akcji ────────────────────────────────
                byte actionIdx = walker is MonsterObject
                                 ? (byte)((raw & 0xE0) >> 5)   // bity 7-5
                                 : (byte)((raw & 0xF8) >> 3);  // bity 7-3

                // ── ustaw animację zawsze od KLATKI 0 ──────────────────
                walker.PlayAction(actionIdx);   // patrz § 2

                walker.Direction = (Models.Direction)dir;
            });

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