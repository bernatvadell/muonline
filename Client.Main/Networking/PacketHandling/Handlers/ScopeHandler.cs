using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Network.Packets.ServerToClient;
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
using Client.Main.Objects.Monsters;
using Client.Main.Objects;
using Client.Main.Objects.Effects;
using Client.Main.Core.Client;
using Client.Main.Scenes;
using Client.Main.Controllers;

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
            TargetProtocolVersion targetVersion)
        {
            _logger = loggerFactory.CreateLogger<ScopeHandler>();
            _scopeManager = scopeManager;
            _characterState = characterState;
            _networkManager = networkManager;
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
                        SpawnRemotePlayerIntoWorld(w, masked, raw, c.CurrentPositionX, c.CurrentPositionY, c.Name, cls);
                    }
                }
                else if (masked != _characterState.Id)
                {
                    lock (_pendingPlayers)
                    {
                        if (!_pendingPlayers.Any(p => p.Id == masked))
                        {
                            _pendingPlayers.Add(new PlayerScopeObject(masked, raw, c.CurrentPositionX, c.CurrentPositionY, c.Name, cls));
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

        private static void SpawnRemotePlayerIntoWorld(
                   WalkableWorldControl world,
                   ushort maskedId,
                   ushort rawId,
                   byte x,
                   byte y,
                   string name,
                   CharacterClassNumber cls)
        {
            MuGame.ScheduleOnMainThread(async () =>
            {
                if (MuGame.Instance.ActiveScene?.World != world || world.Status != GameControlStatus.Ready)
                {
                    return;
                }

                if (world.WalkerObjectsById.TryGetValue(maskedId, out WalkerObject existingWalker))
                {
                    // GameScene._logger.LogWarning($"SpawnRemotePlayer: Stale PlayerObject {maskedId:X4} found. Removing before adding new.");
                    world.Objects.Remove(existingWalker);
                    existingWalker.Dispose();
                }

                if (world.Objects.OfType<PlayerObject>().Any(p => p.NetworkId == maskedId))
                    return;

                var p = new PlayerObject
                {
                    NetworkId = maskedId,
                    CharacterClass = cls,
                    Name = name,
                    Location = new Vector2(x, y)
                };

                world.Objects.Add(p);

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

                await p.Load();
            });
        }

        [PacketHandler(0x13, PacketRouter.NoSubCode)] // AddNpcToScope
        public Task HandleAddNpcToScopeAsync(Memory<byte> packet)
        {
            _ = Task.Run(() => ParseAndAddNpcsToScopeWithStaggering(packet.ToArray()));
            return Task.CompletedTask;
        }

        [PacketHandler(0x16, PacketRouter.NoSubCode)] // AddMonstersToScope
        public Task HandleAddMonstersToScopeAsync(Memory<byte> packet)
        {
            _ = Task.Run(() => ParseAndAddNpcsToScopeWithStaggering(packet.ToArray()));
            return Task.CompletedTask;
        }

        private void ParseAndAddNpcsToScopeWithStaggering(byte[] packetData)
        {
            Memory<byte> packet = packetData;
            int npcCount = 0, firstOffset = 0, dataSize = 0;
            Func<Memory<byte>, (ushort id, ushort type, byte x, byte y)> readNpc = null!;

            switch (_targetVersion)
            {
                case TargetProtocolVersion.Season6:
                    var s6 = new AddNpcsToScope(packet);
                    npcCount = s6.NpcCount;
                    firstOffset = 5;
                    dataSize = AddNpcsToScope.NpcData.Length;
                    readNpc = m => { var d = new AddNpcsToScope.NpcData(m); return (d.Id, d.TypeNumber, d.CurrentPositionX, d.CurrentPositionY); };
                    break;
                case TargetProtocolVersion.Version097:
                    var v97 = new AddNpcsToScope095(packet);
                    npcCount = v97.NpcCount;
                    firstOffset = 5;
                    dataSize = AddNpcsToScope095.NpcData.Length;
                    readNpc = m => { var d = new AddNpcsToScope095.NpcData(m); return (d.Id, d.TypeNumber, d.CurrentPositionX, d.CurrentPositionY); };
                    break;
                case TargetProtocolVersion.Version075:
                    var v75 = new AddNpcsToScope075(packet);
                    npcCount = v75.NpcCount;
                    firstOffset = 5;
                    dataSize = AddNpcsToScope075.NpcData.Length;
                    readNpc = m => { var d = new AddNpcsToScope075.NpcData(m); return (d.Id, d.TypeNumber, d.CurrentPositionX, d.CurrentPositionY); };
                    break;
                default:
                    _logger.LogWarning("Unsupported protocol version {Version} for AddNpcToScope.", _targetVersion);
                    return;
            }

            _logger.LogInformation("ScopeHandler: AddNpcToScope received {Count} objects.", npcCount);

            int currentPacketOffset = firstOffset;
            for (int i = 0; i < npcCount; i++)
            {
                if (currentPacketOffset + dataSize > packet.Length)
                {
                    _logger.LogWarning("ScopeHandler: Packet too short for NPC data at index {Index}.", i);
                    break;
                }

                var (rawId, type, x, y) = readNpc(packet.Slice(currentPacketOffset));
                currentPacketOffset += dataSize;

                ushort maskedId = (ushort)(rawId & 0x7FFF);
                string name = NpcDatabase.GetNpcName(type);

                _scopeManager.AddOrUpdateNpcInScope(maskedId, rawId, x, y, type, name);

                MuGame.ScheduleOnMainThread(async () =>
                {
                    if (MuGame.Instance.ActiveScene?.World is WalkableWorldControl worldRef && worldRef.Status == GameControlStatus.Ready)
                    {
                        if (worldRef.WalkerObjectsById.TryGetValue(maskedId, out WalkerObject existingWalker))
                        {
                            // An object with this ID already exists in the dictionary.
                            // This could be a stale object or a rapid respawn with ID reuse.
                            _logger.LogWarning($"ScopeHandler: Stale/Duplicate NPC/Monster ID {maskedId:X4} ({existingWalker.GetType().Name}) found in WalkerObjectsById. Removing it before adding new {name} (Type: {type}).");

                            // Remove from visual list (triggers OnObjectRemoved in WorldControl)
                            worldRef.Objects.Remove(existingWalker);
                            existingWalker.Dispose(); // Dispose the old instance.
                        }
                        // else: No existing walker with this ID in the dictionary, proceed to add.

                        if (NpcDatabase.TryGetNpcType(type, out var npcClassType))
                        {
                            // Check again if it was added by another thread in the meantime, though less likely now with prior check
                            if (worldRef.Objects.OfType<WalkerObject>().Any(o => o.NetworkId == maskedId)) return;

                            if (Activator.CreateInstance(npcClassType) is WalkerObject obj)
                            {
                                obj.NetworkId = maskedId;
                                obj.Location = new Vector2(x, y);

                                worldRef.Objects.Add(obj);

                                if (obj.World?.Terrain != null)
                                {
                                    obj.MoveTargetPosition = obj.TargetPosition;
                                    obj.Position = obj.TargetPosition;
                                }
                                else
                                {
                                    _logger.LogError($"ScopeHandler: obj.World or obj.World.Terrain is null for NPC/Monster {maskedId} ({obj.GetType().Name}) AFTER adding to worldRef.Objects. This should not happen.");
                                    // Awaryjne ustawienie, jeśli teren nie jest dostępny
                                    float worldX = obj.Location.X * Constants.TERRAIN_SCALE + 0.5f * Constants.TERRAIN_SCALE;
                                    float worldY = obj.Location.Y * Constants.TERRAIN_SCALE + 0.5f * Constants.TERRAIN_SCALE;
                                    obj.MoveTargetPosition = new Vector3(worldX, worldY, 0); // Z = 0 jako fallback
                                    obj.Position = obj.MoveTargetPosition;
                                }

                                try
                                {
                                    await obj.Load();
                                    if (obj.Status != GameControlStatus.Ready)
                                    {
                                        _logger.LogWarning($"ScopeHandler: NPC/Monster {maskedId} ({obj.GetType().Name}) loaded but status is {obj.Status}.");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, $"ScopeHandler: Error loading NPC/Monster {maskedId} ({obj.GetType().Name}).");
                                }
                            }
                            else
                            {
                                _logger.LogWarning($"ScopeHandler: Could not create instance of NPC type {npcClassType} for TypeID {type}.");
                            }
                        }
                        else
                        {
                            _logger.LogWarning($"ScopeHandler: NPC type not found in NpcDatabase for TypeID {type}.");
                        }
                    }
                    else
                    {
                        lock (_pendingNpcsMonsters)
                        {
                            if (!_pendingNpcsMonsters.Any(p => p.Id == maskedId))
                            {
                                _pendingNpcsMonsters.Add(new NpcScopeObject(maskedId, rawId, x, y, type, name));
                            }
                        }
                    }
                });
            }
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

                    var txt = new DamageTextObject(
                        totalDmg == 0 ? "Miss" : totalDmg.ToString(),
                        maskedId,
                        totalDmg == 0 ? Color.White : Color.Red
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

                        // Schedule on main thread for visual update and sound
                        MuGame.ScheduleOnMainThread(async () =>
                        {
                            if (MuGame.Instance.ActiveScene?.World is not WalkableWorldControl w) return;
                            // Remove existing visual object if it's already there (e.g., from a previous packet re-send)
                            var existing = w.Objects.OfType<DroppedItemObject>().FirstOrDefault(d => d.NetworkId == maskedId);
                            if (existing != null)
                            {
                                w.Objects.Remove(existing);
                                existing.Dispose();
                            }
                            // Create and add the new visual object
                            var obj = new DroppedItemObject(dropObj, _characterState.Id, _networkManager.GetCharacterService(), _loggerFactory.CreateLogger<DroppedItemObject>());
                            w.Objects.Add(obj); // Add the object to the world's objects collection
                            await obj.Load(); // Ensure its assets are loaded

                            SoundController.Instance.PlayBufferWithAttenuation("Sound/pDropMoney.wav", obj.Position, w.Walker.Position); // Play money drop sound
                            // Initial visibility check (hide if too far or out of view initially)
                            obj.Hidden = !w.IsObjectInView(obj);
                            _logger.LogDebug($"Spawned dropped money ({obj.DisplayName}) at {obj.Position.X},{obj.Position.Y},{obj.Position.Z}. RawId: {obj.RawId:X4}, MaskedId: {obj.NetworkId:X4}");
                        });
                    }
                    else
                    {
                        dropObj = new ItemScopeObject(maskedId, rawId, x, y, data.ToArray());
                        _scopeManager.AddOrUpdateItemInScope(maskedId, rawId, x, y, data.ToArray());
                        _logger.LogDebug("Dropped Item: ID={Id:X4}, DataLen={Len}", maskedId, data.Length);

                        // Schedule on main thread for visual update and sound
                        MuGame.ScheduleOnMainThread(async () =>
                        {
                            if (MuGame.Instance.ActiveScene?.World is not WalkableWorldControl w) return;
                            // Remove existing visual object if it's already there
                            var existing = w.Objects.OfType<DroppedItemObject>().FirstOrDefault(d => d.NetworkId == maskedId);
                            if (existing != null)
                            {
                                w.Objects.Remove(existing);
                                existing.Dispose();
                            }

                            // Play drop sound based on item type (Jewel vs. Generic)
                            byte[] dataCopy = item.ItemData.ToArray(); // Create a defensive copy
                            string itemName = ItemDatabase.GetItemName(dataCopy) ?? string.Empty;

                            var obj = new DroppedItemObject(dropObj, _characterState.Id, _networkManager.GetCharacterService(), _loggerFactory.CreateLogger<DroppedItemObject>());
                            w.Objects.Add(obj); // Add the object to the world's objects collection
                            await obj.Load(); // Ensure its assets are loaded

                            if (itemName.StartsWith("Jewel", StringComparison.OrdinalIgnoreCase))
                            {
                                SoundController.Instance.PlayBufferWithAttenuation("Sound/pGem.wav", obj.Position, w.Walker.Position); // Play jewel drop sound
                            }
                            else
                            {
                                SoundController.Instance.PlayBufferWithAttenuation("Sound/pDropItem.wav", obj.Position, w.Walker.Position); // Play generic item drop sound
                            }
                            // Initial visibility check
                            obj.Hidden = !w.IsObjectInView(obj);
                            _logger.LogDebug($"Spawned dropped item ({obj.DisplayName}) at {obj.Position.X},{obj.Position.Y},{obj.Position.Z}. RawId: {obj.RawId:X4}, MaskedId: {obj.NetworkId:X4}");
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

                        MuGame.ScheduleOnMainThread(async () =>
                        {
                            if (MuGame.Instance.ActiveScene?.World is not WalkableWorldControl w) return;
                            var existing = w.Objects.OfType<DroppedItemObject>().FirstOrDefault(d => d.NetworkId == maskedId);
                            if (existing != null)
                            {
                                w.Objects.Remove(existing);
                                existing.Dispose();
                            }
                            var newScopeObject = new MoneyScopeObject(maskedId, rawId, x, y, amount);
                            var obj = new DroppedItemObject(newScopeObject, _characterState.Id, _networkManager.GetCharacterService());
                            w.Objects.Add(obj);
                            await obj.Load();
                            SoundController.Instance.PlayBufferWithAttenuation("Sound/pDropMoney.wav", obj.Position, w.Walker.Position); // Sound for money
                            obj.Hidden = !w.IsObjectInView(obj);
                            _logger.LogDebug($"Spawned dropped money (0.75) ({obj.DisplayName}) at {obj.Position.X},{obj.Position.Y},{obj.Position.Z}. RawId: {obj.RawId:X4}, MaskedId: {obj.NetworkId:X4}");
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

                            MuGame.ScheduleOnMainThread(async () =>
                            {
                                if (MuGame.Instance.ActiveScene?.World is not WalkableWorldControl w) return;
                                var existing = w.Objects.OfType<DroppedItemObject>().FirstOrDefault(d => d.NetworkId == maskedId);
                                if (existing != null)
                                {
                                    w.Objects.Remove(existing);
                                    existing.Dispose();
                                }
                                var newScopeObject = new ItemScopeObject(maskedId, rawId, x, y, data);
                                var obj = new DroppedItemObject(newScopeObject, _characterState.Id, _networkManager.GetCharacterService());
                                w.Objects.Add(obj);
                                await obj.Load();
                                string itemName = ItemDatabase.GetItemName(data) ?? string.Empty;
                                if (itemName.StartsWith("Jewel", StringComparison.OrdinalIgnoreCase))
                                {
                                    SoundController.Instance.PlayBufferWithAttenuation("Sound/pGem.wav", obj.Position, w.Walker.Position); // Sound for jewel
                                }
                                else
                                {
                                    SoundController.Instance.PlayBufferWithAttenuation("Sound/pDropItem.wav", obj.Position, w.Walker.Position); // Sound for generic item
                                }
                                obj.Hidden = !w.IsObjectInView(obj);
                                _logger.LogDebug($"Spawned dropped item (0.75) ({obj.DisplayName}) at {obj.Position.X},{obj.Position.Y},{obj.Position.Z}. RawId: {obj.RawId:X4}, MaskedId: {obj.NetworkId:X4}");
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

            for (int i = 0; i < count; i++)
            {
                try
                {
                    var entry = removed[i];
                    ushort rawId = entry.Id;
                    ushort masked = (ushort)(rawId & 0x7FFF);

                    _scopeManager.RemoveObjectFromScope(masked);

                    MuGame.ScheduleOnMainThread(() =>
                    {
                        if (MuGame.Instance.ActiveScene?.World is not WalkableWorldControl world) return;

                        var obj = world.Objects
                                       .OfType<DroppedItemObject>()
                                       .FirstOrDefault(d => d.NetworkId == masked);
                        if (obj != null)
                        {
                            world.Objects.Remove(obj);
                            _logger.LogDebug("Removed DroppedItemObject {Id:X4} from world (scope gone).", masked);
                        }
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error removing dropped item at idx {Idx}.", i);
                }
            }
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

            MuGame.ScheduleOnMainThread(() =>
            {
                if (MuGame.Instance.ActiveScene?.World is not WalkableWorldControl world) return;

                for (int i = 0; i < count; i++)
                {
                    ushort raw = outPkt[i].Id;
                    ushort masked = (ushort)(raw & 0x7FFF);

                    _scopeManager.RemoveObjectFromScope(masked);

                    // ---- 1) Walker / NPC / Player -----------------------------------
                    var walker = world.Objects
                                      .OfType<WalkerObject>()
                                      .FirstOrDefault(w => w.NetworkId == masked);
                    if (walker != null)
                    {
                        world.Objects.Remove(walker);
                        walker.Dispose();
                        continue;
                    }

                    // ---- 2) Dropped item --------------------------------------------
                    var drop = world.Objects
                                     .OfType<DroppedItemObject>()
                                     .FirstOrDefault(d => d.NetworkId == masked);
                    if (drop != null)
                    {
                        world.Objects.Remove(drop);
                        drop.Dispose();
                    }
                }
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

                bool isLocalPlayer = maskedId == _characterState.Id;

                // Local player movement
                var player = world.Objects.OfType<PlayerObject>()
                                   .FirstOrDefault(p => p.NetworkId == maskedId);
                if (player != null)
                {
                    bool wasResting = player.IsResting;
                    bool wasSitting = player.IsSitting;

                    if (wasResting || wasSitting)
                    {
                        player.IsResting = false;
                        player.IsSitting = false;
                        player.RestPlaceTarget = null;
                        player.SitPlaceTarget = null;
                        _logger.LogDebug("Reset rest/sit state for player {Id:X4} due to movement", maskedId);
                    }

                    player.MoveTo(new Vector2(x, y), sendToServer: false);

                    PlayerAction walkAction;
                    if (world.WorldIndex == 8) // Swimming world
                    {
                        walkAction = PlayerAction.RunSwim;
                    }
                    else if (world.WorldIndex == 11) // Flying world
                    {
                        walkAction = PlayerAction.Fly;
                    }
                    else
                    {
                        walkAction = PlayerActionMapper.IsCharacterFemale(player.CharacterClass)
                            ? PlayerAction.WalkFemale
                            : PlayerAction.WalkMale;
                    }

                    if (player.CurrentAction != walkAction)
                    {
                        player.PlayAction((ushort)walkAction);
                        // _logger.LogDebug("Set walk animation {WalkAction} for player {Id:X4}", walkAction, maskedId);
                    }

                    return;
                }

                // NPC/monster movement
                var walker = world.Objects.OfType<WalkerObject>()
                                 .FirstOrDefault(w => w.NetworkId == maskedId);
                if (walker != null)
                {
                    walker.MoveTo(new Vector2(x, y), sendToServer: false);

                    const byte walkActionNpc = (byte)MonsterActionType.Walk;
                    if (walker.Model?.Actions?.Length > walkActionNpc)
                    {
                        walker.PlayAction(walkActionNpc);
                        // _logger.LogDebug("Set walk animation for NPC/monster {Id:X4}", maskedId);
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
                            _logger.LogDebug("💀 Monster {Id:X4} death animation started", killed);

                            Task.Delay(2000).ContinueWith(_ =>
                            {
                                MuGame.ScheduleOnMainThread(() =>
                                {
                                    if (world.Objects.Contains(walker))
                                    {
                                        world.Objects.Remove(walker);
                                        walker.Dispose();
                                        _logger.LogDebug("💀 Removed dead monster {Id:X4}", killed);
                                    }
                                });
                            });
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

                if (walker is PlayerObject playerToAnimate)
                {
                    CharacterClassNumber playerClass = playerToAnimate.CharacterClass;
                    clientActionToPlay = PlayerActionMapper.GetClientAction(serverActionId, playerClass);
                    actionNameForLog = clientActionToPlay.ToString();
                }
                else if (walker is MonsterObject monsterToAnimate)
                {
                    byte actionIdx = (byte)((serverActionId & 0xE0) >> 5);
                    clientActionToPlay = (PlayerAction)(MonsterActionType)actionIdx;
                    actionNameForLog = ((MonsterActionType)actionIdx).ToString();
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
    }
}
