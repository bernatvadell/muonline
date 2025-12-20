using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Client.Main.Controllers;
using Client.Main.Core.Models;
using Client.Main.Core.Utilities;
using Client.Main.Models;
using Client.Main.Objects;
using Client.Main.Objects.Effects;
using Client.Main.Objects.Player;
using Client.Main.Networking.PacketHandling.Handlers;
using Client.Main.Worlds;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Xna.Framework;
using Client.Main.Controls;
using TaskScheduler = Client.Main.Controllers.TaskScheduler;

namespace Client.Main.Scenes
{
    internal sealed class GameSceneScopeImportController
    {
        private readonly GameScene _scene;
        private readonly ILogger _logger;
        private readonly HashSet<ushort> _activePlayerIds = new();
        private readonly HashSet<ushort> _activeMonsterIds = new();
        private readonly HashSet<ushort> _activeNpcIds = new();
        private readonly HashSet<ushort> _activeItemIds = new();

        public GameSceneScopeImportController(GameScene scene, ILogger logger)
        {
            _scene = scene ?? throw new ArgumentNullException(nameof(scene));
            _logger = logger ?? NullLogger<GameSceneScopeImportController>.Instance;
        }

        public Task ImportPendingNpcsMonstersAsync()
        {
            if (_scene.World is not WalkableWorldControl w) return Task.CompletedTask;
            var list = ScopeHandler.TakePendingNpcsMonsters();
            if (list.Count == 0) return Task.CompletedTask;

            foreach (var s in list)
            {
                if (_activeNpcIds.Contains(s.Id) || _activeMonsterIds.Contains(s.Id)) continue;

                if (!NpcDatabase.TryGetNpcType(s.TypeNumber, out Type objectType)) continue;
                if (Activator.CreateInstance(objectType) is WalkerObject npcMonster)
                {
                    npcMonster.NetworkId = s.Id;
                    npcMonster.Location = new Vector2(s.PositionX, s.PositionY);
                    npcMonster.Direction = (Models.Direction)s.Direction;
                    npcMonster.World = w;

                    MuGame.TaskScheduler.QueueTask(async () =>
                    {
                        try
                        {
                            await npcMonster.Load();
                            w.Objects.Add(npcMonster);

                            if (npcMonster is MonsterObject)
                                _activeMonsterIds.Add(s.Id);
                            else
                                _activeNpcIds.Add(s.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, $"Error loading pending NPC/Monster {s.Id:X4}");
                            npcMonster.Dispose();
                        }
                    }, TaskScheduler.Priority.High);
                }
            }

            return Task.CompletedTask;
        }

        public Task ImportPendingRemotePlayersAsync()
        {
            if (_scene.World is not WalkableWorldControl w) return Task.CompletedTask;
            var list = ScopeHandler.TakePendingPlayers();

            var heroId = MuGame.Network.GetCharacterState().Id;
            foreach (var s in list)
            {
                if (s.Id == heroId) continue;

                if (_activePlayerIds.Contains(s.Id)) continue;

                var remote = new PlayerObject(new AppearanceData(s.AppearanceData))
                {
                    NetworkId = s.Id,
                    Name = s.Name,
                    CharacterClass = s.Class,
                    Location = new Vector2(s.PositionX, s.PositionY),
                    World = w
                };

                MuGame.TaskScheduler.QueueTask(async () =>
                {
                    try
                    {
                        await remote.Load();
                        w.Objects.Add(remote);

                        _activePlayerIds.Add(s.Id);

                        ElfBuffEffectManager.Instance?.EnsureBuffsForPlayer(s.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, $"Error loading pending remote player {s.Name} ({s.Id:X4})");
                        remote.Dispose();
                    }
                }, TaskScheduler.Priority.High);
            }

            return Task.CompletedTask;
        }

        public Task ImportPendingDroppedItemsAsync()
        {
            if (_scene.World is not WalkableWorldControl w) return Task.CompletedTask;

            var scopeManager = MuGame.Network?.GetScopeManager();
            if (scopeManager == null) return Task.CompletedTask;

            var allDrops = scopeManager.GetScopeItems(ScopeObjectType.Item)
                                       .Concat(scopeManager.GetScopeItems(ScopeObjectType.Money))
                                       .Cast<ScopeObject>();

            foreach (var s in allDrops)
            {
                if (_activeItemIds.Contains(s.Id))
                    continue;

                MuGame.ScheduleOnMainThread(() =>
                {
                    if (w.Status != GameControlStatus.Ready ||
                        _activeItemIds.Contains(s.Id))
                        return;

                    var obj = DroppedItemObject.Rent(
                        s,
                        MuGame.Network.GetCharacterState().Id,
                        MuGame.Network.GetCharacterService(),
                        MuGame.AppLoggerFactory.CreateLogger<DroppedItemObject>());

                    obj.World = w;
                    w.Objects.Add(obj);

                    _activeItemIds.Add(s.Id);

                    MuGame.TaskScheduler.QueueTask(async () =>
                    {
                        try
                        {
                            await obj.Load();
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, $"Error loading pending dropped item {s.Id:X4}");
                            w.Objects.Remove(obj);
                            _activeItemIds.Remove(s.Id);
                            obj.Recycle();
                        }
                    }, TaskScheduler.Priority.Low);
                });
            }

            return Task.CompletedTask;
        }

        public void ClearObjectTracking()
        {
            if (_scene.World?.Objects != null)
            {
                var objectsToRemove = new List<WorldObject>();

                foreach (var obj in _scene.World.Objects.ToList())
                {
                    if (obj == _scene.Hero) continue;

                    objectsToRemove.Add(obj);
                }

                foreach (var obj in objectsToRemove)
                {
                    _scene.World.Objects.Remove(obj);
                    if (obj is DroppedItemObject drop)
                        drop.Recycle();
                    else
                        obj.Dispose();
                }

                _logger?.LogDebug("ClearObjectTracking: Removed {Count} objects from previous map", objectsToRemove.Count);
            }

            _activePlayerIds.Clear();
            _activeMonsterIds.Clear();
            _activeNpcIds.Clear();
            _activeItemIds.Clear();

            var scopeManager = MuGame.Network?.GetScopeManager();
            if (scopeManager != null)
            {
                scopeManager.ClearDroppedItemsFromScope();
                _logger?.LogDebug("ClearObjectTracking: Manually cleared dropped items from ScopeManager");
            }
        }

        public void RemoveObjectFromTracking(ushort networkId)
        {
            _activePlayerIds.Remove(networkId);
            _activeMonsterIds.Remove(networkId);
            _activeNpcIds.Remove(networkId);
            _activeItemIds.Remove(networkId);
        }

        public void EnsureHeroNetworkId(ushort expectedId, string context = "")
        {
            if (_scene.Hero.NetworkId != expectedId)
            {
                _logger?.LogWarning($"NetworkId mismatch in {context}. Fixing: {_scene.Hero.NetworkId:X4} -> {expectedId:X4}");
                _scene.Hero.NetworkId = expectedId;
            }
        }

        public void EnsureWalkerNetworkId(WalkableWorldControl walkable, ushort expectedId, string context = "")
        {
            if (walkable?.Walker?.NetworkId != expectedId)
            {
                _logger?.LogWarning($"Walker NetworkId mismatch in {context}. Fixing: {walkable.Walker?.NetworkId:X4} -> {expectedId:X4}");
                if (walkable.Walker != null)
                {
                    walkable.Walker.NetworkId = expectedId;
                }
            }
        }
    }
}
