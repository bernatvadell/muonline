using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using Client.Main.Core.Utilities;
using Client.Main.Core.Models;
using System;
using System.Threading.Tasks;
using System.Linq;
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
using Client.Data.ATT;

namespace Client.Main.Networking.PacketHandling.Handlers
{
    /// <summary>
    /// Handles packets related to objects entering or leaving scope, moving, and dying.
    /// </summary>
    public class ScopeHandler : IGamePacketHandler
    {
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

            // Load assets in background
            try
            {
                await p.Load();
                _logger.LogDebug($"[Spawn] p.Load() completed for {name}.");
                // Skip preloading to avoid blocking
                _logger.LogDebug($"[Spawn] Skipping preloading for {name}.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[Spawn] Error loading assets for {name} ({maskedId:X4}).");
                p.Dispose();
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
                    // Dispose asynchronously to avoid blocking
                    _ = Task.Run(() => existingWalker.Dispose());
                }

                if (world.Objects.OfType<PlayerObject>().FirstOrDefault(pl => pl.NetworkId == maskedId) != null)
                {
                    _logger.LogWarning($"[Spawn] PlayerObject for {name} already exists. Aborting.");
                    p.Dispose();
                    return;
                }

                world.Objects.Add(p);
                _logger.LogDebug($"[Spawn] Added {name} to world.Objects.");

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
            _ = ParseAndAddNpcsToScopeWithStaggeringAsync(packet.ToArray());
            return Task.CompletedTask;
        }

        [PacketHandler(0x16, PacketRouter.NoSubCode)] // AddMonstersToScope
        public Task HandleAddMonstersToScopeAsync(Memory<byte> packet)
        {
            _ = ParseAndAddNpcsToScopeWithStaggeringAsync(packet.ToArray());
            return Task.CompletedTask;
        }

        private async Task ParseAndAddNpcsToScopeWithStaggeringAsync(byte[] packetData)
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

            var spawnTasks = new List<Task>();
            int currentPacketOffset = firstOffset;

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

                // Process NPC/Monster spawning asynchronously without blocking
                spawnTasks.Add(ProcessNpcSpawnAsync(maskedId, rawId, x, y, direction, type, name));

                // Process in batches to avoid overwhelming the system
                if (spawnTasks.Count >= 10)
                {
                    await Task.WhenAll(spawnTasks);
                    spawnTasks.Clear();
                    // Small delay to prevent overwhelming the system
                    await Task.Delay(1);
                }
            }

            // Process remaining tasks
            if (spawnTasks.Count > 0)
            {
                await Task.WhenAll(spawnTasks);
            }
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
                obj.Dispose();
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

                    // Remove and dispose asynchronously to avoid blocking
                    _ = Task.Run(() =>
                    {
                        existingWalker.Dispose();
                    });
                    worldRef.Objects.Remove(existingWalker);
                }

                // Quick check for duplicates using LINQ
                var duplicate = worldRef.Objects.OfType<WalkerObject>().FirstOrDefault(o => o.NetworkId == maskedId);
                if (duplicate != null)
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
            });
        }

        [PacketHandler(0x25, PacketRouter.NoSubCode)]
        public async Task HandleAppearanceChangedAsync(Memory<byte> packet)
        {
            try
            {
                const int EXPECTED_MIN_LENGTH = 13;
                const byte UNEQUIP_MARKER = 0xFF;
                const ushort ID_MASK = 0x7FFF;
                const int OBJECT_ID_OFFSET = 3;
                const int ITEM_GROUP_OFFSET = 5;
                const int SLOT_AND_GLOW_OFFSET = 6;
                const int ITEM_OPTIONS_OFFSET = 8;
                const int EXCELLENT_FLAGS_OFFSET = 9;
                const int ANCIENT_DISCRIMINATOR_OFFSET = 10;
                const int ANCIENT_SET_COMPLETE_OFFSET = 11;
                const int WEAPON_SLOT_THRESHOLD = 2;
                const int WEAPON_GROUP = 0;
                const int ARMOR_GROUP_OFFSET = 5;

                if (packet.Length < EXPECTED_MIN_LENGTH)
                {
                    _logger.LogWarning("AppearanceChanged packet has invalid length: {Length}. Expected at least {Expected}.", packet.Length, EXPECTED_MIN_LENGTH);
                        }

                var span = packet.Span;
                ushort rawKey = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(span.Slice(OBJECT_ID_OFFSET));
                ushort maskedId = (ushort)(rawKey & ID_MASK);

                bool isUnequip = span[ITEM_GROUP_OFFSET] == UNEQUIP_MARKER;
                byte itemSlot = (byte)((span[SLOT_AND_GLOW_OFFSET] >> 4) & 0x0F);

                if (isUnequip)
                {
                    await HandleUnequipAsync(maskedId, itemSlot);
                    return;
                }

                byte glowLevel = (byte)(span[SLOT_AND_GLOW_OFFSET] & 0x0F);
                byte itemGroup = itemSlot < WEAPON_SLOT_THRESHOLD ? (byte)WEAPON_GROUP : (byte)(itemSlot + ARMOR_GROUP_OFFSET);
                byte itemNumber = (byte)(span[ITEM_GROUP_OFFSET] & 0x0F);
                byte itemLevel = ConvertGlowToItemLevel(glowLevel);

                byte itemOptions = span[ITEM_OPTIONS_OFFSET];
                byte excellentFlags = span[EXCELLENT_FLAGS_OFFSET];
                byte ancientDiscriminator = span[ANCIENT_DISCRIMINATOR_OFFSET];
                bool isAncientSetComplete = span[ANCIENT_SET_COMPLETE_OFFSET] != 0;

                bool hasExcellent = excellentFlags != 0;
                bool hasAncient = ancientDiscriminator != 0 && excellentFlags != 0;

                const int MAX_ITEM_INDEX = 512;
                int finalItemType = (itemGroup * MAX_ITEM_INDEX) + itemNumber;

                _logger.LogDebug("Parsed AppearanceChanged for ID {Id:X4}: Slot={Slot}, Group={Group}, Number={Number}, Type={Type}, Level={Level}",
                    maskedId, itemSlot, itemGroup, itemNumber, finalItemType, itemLevel);

                await HandleEquipAsync(maskedId, itemSlot, itemGroup, itemNumber, finalItemType, itemLevel, 
                    itemOptions, hasExcellent ? excellentFlags : (byte)0, 
                    hasAncient ? ancientDiscriminator : (byte)0, isAncientSetComplete);
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

        private Task HandleEquipAsync(ushort maskedId, byte itemSlot, byte itemGroup, byte itemNumber, 
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
                if (packet.Length < ObjectHit.Length)
                {
                    _logger.LogWarning("ObjectHit packet (0x11) too short: {Length}", packet.Length);
                    return Task.CompletedTask;
                }

                var hitInfo = new ObjectHit(packet);
                ushort rawId = hitInfo.ObjectId;
                ushort maskedId = (ushort)(rawId & 0x7FFF);
                uint healthDmg = hitInfo.HealthDamage;
                uint shieldDmg = hitInfo.ShieldDamage;
                uint totalDmg = healthDmg + shieldDmg;

                // Log damage event
                string objectName = _scopeManager.TryGetScopeObjectName(maskedId, out var nm) ? (nm ?? "Object") : "Object";
                _logger.LogInformation(
                    "💥 {ObjectName} (ID: {Id:X4}) received hit: HP {HpDmg}, SD {SdDmg}",
                    objectName, maskedId, healthDmg, shieldDmg
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

                    Color dmgColor;
                    if (totalDmg == 0)
                    {
                        dmgColor = Color.White;
                    }
                    else if (maskedId == _characterState.Id)
                    {
                        dmgColor = Color.Red;
                    }
                    else
                    {
                        dmgColor = Color.Orange;
                    }

                    var txt = new DamageTextObject(
                        totalDmg == 0 ? "Miss" : totalDmg.ToString(),
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
                                    ? "Sound/pGem.wav"
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
                                        ? "Sound/pGem.wav"
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
                        var obj = world.Objects
                                       .OfType<DroppedItemObject>()
                                       .FirstOrDefault(d => d.NetworkId == masked);
                        if (obj != null)
                        {
                            world.Objects.Remove(obj);
                            // Dispose asynchronously to avoid blocking
                            _ = Task.Run(() => obj.Dispose());
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
                        var player = world.Objects
                                          .OfType<PlayerObject>()
                                          .FirstOrDefault(p => p.NetworkId == masked);
                        if (player != null)
                        {
                            world.Objects.Remove(player);
                            // Dispose asynchronously to avoid blocking
                            _ = Task.Run(() => player.Dispose());
                            continue;
                        }

                        // ---- 2) Walker / NPC --------------------------------------------
                        var walker = world.Objects
                                          .OfType<WalkerObject>()
                                          .FirstOrDefault(w => w.NetworkId == masked);
                        if (walker != null)
                        {
                            world.Objects.Remove(walker);
                            // Dispose asynchronously to avoid blocking
                            _ = Task.Run(() => walker.Dispose());
                            continue;
                        }

                        // ---- 3) Dropped item --------------------------------------------
                        var drop = world.Objects
                                         .OfType<DroppedItemObject>()
                                         .FirstOrDefault(d => d.NetworkId == masked);
                        if (drop != null)
                        {
                            world.Objects.Remove(drop);
                            // Dispose asynchronously to avoid blocking
                            _ = Task.Run(() => drop.Dispose());
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
                        var objToMove = world.Objects.OfType<WalkerObject>()
                                            .FirstOrDefault(w => w.NetworkId == maskedId);
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
                        walkAction = flags.HasFlag(TWFlags.SafeZone)
                            ? (isFemale ? PlayerAction.PlayerWalkFemale : PlayerAction.PlayerWalkMale)
                            : PlayerAction.PlayerRunSwim;
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
                    var player = world.Objects.OfType<PlayerObject>()
                                   .FirstOrDefault(p => p.NetworkId == killed);

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

            MuGame.ScheduleOnMainThread(() =>
            {
                if (MuGame.Instance.ActiveScene?.World is not WorldControl world) return;

                var player = world.Objects.OfType<PlayerObject>()
                               .FirstOrDefault(p => p.NetworkId == maskedId);

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

                    walker.PlayAction((ushort)clientActionToPlay, fromServer: true); // <-- Dodaj fromServer: true

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

        private async void ProcessDroppedItemOnMainThread(ScopeObject dropObj, ushort maskedId, string soundPath, TaskCompletionSource<bool> tcs)
        {
            try
            {
                if (MuGame.Instance.ActiveScene?.World is not WalkableWorldControl world)
                {
                    tcs.SetResult(false);
                    return;
                }

                // Remove existing visual object if it's already there
                var existing = world.Objects.OfType<DroppedItemObject>().FirstOrDefault(d => d.NetworkId == maskedId);
                if (existing != null)
                {
                    world.Objects.Remove(existing);
                    // Dispose asynchronously to avoid blocking
                    _ = Task.Run(() => existing.Dispose());
                }

                var obj = new DroppedItemObject(dropObj, _characterState.Id, _networkManager.GetCharacterService(), _loggerFactory.CreateLogger<DroppedItemObject>());

                // Set World property before adding to world objects
                obj.World = world;

                // Add to world so World.Scene is available
                world.Objects.Add(obj);

                // Load assets
                try
                {
                    await obj.Load();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error loading dropped item assets for {maskedId:X4}");
                    world.Objects.Remove(obj);
                    obj.Dispose();
                    tcs.SetResult(false);
                    return;
                }

                // Play drop sound
                SoundController.Instance.PlayBufferWithAttenuation(soundPath, obj.Position, world.Walker.Position);

                // Initial visibility check
                obj.Hidden = !world.IsObjectInView(obj);
                _logger.LogDebug($"Spawned dropped item ({obj.DisplayName}) at {obj.Position.X},{obj.Position.Y},{obj.Position.Z}. RawId: {obj.RawId:X4}, MaskedId: {obj.NetworkId:X4}");

                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing dropped item on main thread for {maskedId:X4}");
                tcs.SetResult(false);
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