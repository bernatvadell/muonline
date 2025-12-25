using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using Client.Main.Core.Utilities;
using Client.Main.Core.Models;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using MUnique.OpenMU.Network.Packets;
using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Client.Main.Models;
using Client.Main.Objects;
using Client.Main.Objects.Player;
using Client.Main.Objects.Effects;
using Client.Main.Core.Client;
using Client.Main.Scenes;
using Client.Main.Controllers;
using System.Threading;
using Client.Data.ATT;

namespace Client.Main.Networking.PacketHandling.Handlers
{
    /// <summary>
    /// Handles packets related to objects entering or leaving scope, moving, and dying.
    /// </summary>
    public class ScopeHandler : IGamePacketHandler
    {
        private static readonly string[] _recentHitPackets = new string[12];
        private static int _recentHitPacketIndex = -1;

        internal static string[] RecentHitPackets => _recentHitPackets;
        internal static int RecentHitPacketIndex => System.Threading.Volatile.Read(ref _recentHitPacketIndex);

        // ─────────────────────────── Fields ───────────────────────────
        private readonly ILogger<ScopeHandler> _logger;
        private readonly ScopeManager _scopeManager;
        private readonly CharacterState _characterState;
        private readonly NetworkManager _networkManager;
        private readonly PartyManager _partyManager;
        private readonly TargetProtocolVersion _targetVersion;
        private readonly ILoggerFactory _loggerFactory;

        private static readonly List<NpcScopeObject> _pendingNpcsMonsters = new List<NpcScopeObject>();
        private static readonly List<PlayerScopeObject> _pendingPlayers = new List<PlayerScopeObject>();
        private static readonly ConcurrentQueue<NpcSpawnRequest> _npcSpawnQueue = new();
        private static int _npcSpawnsInFlight;
        private const int MaxNpcSpawnsPerFrame = 8;
        private const int MaxConcurrentNpcSpawns = 8;
        private static ScopeHandler _activeInstance;

        // ─────────────────────── Constructors ────────────────────────
        public ScopeHandler(
            ILoggerFactory loggerFactory,
            ScopeManager scopeManager,
            CharacterState characterState,
            NetworkManager networkManager,
            PartyManager partyManager,
            TargetProtocolVersion targetVersion)
        {
            _logger = loggerFactory.CreateLogger<ScopeHandler>();
            _scopeManager = scopeManager;
            _characterState = characterState;
            _networkManager = networkManager;
            _partyManager = partyManager;
            _targetVersion = targetVersion;
            _loggerFactory = loggerFactory;
            _activeInstance = this;
        }

        private static void RecordHitPacket(ReadOnlySpan<byte> packetSpan)
        {
            try
            {
                var hex = BitConverter.ToString(packetSpan.ToArray()).Replace("-", " ");
                var entry = $"Len={packetSpan.Length} Data={hex}";
                var index = Interlocked.Increment(ref _recentHitPacketIndex);
                _recentHitPackets[index % _recentHitPackets.Length] = entry;
            }
            catch
            {
                // Diagnostic helper should never throw into caller.
            }
        }

        // ───────────────────── Internal API ────────────────────────
        /// <summary>
        /// Retrieves and clears pending player spawns.
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

        /// <summary>
        /// Retrieves and clears pending NPC and monster spawns.
        /// </summary>
        internal static List<NpcScopeObject> TakePendingNpcsMonsters()
        {
            lock (_pendingNpcsMonsters)
            {
                var copy = _pendingNpcsMonsters.ToList();
                _pendingNpcsMonsters.Clear();
                return copy;
            }
        }

        // ───────────────────── Packet Handlers ──────────────────────

        [PacketHandler(0x12, PacketRouter.NoSubCode)] // AddCharacterToScope
        public Task HandleAddCharacterToScopeAsync(Memory<byte> packet)
        {
            try
            {
                ParseAndAddCharactersToScope(packet);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing AddCharactersToScope (0x12).");
            }
            return Task.CompletedTask;
        }

        private void ParseAndAddCharactersToScope(Memory<byte> packet)
        {
            var scope = new AddCharactersToScopeRef(packet.Span);

            for (int i = 0; i < scope.CharacterCount; i++)
            {
                var c = scope[i];
                ushort raw = c.Id;
                ushort masked = (ushort)(raw & 0x7FFF);
                var cls = ClassFromAppearance(c.Appearance);

                // Capture any active effects from the packet
                if (c.EffectCount > 0)
                {
                    for (int e = 0; e < c.EffectCount; e++)
                    {
                        byte effectId = c[e].Id;
                        _characterState.ActivateBuff(effectId, raw);
                        ElfBuffEffectManager.Instance?.HandleBuff(effectId, raw, true);
                    }
                }

                // Always update the manager, even for the local player
                _scopeManager.AddOrUpdatePlayerInScope(masked, raw, c.CurrentPositionX, c.CurrentPositionY, c.Name);

                // Spawn remote players immediately if the world is ready,
                // otherwise buffer for later
                if (MuGame.Instance.ActiveScene?.World is WalkableWorldControl w
                    && w.Status == GameControlStatus.Ready)
                {
                    if (masked != _characterState.Id) // Don't spawn self as a remote player
                    {
                        SpawnRemotePlayerIntoWorld(w, masked, raw, c.CurrentPositionX, c.CurrentPositionY, c.Name, cls, c.Appearance.ToArray());
                    }
                }
                else if (masked != _characterState.Id)
                {
                    lock (_pendingPlayers)
                    {
                        if (!_pendingPlayers.Any(p => p.Id == masked))
                        {
                            _pendingPlayers.Add(new PlayerScopeObject(masked, raw, c.CurrentPositionX, c.CurrentPositionY, c.Name, cls, c.Appearance.ToArray()));
                        }
                    }
                }
            }
        }

        private static CharacterClassNumber ClassFromAppearance(ReadOnlySpan<byte> app)
        {
            if (app.Length == 0) return CharacterClassNumber.DarkWizard;
            int raw = (app[0] >> 3) & 0b1_1111;
            return raw switch
            {
                0 or 2 or 3 or 4 or 6 or 7 or 8 or 10 or 11 or 12 or 13 or
                16 or 17 or 20 or 22 or 23 or 24 or 25 => (CharacterClassNumber)raw,
                _ => CharacterClassNumber.DarkWizard
            };
        }

        private void SpawnRemotePlayerIntoWorld(
                WalkableWorldControl world,
                ushort maskedId,
                ushort rawId,
                byte x,
                byte y,
                string name,
                CharacterClassNumber cls,
                ReadOnlyMemory<byte> appearanceData)
        {
            _logger.LogDebug($"[Spawn] Received request for {name} ({maskedId:X4}).");

            // Process player spawning asynchronously without blocking
            _ = Task.Run(async () =>
            {
                try
                {
                    await ProcessPlayerSpawnAsync(world, maskedId, rawId, x, y, name, cls, appearanceData);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"[Spawn] Error processing player spawn for {name} ({maskedId:X4}).");
                }
            });
        }

        private async Task ProcessPlayerSpawnAsync(
                WalkableWorldControl world,
                ushort maskedId,
                ushort rawId,
                byte x,
                byte y,
                string name,
                CharacterClassNumber cls,
                ReadOnlyMemory<byte> appearanceData)
        {
            _logger.LogDebug($"[Spawn] Starting creation for {name} ({maskedId:X4}).");

            if (MuGame.Instance.ActiveScene?.World != world || world.Status != GameControlStatus.Ready)
            {
                _logger.LogWarning($"[Spawn] World changed or not ready. Aborting spawn for {name}.");
                return;
            }

            var p = new PlayerObject(new AppearanceData(appearanceData))
            {
                NetworkId = maskedId,
                CharacterClass = cls,
                Name = name,
                Location = new Vector2(x, y),
                World = world
            };
            _logger.LogDebug($"[Spawn] PlayerObject created for {name}.");

            var preloadTask = p.PreloadAppearanceModelsAsync();

            // Load assets in background
            try
            {
                var loadTask = p.Load();
                await Task.WhenAll(preloadTask, loadTask);
                _logger.LogDebug($"[Spawn] Assets preloaded and Load() completed for {name}.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[Spawn] Error loading assets for {name} ({maskedId:X4}).");
                MuGame.ScheduleOnMainThread(() => p.Dispose());
                return;
            }

            // Add to world on main thread
            MuGame.ScheduleOnMainThread(() =>
            {
                // Double-check world is still valid
                if (MuGame.Instance.ActiveScene?.World != world || world.Status != GameControlStatus.Ready)
                {
                    _logger.LogWarning($"[Spawn] World changed or not ready during spawn. Aborting spawn for {name}.");
                    p.Dispose();
                    return;
                }

                if (world.WalkerObjectsById.TryGetValue(maskedId, out WalkerObject existingWalker))
                {
                    _logger.LogWarning($"[Spawn] Stale object for {name} found. Removing before adding new.");
                    world.Objects.Remove(existingWalker);
                    existingWalker.Dispose();
                }

                if (world.FindPlayerById(maskedId) != null)
                {
                    _logger.LogWarning($"[Spawn] PlayerObject for {name} already exists. Aborting.");
                    p.Dispose();
                    return;
                }

                world.Objects.Add(p);
                _logger.LogDebug($"[Spawn] Added {name} to world.Objects.");

                ElfBuffEffectManager.Instance?.EnsureBuffsForPlayer(maskedId);

                // Set final position
                if (p.World != null && p.World.Terrain != null)
                {
                    p.MoveTargetPosition = p.TargetPosition;
                    p.Position = p.TargetPosition;
                }
                else
                {
                    float worldX = p.Location.X * Constants.TERRAIN_SCALE + 0.5f * Constants.TERRAIN_SCALE;
                    float worldY = p.Location.Y * Constants.TERRAIN_SCALE + 0.5f * Constants.TERRAIN_SCALE;
                    p.MoveTargetPosition = new Vector3(worldX, worldY, 0);
                    p.Position = p.MoveTargetPosition;
                }
                _logger.LogInformation($"[Spawn] Successfully spawned {name} ({maskedId:X4}) into world.");
            });
        }

        [PacketHandler(0x13, PacketRouter.NoSubCode)] // AddNpcToScope
        public Task HandleAddNpcToScopeAsync(Memory<byte> packet)
        {
            ParseAndQueueNpcSpawns(packet.ToArray());
            return Task.CompletedTask;
        }

        [PacketHandler(0x16, PacketRouter.NoSubCode)] // AddMonstersToScope
        public Task HandleAddMonstersToScopeAsync(Memory<byte> packet)
        {
            ParseAndQueueNpcSpawns(packet.ToArray());
            return Task.CompletedTask;
        }

        private void ParseAndQueueNpcSpawns(byte[] packetData)
        {
            Memory<byte> packet = packetData;
            int npcCount = 0, firstOffset = 0, dataSize = 0;
            Func<Memory<byte>, (ushort id, ushort type, byte x, byte y, byte direction)> readNpc = null!;

            switch (_targetVersion)
            {
                case TargetProtocolVersion.Season6:
                    var s6 = new AddNpcsToScope(packet);
                    npcCount = s6.NpcCount;
                    firstOffset = 5;
                    dataSize = AddNpcsToScope.NpcData.Length;
                    readNpc = m => { var d = new AddNpcsToScope.NpcData(m); return (d.Id, d.TypeNumber, d.CurrentPositionX, d.CurrentPositionY, d.Rotation); };
                    break;
                case TargetProtocolVersion.Version097:
                    var v97 = new AddNpcsToScope095(packet);
                    npcCount = v97.NpcCount;
                    firstOffset = 5;
                    dataSize = AddNpcsToScope095.NpcData.Length;
                    readNpc = m => { var d = new AddNpcsToScope095.NpcData(m); return (d.Id, d.TypeNumber, d.CurrentPositionX, d.CurrentPositionY, d.Rotation); };
                    break;
                case TargetProtocolVersion.Version075:
                    var v75 = new AddNpcsToScope075(packet);
                    npcCount = v75.NpcCount;
                    firstOffset = 5;
                    dataSize = AddNpcsToScope075.NpcData.Length;
                    readNpc = m => { var d = new AddNpcsToScope075.NpcData(m); return (d.Id, d.TypeNumber, d.CurrentPositionX, d.CurrentPositionY, d.Rotation); };
                    break;
                default:
                    _logger.LogWarning("Unsupported protocol version {Version} for AddNpcToScope.", _targetVersion);
                    return;
            }

            _logger.LogInformation("ScopeHandler: AddNpcToScope received {Count} objects.", npcCount);

            int currentPacketOffset = firstOffset;
            ushort currentMapId = _characterState.MapId;

            for (int i = 0; i < npcCount; i++)
            {
                if (currentPacketOffset + dataSize > packet.Length)
                {
                    _logger.LogWarning("ScopeHandler: Packet too short for NPC data at index {Index}.", i);
                    break;
                }

                var (rawId, type, x, y, direction) = readNpc(packet.Slice(currentPacketOffset));
                currentPacketOffset += dataSize;

                ushort maskedId = (ushort)(rawId & 0x7FFF);
                string name = NpcDatabase.GetNpcName(type);

                _scopeManager.AddOrUpdateNpcInScope(maskedId, rawId, x, y, type, name);

                _npcSpawnQueue.Enqueue(new NpcSpawnRequest(maskedId, rawId, x, y, direction, type, name, currentMapId));
            }
        }

        internal static void PumpNpcSpawnQueue(WalkableWorldControl world, int maxPerFrame = MaxNpcSpawnsPerFrame)
        {
            if (world == null || world.Status != GameControlStatus.Ready)
            {
                return;
            }

            var handler = _activeInstance;
            if (handler == null || _npcSpawnQueue.IsEmpty)
            {
                return;
            }

            int startedThisFrame = 0;
            while (startedThisFrame < maxPerFrame
                && Volatile.Read(ref _npcSpawnsInFlight) < MaxConcurrentNpcSpawns
                && _npcSpawnQueue.TryDequeue(out var request))
            {
                if (request.MapId != handler._characterState.MapId)
                {
                    handler._logger.LogDebug("Discarding queued NPC/Monster spawn {SpawnId:X4} for map {RequestMap} after map changed to {CurrentMap}.", request.MaskedId, request.MapId, handler._characterState.MapId);
                    continue;
                }

                startedThisFrame++;
                handler.StartNpcSpawn(request);
            }
        }

        private void StartNpcSpawn(NpcSpawnRequest request)
        {
            if (request.MapId != _characterState.MapId)
            {
                _logger.LogDebug("Skipping queued NPC/Monster spawn {SpawnId:X4} for stale map {RequestMap} (current: {CurrentMap}).", request.MaskedId, request.MapId, _characterState.MapId);
                return;
            }

            Interlocked.Increment(ref _npcSpawnsInFlight);

            _ = ProcessNpcSpawnAsync(request).ContinueWith(t =>
            {
                if (t.IsFaulted && t.Exception != null)
                {
                    _logger.LogError(t.Exception, "ScopeHandler: Error processing NPC/Monster spawn {SpawnId:X4}.", request.MaskedId);
                }

                Interlocked.Decrement(ref _npcSpawnsInFlight);
            }, global::System.Threading.Tasks.TaskScheduler.Default);
        }

        private Task ProcessNpcSpawnAsync(NpcSpawnRequest request)
        {
            if (request.MapId != _characterState.MapId)
            {
                _logger.LogDebug("Dropping NPC/Monster spawn {SpawnId:X4} queued for map {RequestMap} after map changed to {CurrentMap}.", request.MaskedId, request.MapId, _characterState.MapId);
                return Task.CompletedTask;
            }

            return ProcessNpcSpawnAsync(request.MaskedId, request.RawId, request.X, request.Y, request.Direction, request.Type, request.Name);
        }

        private async Task ProcessNpcSpawnAsync(ushort maskedId, ushort rawId, byte x, byte y, byte direction, ushort type, string name)
        {
            if (MuGame.Instance.ActiveScene?.World is not WalkableWorldControl worldRef || worldRef.Status != GameControlStatus.Ready)
            {
                lock (_pendingNpcsMonsters)
                {
                    if (!_pendingNpcsMonsters.Any(p => p.Id == maskedId))
                    {
                        _pendingNpcsMonsters.Add(new NpcScopeObject(maskedId, rawId, x, y, type, name) { Direction = direction });
                    }
                }
                return;
            }

            if (!NpcDatabase.TryGetNpcType(type, out var npcClassType))
            {
                _logger.LogWarning($"ScopeHandler: NPC type not found in NpcDatabase for TypeID {type}.");
                return;
            }

            if (!(Activator.CreateInstance(npcClassType) is WalkerObject obj))
            {
                _logger.LogWarning($"ScopeHandler: Could not create instance of NPC type {npcClassType} for TypeID {type}.");
                return;
            }

            // Configure the object's properties
            obj.NetworkId = maskedId;
            obj.Location = new Vector2(x, y);
            obj.Direction = (Client.Main.Models.Direction)direction;
            obj.World = worldRef;

            // Load assets in background
            try
            {
                await obj.Load();
                if (obj is ModelObject modelObj)
                {
                    // Skip preloading to avoid blocking
                }

                if (obj.Status != GameControlStatus.Ready)
                {
                    _logger.LogWarning($"ScopeHandler: NPC/Monster {maskedId} ({obj.GetType().Name}) loaded but status is {obj.Status}.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"ScopeHandler: Error loading NPC/Monster {maskedId} ({obj.GetType().Name}).");
                MuGame.ScheduleOnMainThread(() => obj.Dispose());
                return;
            }

            // Add to world on main thread
            MuGame.ScheduleOnMainThread(() =>
            {
                // Double-check world is still valid and object doesn't already exist
                if (MuGame.Instance.ActiveScene?.World != worldRef || worldRef.Status != GameControlStatus.Ready)
                {
                    obj.Dispose();
                    return;
                }

                // Check and remove stale objects quickly
                if (worldRef.WalkerObjectsById.TryGetValue(maskedId, out WalkerObject existingWalker))
                {
                    _logger.LogWarning($"ScopeHandler: Stale/Duplicate NPC/Monster ID {maskedId:X4} ({existingWalker.GetType().Name}) found in WalkerObjectsById. Removing it before adding new {name} (Type: {type}).");

                    existingWalker.Dispose();
                    worldRef.Objects.Remove(existingWalker);
                }

                // Quick check for duplicates using cached walkers
                if (worldRef.FindWalkerById(maskedId) != null)
                {
                    obj.Dispose();
                    return;
                }

                worldRef.Objects.Add(obj);

                // Set final position
                if (obj.World?.Terrain != null)
                {
                    obj.MoveTargetPosition = obj.TargetPosition;
                    obj.Position = obj.TargetPosition;
                }
                else
                {
                    _logger.LogError($"ScopeHandler: obj.World or obj.World.Terrain is null for NPC/Monster {maskedId} ({obj.GetType().Name}) AFTER loading and adding. This indicates a problem.");
                    float worldX = obj.Location.X * Constants.TERRAIN_SCALE + 0.5f * Constants.TERRAIN_SCALE;
                    float worldY = obj.Location.Y * Constants.TERRAIN_SCALE + 0.5f * Constants.TERRAIN_SCALE;
                    obj.MoveTargetPosition = new Vector3(worldX, worldY, 0);
                    obj.Position = obj.MoveTargetPosition;
                }

                // Play Appear animation for monsters that have MonsterActionType.Appear mapped
                if (obj is MonsterObject monster && obj is ModelObject modelObj)
                {
                    // Check if monster has Appear animation available
                    if (modelObj.Model?.Actions != null &&
                        (int)MonsterActionType.Appear < modelObj.Model.Actions.Length &&
                        modelObj.Model.Actions[(int)MonsterActionType.Appear] != null)
                    {
                        // Play MonsterActionType.Appear animation for dramatic spawn effect
                        monster.PlayAction((ushort)MonsterActionType.Appear);
                    }
                }
            });
        }

        private readonly struct NpcSpawnRequest
        {
            public NpcSpawnRequest(ushort maskedId, ushort rawId, byte x, byte y, byte direction, ushort type, string name, ushort mapId)
            {
                MaskedId = maskedId;
                RawId = rawId;
                X = x;
                Y = y;
                Direction = direction;
                Type = type;
                Name = name;
                MapId = mapId;
            }

            public ushort MaskedId { get; }
            public ushort RawId { get; }
            public byte X { get; }
            public byte Y { get; }
            public byte Direction { get; }
            public ushort Type { get; }
            public string Name { get; }
            public ushort MapId { get; }
        }

        [PacketHandler(0x25, PacketRouter.NoSubCode)]
        public async Task HandleAppearanceChangedAsync(Memory<byte> packet)
        {
            try
            {
                const byte UNEQUIP_MARKER = 0xFF;
                const ushort ID_MASK = 0x7FFF;

                var span = packet.Span;

                // Season 6 servers can send two variants:
                // - Standard AppearanceChanged (length 13): player id + 8 bytes packed item appearance.
                // - AppearanceChangedExtended (length 14): explicit slot/group/number/level fields.
                if (span.Length == 14)
                {
                    const int EXT_PLAYER_ID_OFFSET = 4;
                    const int EXT_ITEM_SLOT_OFFSET = 6;
                    const int EXT_ITEM_GROUP_OFFSET = 7;
                    const int EXT_ITEM_NUMBER_OFFSET = 8;
                    const int EXT_ITEM_LEVEL_OFFSET = 10;
                    const int EXT_EXCELLENT_FLAGS_OFFSET = 11;
                    const int EXT_ANCIENT_DISCRIMINATOR_OFFSET = 12;
                    const int EXT_SET_COMPLETE_OFFSET = 13;

                    ushort extRawKey = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(EXT_PLAYER_ID_OFFSET, 2));
                    ushort extMaskedId = (ushort)(extRawKey & ID_MASK);

                    byte extItemSlot = span[EXT_ITEM_SLOT_OFFSET];
                    byte extItemGroup = span[EXT_ITEM_GROUP_OFFSET];

                    if (extItemGroup == UNEQUIP_MARKER)
                    {
                        await HandleUnequipAsync(extMaskedId, extItemSlot);
                        return;
                    }

                    ushort extItemNumber = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(EXT_ITEM_NUMBER_OFFSET, 2));
                    byte extItemLevel = span[EXT_ITEM_LEVEL_OFFSET];
                    byte extExcellentFlags = span[EXT_EXCELLENT_FLAGS_OFFSET];
                    byte extAncientDiscriminator = span[EXT_ANCIENT_DISCRIMINATOR_OFFSET];
                    bool extIsAncientSetComplete = span[EXT_SET_COMPLETE_OFFSET] != 0;

                    const int EXT_MAX_ITEM_INDEX = 512;
                    int extFinalItemType = (extItemGroup * EXT_MAX_ITEM_INDEX) + extItemNumber;

                    _logger.LogDebug("Parsed AppearanceChangedExtended for ID {Id:X4}: Slot={Slot}, Group={Group}, Number={Number}, Type={Type}, Level={Level}",
                        extMaskedId, extItemSlot, extItemGroup, extItemNumber, extFinalItemType, extItemLevel);

                    _logger.LogInformation("[ScopeHandler] AppearanceChangedExtended ID {Id:X4}: ExcFlags=0x{ExcFlags:X2}, AncDisc=0x{AncDisc:X2}, SetComplete={SetComplete}",
                        extMaskedId, extExcellentFlags, extAncientDiscriminator, extIsAncientSetComplete);

                    await HandleEquipAsync(extMaskedId, extItemSlot, extItemGroup, extItemNumber, extFinalItemType, extItemLevel,
                        itemOptions: 0, extExcellentFlags, extAncientDiscriminator, extIsAncientSetComplete);
                    return;
                }

                // Standard packed variant.
                const int STD_MIN_LENGTH = 7; // header(3) + id(2) + at least 2 bytes of item data
                const int STD_PLAYER_ID_OFFSET = 3;
                const int STD_ITEM_DATA_OFFSET = 5;
                const int WEAPON_SLOT_THRESHOLD = 2;
                const int WEAPON_GROUP = 0;
                const int ARMOR_GROUP_OFFSET = 5;

                if (span.Length < STD_MIN_LENGTH)
                {
                    _logger.LogWarning("AppearanceChanged packet (0x25) too short: {Length}.", span.Length);
                    return;
                }

                ushort stdRawKey = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(span.Slice(STD_PLAYER_ID_OFFSET, 2));
                ushort stdMaskedId = (ushort)(stdRawKey & ID_MASK);

                var itemData = span.Slice(STD_ITEM_DATA_OFFSET);
                if (itemData.Length < 2)
                {
                    _logger.LogWarning("AppearanceChanged packet (0x25) item data too short: {Length}.", itemData.Length);
                    return;
                }

                byte itemSlot = (byte)((itemData[1] >> 4) & 0x0F);

                if (itemData[0] == UNEQUIP_MARKER)
                {
                    await HandleUnequipAsync(stdMaskedId, itemSlot);
                    return;
                }

                byte glowLevel = (byte)(itemData[1] & 0x0F);

                ushort itemNumber = itemData[0];
                byte itemGroup;

                if (itemSlot < WEAPON_SLOT_THRESHOLD)
                {
                    // For weapon slots the group is encoded in itemData[2] high nibble,
                    // and the item number high bits in its low nibble (same layout as viewport equipment).
                    if (itemData.Length > 2)
                    {
                        itemGroup = (byte)((itemData[2] >> 4) & 0x0F);
                        itemNumber = (ushort)(itemNumber | ((itemData[2] & 0x0F) << 8));
                    }
                    else
                    {
                        itemGroup = (byte)WEAPON_GROUP;
                    }
                }
                else
                {
                    itemGroup = (byte)(itemSlot + ARMOR_GROUP_OFFSET);
                }

                byte itemLevel = ConvertGlowToItemLevel(glowLevel);

                byte itemOptions = itemData.Length > 3 ? itemData[3] : (byte)0;
                byte excellentFlags = itemData.Length > 4 ? itemData[4] : (byte)0;
                byte ancientDiscriminator = itemData.Length > 5 ? itemData[5] : (byte)0;
                bool isAncientSetComplete = itemData.Length > 6 && itemData[6] != 0;

                const int MAX_ITEM_INDEX = 512;
                int finalItemType = (itemGroup * MAX_ITEM_INDEX) + itemNumber;

                _logger.LogDebug("Parsed AppearanceChanged for ID {Id:X4}: Slot={Slot}, Group={Group}, Number={Number}, Type={Type}, Level={Level}",
                    stdMaskedId, itemSlot, itemGroup, itemNumber, finalItemType, itemLevel);

                _logger.LogInformation("[ScopeHandler] AppearanceChanged ID {Id:X4}: ExcFlags=0x{ExcFlags:X2}, AncDisc=0x{AncDisc:X2}, SetComplete={SetComplete}",
                    stdMaskedId, excellentFlags, ancientDiscriminator, isAncientSetComplete);

                await HandleEquipAsync(stdMaskedId, itemSlot, itemGroup, itemNumber, finalItemType, itemLevel,
                    itemOptions, excellentFlags, ancientDiscriminator, isAncientSetComplete);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing AppearanceChanged (0x25).");
            }
        }

        private Task HandleUnequipAsync(ushort maskedId, byte itemSlot)
        {
            MuGame.ScheduleOnMainThread(async () =>
            {
                if (MuGame.Instance.ActiveScene?.World is not WorldControl world)
                {
                    _logger.LogWarning("No world available for unequip operation");
                    return;
                }

                if (world.TryGetWalkerById(maskedId, out var walker) && walker is PlayerObject player)
                {
                    try
                    {
                        await player.UpdateEquipmentSlotAsync(itemSlot, null);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error unequipping item from slot {Slot}", itemSlot);
                    }
                }
                else
                {
                    _logger.LogWarning("Player with ID {Id:X4} not found for unequip operation", maskedId);
                }
            });
            return Task.CompletedTask;
        }

        private Task HandleEquipAsync(ushort maskedId, byte itemSlot, byte itemGroup, ushort itemNumber,
            int finalItemType, byte itemLevel, byte itemOptions, byte excellentFlags,
            byte ancientDiscriminator, bool isAncientSetComplete)
        {
            MuGame.ScheduleOnMainThread(async () =>
            {
                if (MuGame.Instance.ActiveScene?.World is not WorldControl world) return;

                if (world.TryGetWalkerById(maskedId, out var walker) && walker is PlayerObject player)
                {
                    var equipmentData = new EquipmentSlotData
                    {
                        ItemGroup = itemGroup,
                        ItemNumber = itemNumber,
                        ItemType = finalItemType,
                        ItemLevel = itemLevel,
                        ItemOptions = itemOptions,
                        ExcellentFlags = excellentFlags,
                        AncientDiscriminator = ancientDiscriminator,
                        IsAncientSetComplete = isAncientSetComplete
                    };

                    await player.UpdateEquipmentSlotAsync(itemSlot, equipmentData);
                }
                else
                {
                    _logger.LogWarning("Player with ID {Id:X4} not found in scope.", maskedId);
                }
            });
            return Task.CompletedTask;
        }

        [PacketHandler(0x11, PacketRouter.NoSubCode)] // ObjectHit / ObjectGotHit
        public Task HandleObjectHitAsync(Memory<byte> packet)
        {
            try
            {
                RecordHitPacket(packet.Span);

                bool isExtendedPacket = packet.Length >= ObjectHitExtended.Length;
                ushort rawId;
                uint healthDmg;
                uint shieldDmg;
                DamageKind damageKind;
                bool isDoubleDamage;
                bool isTripleDamage;
                byte? healthStatus = null;
                byte? shieldStatus = null;

                if (isExtendedPacket)
                {
                    var extended = new ObjectHitExtended(packet);
                    rawId = extended.ObjectId;
                    healthDmg = extended.HealthDamage;
                    shieldDmg = extended.ShieldDamage;
                    damageKind = extended.Kind;
                    isDoubleDamage = extended.IsDoubleDamage;
                    isTripleDamage = extended.IsTripleDamage;
                    healthStatus = extended.HealthStatus;
                    shieldStatus = extended.ShieldStatus;
                }
                else
                {
                    if (packet.Length < ObjectHit.Length)
                    {
                        _logger.LogWarning("ObjectHit packet (0x11) too short: {Length}", packet.Length);
                        return Task.CompletedTask;
                    }

                    var hitInfo = new ObjectHit(packet);
                    rawId = hitInfo.ObjectId;
                    healthDmg = hitInfo.HealthDamage;
                    shieldDmg = hitInfo.ShieldDamage;
                    damageKind = hitInfo.Kind;
                    isDoubleDamage = hitInfo.IsDoubleDamage;
                    isTripleDamage = hitInfo.IsTripleDamage;
                }

                ushort maskedId = (ushort)(rawId & 0x7FFF);
                uint totalDmg = healthDmg + shieldDmg;

                float? healthFraction = null;
                float? shieldFraction = null;
                const float statusScale = 1f / 250f;

                if (healthStatus is { } hs && hs != byte.MaxValue)
                {
                    healthFraction = Math.Clamp(hs * statusScale, 0f, 1f);
                }

                if (shieldStatus is { } ss && ss != byte.MaxValue)
                {
                    shieldFraction = Math.Clamp(ss * statusScale, 0f, 1f);
                }

                // Log damage event with type information
                string objectName = _scopeManager.TryGetScopeObjectName(maskedId, out var nm) ? (nm ?? "Object") : "Object";
                _logger.LogInformation(
                    "💥 {ObjectName} (ID: {Id:X4}) received hit: HP {HpDmg}, SD {SdDmg}, Type: {DamageKind}, 2x: {IsDouble}, 3x: {IsTriple}",
                    objectName, maskedId, healthDmg, shieldDmg, damageKind, isDoubleDamage, isTripleDamage
                );

                // Display floating damage text on the main thread
                MuGame.ScheduleOnMainThread(() =>
                {
                    if (MuGame.Instance.ActiveScene?.World is not WorldControl world)
                    {
                        _logger.LogWarning("Cannot show damage text: Active world is not ready.");
                        return;
                    }

                    WalkerObject target = null;
                    if (maskedId == _characterState.Id && world is WalkableWorldControl walkable)
                    {
                        target = walkable.Walker;
                        if (target == null)
                        {
                            _logger.LogWarning("Local player (ID {Id:X4}) hit but walker is null.", maskedId);
                            return;
                        }
                    }
                    else if (!world.TryGetWalkerById(maskedId, out target))
                    {
                        _logger.LogWarning("Cannot find walker {Id:X4} to show damage text.", maskedId);
                        return;
                    }

                    var headPos = target.WorldPosition.Translation
                                + Vector3.UnitZ * (target.BoundingBoxWorld.Max.Z - target.WorldPosition.Translation.Z + 30f);

                    if (target is MonsterObject monster)
                    {
                        monster.UpdateHealthFractions(healthFraction, shieldFraction, healthDmg, shieldDmg);
                    }

                    // Use server-provided damage type for authentic MU Online colors
                    Color dmgColor;
                    string dmgText;

                    if (totalDmg == 0)
                    {
                        dmgColor = Color.White;
                        dmgText = "Miss";
                    }
                    else
                    {
                        // Local player damage is always red, others use server-provided damage type colors
                        if (maskedId == _characterState.Id)
                        {
                            dmgColor = Color.Red;
                        }
                        else
                        {
                            // Map DamageKind to colors for other players/monsters
                            dmgColor = damageKind switch
                            {
                                DamageKind.NormalRed => Color.Orange,
                                DamageKind.IgnoreDefenseCyan => Color.Cyan,
                                DamageKind.ExcellentLightGreen => Color.LightGreen,
                                DamageKind.CriticalBlue => Color.DeepSkyBlue,
                                DamageKind.LightPink => Color.LightPink,
                                DamageKind.PoisonDarkGreen => Color.DarkGreen,
                                DamageKind.ReflectedDarkPink => Color.DeepPink,
                                DamageKind.White => Color.White,
                                _ => Color.Red // fallback to normal red
                            };
                        }

                        // Add damage multiplier indicators for double/triple damage
                        string multiplier = "";
                        if (isTripleDamage)
                            multiplier = "!!!";
                        else if (isDoubleDamage)
                            multiplier = "!!";

                        dmgText = $"{totalDmg}{multiplier}";
                    }

                    var txt = DamageTextObject.Rent(
                        dmgText,
                        maskedId,
                        dmgColor
                    );
                    world.Objects.Add(txt);
                    _logger.LogDebug("Spawned DamageTextObject '{Text}' for {Id:X4}", txt.Text, maskedId);
                });

                // Update local player's health/shield
                if (maskedId == _characterState.Id)
                {
                    uint currentHpBeforeHit = _characterState.CurrentHealth;
                    uint newHp = (uint)Math.Max(0, (int)_characterState.CurrentHealth - (int)healthDmg);
                    uint newSd = (uint)Math.Max(0, (int)_characterState.CurrentShield - (int)shieldDmg);
                    _characterState.UpdateCurrentHealthShield(newHp, newSd);

                    MuGame.ScheduleOnMainThread(() =>
                    {
                        if (MuGame.Instance.ActiveScene is GameScene gs && gs.Hero != null)
                        {
                            gs.Hero.OnPlayerTookDamage();
                        }
                    });

                    if (newHp == 0 && currentHpBeforeHit > 0)
                    {
                        _logger.LogWarning("💀 Local player (ID: {Id:X4}) died!", maskedId);
                        MuGame.ScheduleOnMainThread(() =>
                        {
                            if (MuGame.Instance.ActiveScene?.World is WalkableWorldControl walkableWorld &&
                                walkableWorld.Walker != null)
                            {
                                var localPlayer = walkableWorld.Walker;

                                if (localPlayer is PlayerObject playerObj)
                                {
                                    playerObj.IsResting = false;
                                    playerObj.IsSitting = false;
                                    playerObj.RestPlaceTarget = null;
                                    playerObj.SitPlaceTarget = null;
                                }

                                localPlayer.PlayAction((ushort)PlayerAction.PlayerDie1);
                                _logger.LogDebug("Triggered PlayerDie1 animation for local player.");
                            }
                        });
                    }
                }
                else
                {
                    // Optionally trigger hit animation for NPCs/monsters
                    MuGame.ScheduleOnMainThread(() =>
                    {
                        if (MuGame.Instance.ActiveScene?.World is WorldControl world
                          && world.TryGetWalkerById(maskedId, out var walker) && walker is MonsterObject monster)
                        {
                            monster.OnReceiveDamage();
                            monster.PlayAction((byte)MonsterActionType.Shock);
                            _logger.LogDebug("Triggering hit animation for {Type} {Id:X4}", walker.GetType().Name, maskedId);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing ObjectHit (0x11).");
            }
            return Task.CompletedTask;
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
            const int HeaderSize = 4; // size+code
            const int PrefixSize = HeaderSize + 1; // +count byte

            if (_targetVersion >= TargetProtocolVersion.Season6)
            {
                if (packet.Length < PrefixSize)
                {
                    _logger.LogWarning("ItemsDropped packet too short: {Length}", packet.Length);
                    return;
                }
                var droppedItems = new ItemsDropped(packet);
                _logger.LogInformation("Received ItemsDropped (S6+): {Count} items.", droppedItems.ItemCount);

                int dataLength = 12; // typical for S6
                int structSize = ItemsDropped.DroppedItem.GetRequiredSize(dataLength);
                int offset = PrefixSize;

                for (int i = 0; i < droppedItems.ItemCount; i++, offset += structSize)
                {
                    if (offset + structSize > packet.Length)
                    {
                        _logger.LogWarning("Packet too short for item {Index}.", i);
                        break;
                    }

                    var itemMem = packet.Slice(offset, structSize);
                    var item = new ItemsDropped.DroppedItem(itemMem);
                    ushort rawId = item.Id;
                    ushort maskedId = (ushort)(rawId & 0x7FFF);
                    byte x = item.PositionX;
                    byte y = item.PositionY;
                    var data = item.ItemData;
                    bool isMoney = data.Length >= 6 && data[0] == 15 && (data[5] >> 4) == 14; // Money is ItemGroup 14, ItemId 15
                    ScopeObject dropObj;

                    if (isMoney)
                    {
                        uint amount = (uint)(data.Length >= 5 ? data[4] : 0);
                        dropObj = new MoneyScopeObject(maskedId, rawId, x, y, amount);
                        _scopeManager.AddOrUpdateMoneyInScope(maskedId, rawId, x, y, amount);
                        _logger.LogDebug("Dropped Money: Amount={Amount}, ID={Id:X4}", amount, maskedId);

                        // Process dropped money asynchronously
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await ProcessDroppedItemAsync(dropObj, maskedId, "Sound/pDropMoney.wav");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"Error processing dropped money {maskedId:X4}");
                            }
                        });
                    }
                    else
                    {
                        dropObj = new ItemScopeObject(maskedId, rawId, x, y, data.ToArray());
                        _scopeManager.AddOrUpdateItemInScope(maskedId, rawId, x, y, data.ToArray());
                        _logger.LogDebug("Dropped Item: ID={Id:X4}, DataLen={Len}", maskedId, data.Length);

                        // Process dropped item asynchronously
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                byte[] dataCopy = item.ItemData.ToArray();
                                string itemName = ItemDatabase.GetItemName(dataCopy) ?? string.Empty;
                                string soundPath = itemName.StartsWith("Jewel", StringComparison.OrdinalIgnoreCase)
                                    ? "Sound/eGem.wav"
                                    : "Sound/pDropItem.wav";

                                await ProcessDroppedItemAsync(dropObj, maskedId, soundPath);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"Error processing dropped item {maskedId:X4}");
                            }
                        });
                    }
                }
            }
            else if (_targetVersion == TargetProtocolVersion.Version075)
            {
                // This block also needs to play sounds, similar to S6+ logic
                if (packet.Length < MoneyDropped075.Length)
                {
                    _logger.LogWarning("Dropped Object packet too short: {Length}", packet.Length);
                    return;
                }
                var legacy = new MoneyDropped075(packet);
                _logger.LogInformation("Received Dropped Object (0.75): Count={Count}.", legacy.ItemCount);

                if (legacy.ItemCount == 1)
                {
                    ushort rawId = legacy.Id;
                    ushort maskedId = (ushort)(rawId & 0x7FFF);
                    byte x = legacy.PositionX;
                    byte y = legacy.PositionY;
                    ScopeObject dropObj;

                    if (legacy.MoneyGroup == 14 && legacy.MoneyNumber == 15) // Money identification
                    {
                        uint amount = legacy.Amount;
                        dropObj = new MoneyScopeObject(maskedId, rawId, x, y, amount);
                        _scopeManager.AddOrUpdateMoneyInScope(maskedId, rawId, x, y, amount);
                        _logger.LogDebug("Dropped Money (0.75): Amount={Amount}, ID={Id:X4}", amount, maskedId);

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var newScopeObject = new MoneyScopeObject(maskedId, rawId, x, y, amount);
                                await ProcessDroppedItemAsync(newScopeObject, maskedId, "Sound/pDropMoney.wav");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"Error processing dropped money (0.75) {maskedId:X4}");
                            }
                        });
                    }
                    else // Item identification
                    {
                        const int dataOffset = 9, dataLen075 = 7;
                        if (packet.Length >= dataOffset + dataLen075)
                        {
                            var data = packet.Span.Slice(dataOffset, dataLen075).ToArray();
                            dropObj = new ItemScopeObject(maskedId, rawId, x, y, data);
                            _scopeManager.AddOrUpdateItemInScope(maskedId, rawId, x, y, data);
                            _logger.LogDebug("Dropped Item (0.75): ID={Id:X4}, DataLen={Len}", maskedId, dataLen075);

                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    var newScopeObject = new ItemScopeObject(maskedId, rawId, x, y, data);
                                    string itemName = ItemDatabase.GetItemName(data) ?? string.Empty;
                                    string soundPath = itemName.StartsWith("Jewel", StringComparison.OrdinalIgnoreCase)
                                        ? "Sound/eGem.wav"
                                        : "Sound/pDropItem.wav";

                                    await ProcessDroppedItemAsync(newScopeObject, maskedId, soundPath);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, $"Error processing dropped item (0.75) {maskedId:X4}");
                                }
                            });
                        }
                        else
                        {
                            _logger.LogWarning("Cannot extract item data from droppacket (0.75).");
                            return;
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("Multiple items in one packet not handled (Count={Count}).", legacy.ItemCount);
                }
            }
            else
            {
                _logger.LogWarning("Unsupported version for ItemsDropped (0x20): {Version}", _targetVersion);
            }
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
                _logger.LogError(ex, "Error processing ItemDropRemoved (0x21).");
            }
            return Task.CompletedTask;
        }

        private void ParseAndRemoveDroppedItemsFromScope(Memory<byte> packet)
        {
            const int headerSize = 4;
            const int prefix = headerSize + 1;   // +count

            if (packet.Length < prefix)
            {
                _logger.LogWarning("ItemDropRemoved packet too short: {Length}", packet.Length);
                return;
            }

            var removed = new ItemDropRemoved(packet);
            byte count = removed.ItemCount;
            _logger.LogInformation("Received ItemDropRemoved: {Count} objects.", count);

            const int idSize = 2;
            int expectedLen = prefix + count * idSize;
            if (packet.Length < expectedLen)
            {
                count = (byte)((packet.Length - prefix) / idSize);
                _logger.LogWarning("Packet shorter than expected – adjusted removal count to {Count}.", count);
            }

            // Process removals asynchronously
            _ = Task.Run(() =>
            {
                var objectsToRemove = new List<ushort>();

                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        var entry = removed[i];
                        ushort rawId = entry.Id;
                        ushort masked = (ushort)(rawId & 0x7FFF);

                        _scopeManager.RemoveObjectFromScope(masked);
                        objectsToRemove.Add(masked);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing dropped item removal at idx {Idx}.", i);
                    }
                }

                // Remove objects on main thread
                MuGame.ScheduleOnMainThread(() =>
                {
                    if (MuGame.Instance.ActiveScene?.World is not WalkableWorldControl world) return;

                    foreach (var masked in objectsToRemove)
                    {
                        var obj = world.FindDroppedItemById(masked);
                        if (obj != null)
                        {
                            world.Objects.Remove(obj);
                            obj.Recycle();
                            _logger.LogDebug("Removed DroppedItemObject {Id:X4} from world (scope gone).", masked);
                        }
                    }
                });
            });
        }

        [PacketHandler(0x2F, PacketRouter.NoSubCode)] // MoneyDroppedExtended
        public Task HandleMoneyDroppedExtendedAsync(Memory<byte> packet)
        {
            try
            {
                if (packet.Length < MoneyDroppedExtended.Length)
                {
                    _logger.LogWarning("MoneyDroppedExtended packet too short: {Length}", packet.Length);
                    return Task.CompletedTask;
                }
                var drop = new MoneyDroppedExtended(packet);
                ushort raw = drop.Id;
                ushort masked = (ushort)(raw & 0x7FFF);
                uint amount = drop.Amount;
                byte x = drop.PositionX;
                byte y = drop.PositionY;

                _scopeManager.AddOrUpdateMoneyInScope(masked, raw, x, y, amount);
                _logger.LogInformation("💰 MoneyDroppedExtended: ID={Id:X4}, Amount={Amount}, Pos=({X},{Y})", masked, amount, x, y);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing MoneyDroppedExtended (0x2F).");
            }
            return Task.CompletedTask;
        }

        [PacketHandler(0x14, PacketRouter.NoSubCode)] // MapObjectOutOfScope
        public Task HandleMapObjectOutOfScopeAsync(Memory<byte> packet)
        {
            var outPkt = new MapObjectOutOfScope(packet);
            int count = outPkt.ObjectCount;

            // Process removal asynchronously to avoid blocking
            _ = Task.Run(() =>
            {
                var objectsToRemove = new List<ushort>();
                for (int i = 0; i < count; i++)
                {
                    ushort raw = outPkt[i].Id;
                    ushort masked = (ushort)(raw & 0x7FFF);
                    objectsToRemove.Add(masked);
                    _scopeManager.RemoveObjectFromScope(masked);
                }

                // Remove objects on main thread in batches
                MuGame.ScheduleOnMainThread(() =>
                {
                    if (MuGame.Instance.ActiveScene?.World is not WalkableWorldControl world) return;

                    foreach (var masked in objectsToRemove)
                    {
                        // ---- 1) Player --------------------------------------------------
                        var player = world.FindPlayerById(masked);
                        if (player != null)
                        {
                            world.Objects.Remove(player);
                            player.Dispose();
                            continue;
                        }

                        // ---- 2) Walker / NPC --------------------------------------------
                        var walker = world.FindWalkerById(masked);
                        if (walker != null)
                        {
                            world.Objects.Remove(walker);
                            walker.Dispose();
                            continue;
                        }

                        // ---- 3) Dropped item --------------------------------------------
                        var drop = world.FindDroppedItemById(masked);
                        if (drop != null)
                        {
                            world.Objects.Remove(drop);
                            drop.Dispose();
                        }
                    }
                });
            });

            return Task.CompletedTask;
        }

        [PacketHandler(0x15, PacketRouter.NoSubCode)] // ObjectMoved
        public Task HandleObjectMovedAsync(Memory<byte> packet)
        {
            ushort maskedId = 0xFFFF;
            try
            {
                if (packet.Length < ObjectMoved.Length)
                {
                    _logger.LogWarning("ObjectMoved packet too short: {Length}", packet.Length);
                    return Task.CompletedTask;
                }

                var move = new ObjectMoved(packet);
                ushort raw = move.ObjectId;
                maskedId = (ushort)(raw & 0x7FFF);
                byte x = move.PositionX;
                byte y = move.PositionY;
                _logger.LogDebug("Parsed ObjectMoved: ID={Id:X4}, Pos=({X},{Y})", maskedId, x, y);

                _scopeManager.TryUpdateScopeObjectPosition(maskedId, x, y);

                // Update visual position on the main thread
                MuGame.ScheduleOnMainThread(() =>
                {
                    if (MuGame.Instance.ActiveScene?.World is WalkableWorldControl world)
                    {
                        var objToMove = world.FindWalkerById(maskedId);
                        if (objToMove != null)
                        {
                            objToMove.Location = new Vector2(x, y);
                            _logger.LogDebug("Updated visual position for {Type} {Id:X4}", objToMove.GetType().Name, maskedId);
                        }
                    }
                });

                if (maskedId == _characterState.Id)
                {
                    _logger.LogInformation("🏃‍♂️ Local character moved to ({X},{Y})", x, y);
                    _characterState.UpdatePosition(x, y);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing ObjectMoved (0x15).");
            }
            return Task.CompletedTask;
        }

        [PacketHandler(0xD4, PacketRouter.NoSubCode)] // ObjectWalked
        public Task HandleObjectWalkedAsync(Memory<byte> packet)
        {
            if (packet.Length < 7) return Task.CompletedTask;

            var walk = new ObjectWalked(packet);
            ushort raw = walk.ObjectId;
            ushort maskedId = (ushort)(raw & 0x7FFF);
            byte x = walk.TargetX, y = walk.TargetY;

            _scopeManager.TryUpdateScopeObjectPosition(maskedId, x, y);

            MuGame.ScheduleOnMainThread(() =>
            {
                if (MuGame.Instance.ActiveScene?.World is not WalkableWorldControl world)
                    return;

                // ────────────────────────────────────────────────
                //  local player?  → do not override animation
                // ────────────────────────────────────────────────
                if (maskedId == _characterState.Id)
                {

                    var self = world.Walker;

                    if (self != null && self.NetworkId == maskedId)
                    {
                        self.MoveTo(new Vector2(x, y), sendToServer: false);
                        return;
                    }
                }

                if (!world.TryGetWalkerById(maskedId, out var walker) || walker == null)
                {
                    _logger.LogTrace("HandleObjectWalked: Walker {Id:X4} not found.", maskedId);
                    return;
                }

                walker.MoveTo(new Vector2(x, y), sendToServer: false);

                if (walker is PlayerObject player)
                {
                    bool isFemale = PlayerActionMapper.IsCharacterFemale(player.CharacterClass);
                    PlayerAction walkAction;

                    if (world.WorldIndex == 8) // Atlans
                    {
                        var flags = world.Terrain.RequestTerrainFlag(x, y);
                        if (flags.HasFlag(TWFlags.SafeZone))
                        {
                            walkAction = isFemale ? PlayerAction.PlayerWalkFemale : PlayerAction.PlayerWalkMale;
                        }
                        else if (player.HasEquippedWings)
                        {
                            walkAction = PlayerAction.PlayerFly;
                        }
                        else
                        {
                            walkAction = PlayerAction.PlayerRunSwim;
                        }
                    }
                    else if (world.WorldIndex == 11 || (world.WorldIndex == 1 && player.HasEquippedWings && !world.Terrain.RequestTerrainFlag(x, y).HasFlag(TWFlags.SafeZone)))
                    {
                        walkAction = PlayerAction.PlayerFly;
                    }
                    else
                    {
                        walkAction = isFemale ? PlayerAction.PlayerWalkFemale : PlayerAction.PlayerWalkMale;
                    }

                    if (player.CurrentAction != walkAction)
                    {
                        player.PlayAction((ushort)walkAction, fromServer: true);
                    }
                }
                else if (walker is MonsterObject)
                {
                    walker.PlayAction((ushort)MonsterActionType.Walk, fromServer: true);
                }
                else if (walker is NPCObject)
                {
                    const PlayerAction walkAction = PlayerAction.PlayerWalkMale;
                    if (walker.CurrentAction != (int)walkAction)
                        walker.PlayAction((ushort)walkAction, fromServer: true);
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
                    _logger.LogWarning("ObjectGotKilled packet too short: {Length}", packet.Length);
                    return Task.CompletedTask;
                }

                var death = new ObjectGotKilled(packet);
                ushort killed = death.KilledId;
                ushort killer = death.KillerId;

                string killerName = _scopeManager.TryGetScopeObjectName(killer, out var kn) ? (kn ?? "Unknown") : "Unknown";
                string killedName = _scopeManager.TryGetScopeObjectName(killed, out var kd) ? (kd ?? "Unknown") : "Unknown";

                if (killed == _characterState.Id)
                {
                    _logger.LogWarning("💀 You died! Killed by {Killer}", killerName);
                    _characterState.UpdateCurrentHealthShield(0, 0);

                    // CRITICAL: Don't remove local player from scope - let respawn handle it
                    // _scopeManager.RemoveObjectFromScope(killed); // REMOVED THIS LINE
                }
                else
                {
                    _logger.LogInformation("💀 {Killed} died. Killed by {Killer}", killedName, killerName);
                    _scopeManager.RemoveObjectFromScope(killed);
                }

                MuGame.ScheduleOnMainThread(() =>
                {
                    if (MuGame.Instance.ActiveScene?.World is not WorldControl world) return;

                    // Use same lookup as HandleObjectAnimation
                    var player = world.FindPlayerById(killed);

                    WalkerObject walker = null;
                    if (!world.TryGetWalkerById(killed, out walker) && player == null)
                    {
                        _logger.LogTrace("HandleObjectGotKilled: Walker with ID {Id:X4} not found in world.", killed);
                        return;
                    }

                    if (player != null)
                    {
                        walker = player;
                    }

                    if (walker != null)
                    {
                        // Handle local player death differently
                        if (killed == _characterState.Id && walker is PlayerObject localPlayer)
                        {
                            // Reset all animation states
                            localPlayer.IsResting = false;
                            localPlayer.IsSitting = false;
                            localPlayer.RestPlaceTarget = null;
                            localPlayer.SitPlaceTarget = null;

                            // Play death animation but DON'T remove from world
                            localPlayer.PlayAction((ushort)PlayerAction.PlayerDie1);
                            _logger.LogDebug("💀 Local player death animation started - staying in world for respawn");
                            return; // Don't remove local player
                        }

                        // Handle remote player death
                        if (walker is PlayerObject remotePlayer && !remotePlayer.IsMainWalker)
                        {
                            remotePlayer.IsResting = false;
                            remotePlayer.IsSitting = false;
                            remotePlayer.RestPlaceTarget = null;
                            remotePlayer.SitPlaceTarget = null;

                            remotePlayer.PlayAction((ushort)PlayerAction.PlayerDie1);
                            _logger.LogDebug("💀 Remote player {Name} ({Id:X4}) death animation started",
                                            remotePlayer.Name, killed);

                            // Remove after death animation
                            Task.Delay(3000).ContinueWith(_ =>
                            {
                                MuGame.ScheduleOnMainThread(() =>
                                {
                                    if (world.Objects.Contains(walker))
                                    {
                                        world.Objects.Remove(walker);
                                        walker.Dispose();
                                        _logger.LogDebug("💀 Removed dead remote player {Name} after animation",
                                                        remotePlayer.Name);
                                    }
                                });
                            });
                        }
                        // Handle monster death
                        else if (walker is MonsterObject monster)
                        {
                            monster.PlayAction((byte)MonsterActionType.Die);
                            monster.StartDeathFade();
                            _logger.LogDebug("💀 Monster {Id:X4} death animation started", killed);
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing ObjectGotKilled (0x17).");
            }
            return Task.CompletedTask;
        }

        [PacketHandler(0x18, PacketRouter.NoSubCode)] // ObjectAnimation
        public Task HandleObjectAnimationAsync(Memory<byte> packet)
        {
            var anim = new ObjectAnimation(packet);
            ushort rawId = anim.ObjectId;
            ushort maskedId = (ushort)(rawId & 0x7FFF);
            byte serverActionId = anim.Animation;
            byte serverDirection = anim.Direction;
            ushort targetId = anim.TargetId;

            MuGame.ScheduleOnMainThread(() =>
            {
                if (MuGame.Instance.ActiveScene?.World is not WorldControl world) return;

                var player = world.FindPlayerById(maskedId);

                if (!world.TryGetWalkerById(maskedId, out var walker) && player == null)
                {
                    _logger.LogTrace("HandleObjectAnimation: Walker with MaskedID {MaskedId} (RawID {RawId}) not found in world.", maskedId, rawId);
                    return;
                }

                if (player != null)
                {
                    walker = player;
                }

                if (walker == null || walker.Status == GameControlStatus.Disposed)
                {
                    _logger.LogWarning("HandleObjectAnimation: Walker {MaskedId} is null or disposed, cannot animate.", maskedId);
                    return;
                }

                PlayerAction clientActionToPlay;
                string actionNameForLog;
                MonsterActionType? monsterAction = null;

                if (walker is PlayerObject playerToAnimate)
                {
                    CharacterClassNumber playerClass = playerToAnimate.CharacterClass;
                    clientActionToPlay = PlayerActionMapper.GetClientAction(serverActionId, playerClass);
                    actionNameForLog = clientActionToPlay.ToString();
                }
                else if (walker is MonsterObject monsterToAnimate)
                {
                    byte actionIdx = (byte)((serverActionId & 0xE0) >> 5);
                    var action = (MonsterActionType)actionIdx;

                    if (action is MonsterActionType.Attack1 or MonsterActionType.Attack2) // It was always attack1
                    {
                        action = MuGame.Random.Next(2) == 0
                            ? MonsterActionType.Attack1
                            : MonsterActionType.Attack2;
                        actionIdx = (byte)action;
                    }

                    clientActionToPlay = (PlayerAction)action;
                    actionNameForLog = action.ToString();
                    monsterAction = action;

                    if (monsterAction == MonsterActionType.Attack1 || monsterAction == MonsterActionType.Attack2)
                    {
                        monsterToAnimate.LastAttackTargetId = targetId;
                    }
                }
                else
                {
                    _logger.LogWarning("HandleObjectAnimation: Walker {MaskedId} is not PlayerObject or MonsterObject. Type: {WalkerType}", maskedId, walker.GetType().Name);
                    return;
                }

                Client.Main.Models.Direction clientDirection = (Client.Main.Models.Direction)serverDirection;

                if (maskedId == _characterState.Id && walker is PlayerObject localPlayer)
                {
                    localPlayer.Direction = clientDirection;
                    localPlayer.PlayAction((ushort)clientActionToPlay, fromServer: true); // <-- Dodaj fromServer: true
                    _logger.LogInformation("🎞️ Animation (LocalPlayer {Id:X4}): Action: {ActionName} ({ClientAction}), ServerActionID: {ServerActionId}, Dir: {Direction}",
                        maskedId, actionNameForLog, clientActionToPlay, serverActionId, clientDirection);
                }
                else
                {
                    walker.Direction = clientDirection;

                    walker.PlayAction((ushort)clientActionToPlay, fromServer: true);

                    if (walker is MonsterObject monster && monsterAction.HasValue &&
                        (monsterAction == MonsterActionType.Attack1 || monsterAction == MonsterActionType.Attack2))
                    {
                        monster.OnPerformAttack(monsterAction == MonsterActionType.Attack1 ? 1 : 2);
                    }

                    _logger.LogInformation("🎞️ Animation ({WalkerType} {Id:X4}): Action: {ActionName} ({ClientAction}), ServerActionID: {ServerActionId}, Dir: {Direction}",
                       walker.GetType().Name, maskedId, actionNameForLog, clientActionToPlay, serverActionId, clientDirection);
                }
            });

            return Task.CompletedTask;
        }


        [PacketHandler(0x65, PacketRouter.NoSubCode)] // AssignCharacterToGuild
        public Task HandleAssignCharacterToGuildAsync(Memory<byte> packet)
        {
            try
            {
                var assign = new AssignCharacterToGuild(packet);
                _logger.LogInformation("🛡️ AssignCharacterToGuild: {Count} players.", assign.PlayerCount);
                for (int i = 0; i < assign.PlayerCount; i++)
                {
                    var rel = assign[i];
                    ushort rawId = rel.PlayerId;
                    ushort maskedId = (ushort)(rawId & 0x7FFF);
                    _logger.LogDebug(
                        "Player {Player:X4} (Raw: {Raw:X4}) in Guild {GuildId}, Role {Role}",
                        maskedId, rawId, rel.GuildId, rel.Role);
                    // TODO: update guild info in _scopeManager
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing AssignCharacterToGuild (0x65).");
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
                    _logger.LogWarning("GuildMemberLeftGuild packet too short: {Length}", packet.Length);
                    return Task.CompletedTask;
                }
                var left = new GuildMemberLeftGuild(packet);
                ushort rawId = left.PlayerId;
                ushort maskedId = (ushort)(rawId & 0x7FFF);
                _logger.LogInformation(
                    "🚶 Player {Id:X4} left guild (GM: {IsGM}).",
                    maskedId, left.IsGuildMaster
                );
                // TODO: clear guild info in _scopeManager
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing GuildMemberLeftGuild (0x5D).");
            }
            return Task.CompletedTask;
        }

        private async Task ProcessDroppedItemAsync(ScopeObject dropObj, ushort maskedId, string soundPath)
        {
            // Add to world on main thread first, then load assets
            var tcs = new TaskCompletionSource<bool>();

            MuGame.ScheduleOnMainThread(() =>
            {
                ProcessDroppedItemOnMainThread(dropObj, maskedId, soundPath, tcs);
            });

            await tcs.Task;
        }

        private void ProcessDroppedItemOnMainThread(ScopeObject dropObj, ushort maskedId, string soundPath, TaskCompletionSource<bool> tcs)
        {
            try
            {
                if (MuGame.Instance.ActiveScene?.World is not WalkableWorldControl world)
                {
                    tcs.SetResult(false);
                    return;
                }

                // Remove existing visual object if it's already there
                var existing = world.FindDroppedItemById(maskedId);
                if (existing != null)
                {
                    world.Objects.Remove(existing);
                    existing.Recycle();
                }

                var obj = DroppedItemObject.Rent(dropObj, _characterState.Id, _networkManager.GetCharacterService(), _loggerFactory.CreateLogger<DroppedItemObject>());

                // Set World property before adding to world objects
                obj.World = world;

                // Add to world so World.Scene is available
                world.Objects.Add(obj);

                // Queue load to avoid long stalls on the main thread
                bool enqueued = MuGame.TaskScheduler.QueueTask(async () =>
                {
                    try
                    {
                        await obj.Load();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error loading dropped item assets for {maskedId:X4}");
                        world.Objects.Remove(obj);
                        obj.Recycle();
                        tcs.TrySetResult(false);
                        return;
                    }

                    // Play drop sound
                    SoundController.Instance.PlayBufferWithAttenuation(soundPath, obj.Position, world.Walker.Position);

                    // Don't set Hidden immediately - let WorldObject.Update handle visibility checks
                    // The immediate visibility check was causing items to be Hidden incorrectly
                    _logger.LogDebug($"Spawned dropped item ({obj.DisplayName}) at {obj.Position.X},{obj.Position.Y},{obj.Position.Z}. RawId: {obj.RawId:X4}, MaskedId: {obj.NetworkId:X4}");
                    tcs.TrySetResult(true);
                }, Controllers.TaskScheduler.Priority.Low);

                if (!enqueued)
                {
                    _logger.LogWarning("Failed to queue dropped item load task for {Id:X4} – scheduler at capacity.", maskedId);
                    world.Objects.Remove(obj);
                    obj.Recycle();
                    tcs.TrySetResult(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing dropped item on main thread for {maskedId:X4}");
                tcs.TrySetResult(false);
            }
        }

        private static byte ConvertGlowToItemLevel(byte glowLevel)
        {
            return glowLevel switch
            {
                0 => 0,
                1 => 3,
                2 => 5,
                3 => 7,
                4 => 9,
                5 => 11,
                6 => 13,
                7 => 15,
                _ => 0
            };
        }
    }
}
