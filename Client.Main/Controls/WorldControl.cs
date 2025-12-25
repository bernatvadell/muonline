using Client.Data.ATT;
using Client.Data.CAP;
using Client.Data.OBJS;
using Client.Main.Controllers;
using Client.Main.Core.Utilities;
using Client.Main.Graphics;
using Client.Main.Helpers;
using Client.Main.Models;
using Client.Main.Objects;
using Client.Main.Objects.Effects;
using Client.Main.Objects.Player;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Client.Main.Controls
{
    // Comparers for sorting world objects by depth
    sealed class WorldObjectDepthAsc : IComparer<WorldObject>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(WorldObject a, WorldObject b) => a.Depth.CompareTo(b.Depth);
    }

    sealed class WorldObjectDepthDesc : IComparer<WorldObject>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(WorldObject a, WorldObject b) => b.Depth.CompareTo(a.Depth);
    }

    // Optimized comparer that sorts by Model+Texture first, then depth
    // This minimizes state changes and improves GPU cache coherency
    sealed class WorldObjectBatchOptimizedAsc : IComparer<WorldObject>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CompareRefs<T>(T a, T b) where T : class
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a is null) return -1;
            if (b is null) return 1;
            return RuntimeHelpers.GetHashCode(a).CompareTo(RuntimeHelpers.GetHashCode(b));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(WorldObject a, WorldObject b)
        {
            if (a is ModelObject ma && b is ModelObject mb)
            {
                // Prioritize by texture and blend state to minimize state changes
                int texCmp = CompareRefs(ma.GetSortTextureHint(), mb.GetSortTextureHint());
                if (texCmp != 0) return texCmp;

                int blendCmp = CompareRefs(ma.BlendState, mb.BlendState);
                if (blendCmp != 0) return blendCmp;

                int modelCmp = CompareRefs(ma.Model, mb.Model);
                if (modelCmp != 0) return modelCmp;
            }

            // Then by depth for correct rendering order
            return a.Depth.CompareTo(b.Depth);
        }
    }

    sealed class WorldObjectBatchOptimizedDesc : IComparer<WorldObject>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CompareRefs<T>(T a, T b) where T : class
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a is null) return -1;
            if (b is null) return 1;
            return RuntimeHelpers.GetHashCode(a).CompareTo(RuntimeHelpers.GetHashCode(b));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(WorldObject a, WorldObject b)
        {
            if (a is ModelObject ma && b is ModelObject mb)
            {
                // Prioritize by texture and blend state to minimize state changes
                int texCmp = CompareRefs(ma.GetSortTextureHint(), mb.GetSortTextureHint());
                if (texCmp != 0) return texCmp;

                int blendCmp = CompareRefs(ma.BlendState, mb.BlendState);
                if (blendCmp != 0) return blendCmp;

                int modelCmp = CompareRefs(ma.Model, mb.Model);
                if (modelCmp != 0) return modelCmp;
            }

            // Then by depth (descending) for correct rendering order
            return b.Depth.CompareTo(a.Depth);
        }
    }

    /// <summary>
    /// Base class for rendering and managing world objects in a game scene.
    /// </summary>
    public abstract class WorldControl : GameControl
    {

        #region Performance Metrics for Objects
        public struct ObjectPerformanceMetrics
        {
            public int TotalObjects;
            public int ConsideredForRender;
            public int CulledByFrustum;
            public int DrawnSolid;
            public int DrawnTransparent;
            public int DrawnTotal => DrawnSolid + DrawnTransparent;
            public int StaticChunksTotal;
            public int StaticChunksVisible;
            public int StaticChunksCulled;
            public int StaticObjectsCulledByChunk;
        }

        public ObjectPerformanceMetrics ObjectMetrics { get; private set; }
        #endregion

        // --- Fields & Constants ---

        private sealed class StaticChunk
        {
            public BoundingBox Bounds;
            public Vector2 Center2D;
            public bool HasBounds;
            public bool IsVisible;
            public readonly List<WorldObject> Objects = new();
        }

        private const int StaticChunkSizeTiles = 16;

        private const float CullingOffset = 800f;

        private int _renderCounter;
        private DepthStencilState _currentDepthState = DepthStencilState.Default;
        private readonly WorldObjectDepthAsc _cmpAsc = new();
        private readonly WorldObjectDepthDesc _cmpDesc = new();
        private readonly WorldObjectBatchOptimizedAsc _cmpBatchAsc = new();
        private readonly WorldObjectBatchOptimizedDesc _cmpBatchDesc = new();
        private static readonly DepthStencilState DepthStateDefault = DepthStencilState.Default;
        private static readonly DepthStencilState DepthStateDepthRead = DepthStencilState.DepthRead;
        private StaticChunk[] _staticChunks;
        private int _staticChunkGridSize;
        private float _staticChunkWorldSize;
        private bool _staticChunksReady;
        private int _staticChunkCountWithObjects;
        private bool _staticChunkCacheDirty;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsChunkableStaticObject(WorldObject obj)
        {
            if (obj == null) return false;
            int type = obj.Type;
            if ((uint)type >= (uint)MapTileObjects.Length) return false;
            var registeredType = MapTileObjects[type];
            if (registeredType == null) return false;

            // Only chunk map tile style objects (non-walkers, non-players/monsters/items)
            if (obj is WalkerObject || obj is PlayerObject || obj is MonsterObject || obj is DroppedItemObject)
                return false;

            return registeredType.IsAssignableFrom(obj.GetType());
        }

        private readonly List<WorldObject> _solidBehind = new();
        private readonly List<WorldObject> _transparentObjects = new();
        private readonly List<WorldObject> _solidInFront = new();
        private readonly List<WalkerObject> _walkers = new();
        private readonly List<PlayerObject> _players = new();
        private readonly List<MonsterObject> _monsters = new();
        private readonly List<DroppedItemObject> _droppedItems = new();

        public Dictionary<ushort, WalkerObject> WalkerObjectsById { get; } = new();

        private ILogger _logger = ModelObject.AppLoggerFactory?.CreateLogger<WorldControl>();

        // --- Properties ---

        public string BackgroundMusicPath { get; set; }
        public string AmbientSoundPath { get; set; }

        public TerrainControl Terrain { get; }

        public short WorldIndex { get; private set; }
        public bool IsSunWorld { get; protected set; } = true;
        
        public bool EnableShadows { get; protected set; } = true;

        public ChildrenCollection<WorldObject> Objects { get; private set; }
            = new ChildrenCollection<WorldObject>(null);
        public IReadOnlyList<WalkerObject> Walkers => _walkers;
        public IReadOnlyList<PlayerObject> Players => _players;
        public IReadOnlyList<MonsterObject> Monsters => _monsters;
        public IReadOnlyList<DroppedItemObject> DroppedItems => _droppedItems;

        public Type[] MapTileObjects { get; } = new Type[Constants.TERRAIN_SIZE];

        public ushort MapId { get; protected set; }

        public new string Name { get; protected set; }

        // --- Constructor ---

        public WorldControl(short worldIndex)
        {
            AutoViewSize = false;
            ViewSize = new(MuGame.Instance.Width, MuGame.Instance.Height);
            WorldIndex = worldIndex;
            if (Constants.SUN_WORLD_INDICES != null && Constants.SUN_WORLD_INDICES.Length > 0)
            {
                IsSunWorld = Array.IndexOf(Constants.SUN_WORLD_INDICES, worldIndex) >= 0;
            }

            var worldInfo = (WorldInfoAttribute)Attribute.GetCustomAttribute(GetType(), typeof(WorldInfoAttribute));
            if (worldInfo != null)
            {
                MapId = worldInfo.MapId;
                Name = worldInfo.DisplayName;
            }

            Controls.Add(Terrain = new TerrainControl { WorldIndex = worldIndex });
            Objects.ControlAdded += OnObjectAdded;
            Objects.ControlRemoved += OnObjectRemoved;

        }

        // --- Lifecycle Methods ---

        public override async Task Load()
        {
            await base.Load();

            CreateMapTileObjects();
            Camera.Instance.AspectRatio = GraphicsDevice.Viewport.AspectRatio;

            var worldFolder = $"World{WorldIndex}";
            var dataPath = Constants.DataPath;
            var tasks = new List<Task>();

            // Load terrain OBJ
            var objPath = Path.Combine(dataPath, worldFolder, $"EncTerrain{WorldIndex}.obj");
            if (File.Exists(objPath))
            {
                var reader = new OBJReader();
                OBJ obj = await reader.Load(objPath);
                foreach (var mapObj in obj.Objects)
                {
                    var instance = WorldObjectFactory.CreateMapTileObject(this, mapObj);
                    if (instance != null) tasks.Add(instance.Load());
                }
            }

            await Task.WhenAll(tasks);
            BuildStaticChunkCache();

            // Load camera settings
            var capPath = Path.Combine(dataPath, worldFolder, "Camera_Angle_Position.bmd");
            if (File.Exists(capPath))
            {
                var capReader = new CAPReader();
                var data = await capReader.Load(capPath);
                Camera.Instance.FOV = data.CameraFOV;
#if ANDROID
                Camera.Instance.FOV *= Constants.ANDROID_FOV_SCALE;
#endif
                Camera.Instance.Position = data.CameraPosition;
                Camera.Instance.Target = data.HeroPosition;
            }

            // Play or stop background music
            if (!string.IsNullOrEmpty(BackgroundMusicPath))
                SoundController.Instance.PlayBackgroundMusic(BackgroundMusicPath);
            else
                SoundController.Instance.StopBackgroundMusic();

            // Play or stop ambient sound
            if (!string.IsNullOrEmpty(AmbientSoundPath))
                SoundController.Instance.PlayAmbientSound(AmbientSoundPath);
            else
                SoundController.Instance.StopAmbientSound();
        }

        public override void AfterLoad()
        {
            base.AfterLoad();
            SendToBack();
        }

        public override void Update(GameTime time)
        {
            base.Update(time);
            if (Status != GameControlStatus.Ready) return;

            // Iterate over a stable snapshot to avoid per-object lock contention
            var snapshot = Objects.GetSnapshot();
            for (int i = 0; i < snapshot.Count; i++)
            {
                var obj = snapshot[i];
                if (obj != null && obj.Status != GameControlStatus.Disposed)
                    obj.Update(time);
            }
        }

        public override void Draw(GameTime time)
        {
            if (Status != GameControlStatus.Ready) return;
            
            // Build shadow map before any backbuffer drawing so terrain tiles aren't lost
            if (EnableShadows && Constants.ENABLE_SHADOW_MAPPING && GraphicsManager.Instance.ShadowMapRenderer != null)
            {
                GraphicsManager.Instance.ShadowMapRenderer.RenderShadowMap(this);
            }

            base.Draw(time);
            RenderObjects(time);
        }

        // --- Object Management ---

        public bool IsWalkable(Vector2 position)
        {
            var terrainFlag = Terrain.RequestTerrainFlag((int)position.X, (int)position.Y);
            bool hasNoMove = terrainFlag.HasFlag(TWFlags.NoMove);

            // In Blood Castle, when the event is active (timer started), allow crossing the bridge
            // even if NoMove flag is set (the bridge area is normally blocked until event starts)
            if (hasNoMove && UI.Game.BloodCastleTimeControl.IsEventActive)
            {
                // Check if we're on a Blood Castle map (map IDs 11-17 and 52)
                var charState = MuGame.Network?.GetCharacterState();
                if (charState != null)
                {
                    var mapId = charState.MapId;
                    if ((mapId >= 11 && mapId <= 17) || mapId == 52)
                    {
                        return true; // Allow movement during active Blood Castle event
                    }
                }
            }

            return !hasNoMove;
        }

        private void OnObjectAdded(object sender, ChildrenEventArgs<WorldObject> e)
        {
            e.Control.World = this;
            TrackObjectType(e.Control);
            if (e.Control is WalkerObject walker &&
                walker.NetworkId != 0 &&
                walker.NetworkId != 0xFFFF)
            {
                if (WalkerObjectsById.TryGetValue(walker.NetworkId, out var existing))
                {
                    if (!ReferenceEquals(existing, walker))
                    {
                        _logger?.LogWarning("Replacing WalkerObject ID {Id:X4} - old: {OldType}, new: {NewType}",
                                           walker.NetworkId, existing.GetType().Name, walker.GetType().Name);
                        existing.Dispose(); // Dispose the old one
                    }
                }
                WalkerObjectsById[walker.NetworkId] = walker; // Always update/add
            }

            if (e.Control is MapTileObject)
                _staticChunkCacheDirty = true;
        }

        private void OnObjectRemoved(object sender, ChildrenEventArgs<WorldObject> e)
        {
            UntrackObjectType(e.Control);
            if (e.Control is WalkerObject walker &&
                walker.NetworkId != 0 &&
                walker.NetworkId != 0xFFFF)
            {
                if (WalkerObjectsById.TryGetValue(walker.NetworkId, out var storedWalker))
                {
                    // Only remove if it's the same object reference
                    if (ReferenceEquals(storedWalker, walker))
                    {
                        WalkerObjectsById.Remove(walker.NetworkId);
                        _logger?.LogTrace("Removed walker {Id:X4} from dictionary.", walker.NetworkId);
                    }
                    else
                    {
                        _logger?.LogDebug("Walker {Id:X4} removed from Objects but different object in dictionary.", walker.NetworkId);
                    }
                }
            }

            if (e.Control is MapTileObject)
                _staticChunkCacheDirty = true;
        }

        private void TrackObjectType(WorldObject obj)
        {
            if (obj is WalkerObject walker)
                _walkers.Add(walker);

            if (obj is PlayerObject player)
                _players.Add(player);

            if (obj is MonsterObject monster)
                _monsters.Add(monster);

            if (obj is DroppedItemObject drop)
                _droppedItems.Add(drop);
        }

        private void UntrackObjectType(WorldObject obj)
        {
            if (obj is WalkerObject walker)
                _walkers.Remove(walker);

            if (obj is PlayerObject player)
                _players.Remove(player);

            if (obj is MonsterObject monster)
                _monsters.Remove(monster);

            if (obj is DroppedItemObject drop)
                _droppedItems.Remove(drop);
        }

        public void DebugListWalkers()
        {
            _logger?.LogDebug("=== Walker Debug Info ===");
            _logger?.LogDebug("Objects collection count: {Count}", _walkers.Count);
            _logger?.LogDebug("WalkerObjectsById count: {Count}", WalkerObjectsById.Count);

            foreach (var walker in _walkers)
            {
                _logger?.LogDebug("Objects: {Type} {Id:X4}", walker.GetType().Name, walker.NetworkId);
            }

            foreach (var kvp in WalkerObjectsById)
            {
                _logger?.LogDebug("Dictionary: {Id:X4} -> {Type}", kvp.Key, kvp.Value.GetType().Name);
            }

            if (this is WalkableWorldControl walkable && walkable.Walker != null)
            {
                _logger?.LogDebug("Local player: {Type} {Id:X4}", walkable.Walker.GetType().Name, walkable.Walker.NetworkId);
            }
            _logger?.LogDebug("=== End Walker Debug ===");
        }

        /// <summary>
        /// Attempts to retrieve a walker by its network ID.
        /// Checks local player first, then dictionary, then full Objects search as fallback.
        /// </summary>
        public virtual bool TryGetWalkerById(ushort networkId, out WalkerObject walker)
        {
            // First check: local player in WalkableWorldControl
            if (this is WalkableWorldControl walkable &&
                walkable.Walker?.NetworkId == networkId)
            {
                walker = walkable.Walker;
                return true;
            }

            // Second check: WalkerObjectsById dictionary
            if (WalkerObjectsById.TryGetValue(networkId, out walker))
            {
                return true;
            }

            // Third check: fallback search in tracked walkers list
            for (int i = 0; i < _walkers.Count; i++)
            {
                var candidate = _walkers[i];
                if (candidate != null && candidate.NetworkId == networkId)
                {
                    walker = candidate;
                    if (!WalkerObjectsById.ContainsKey(networkId))
                    {
                        WalkerObjectsById[networkId] = walker;
                        _logger?.LogDebug("Sync fix: Added walker {Id:X4} to dictionary during lookup.", networkId);
                    }
                    return true;
                }
            }

            return false;
        }

        public bool ContainsWalkerId(ushort networkId) =>
            WalkerObjectsById.ContainsKey(networkId);

        public WalkerObject FindWalkerById(ushort networkId) =>
            TryGetWalkerById(networkId, out var walker) ? walker : null;

        public PlayerObject FindPlayerById(ushort networkId)
        {
            for (int i = 0; i < _players.Count; i++)
            {
                var player = _players[i];
                if (player != null && player.NetworkId == networkId)
                    return player;
            }
            return null;
        }

        public DroppedItemObject FindDroppedItemById(ushort networkId)
        {
            for (int i = 0; i < _droppedItems.Count; i++)
            {
                var drop = _droppedItems[i];
                if (drop != null && drop.NetworkId == networkId)
                    return drop;
            }
            return null;
        }

        public MonsterObject FindMonsterById(ushort networkId)
        {
            for (int i = 0; i < _monsters.Count; i++)
            {
                var monster = _monsters[i];
                if (monster != null && monster.NetworkId == networkId)
                    return monster;
            }
            return null;
        }

        /// <summary>
        /// Removes an object from the scene and dictionary if applicable.
        /// </summary>
        public bool RemoveObject(WorldObject obj)
        {
            bool removed = Objects.Remove(obj);
            if (removed && obj is WalkerObject walker &&
                walker.NetworkId != 0 &&
                walker.NetworkId != 0xFFFF)
            {
                WalkerObjectsById.Remove(walker.NetworkId);
            }
            return removed;
        }

        // --- Rendering Helpers ---

        private void RenderObjects(GameTime time)
        {
            _renderCounter = 0;
            _solidBehind.Clear();
            _transparentObjects.Clear();
            _solidInFront.Clear();

            var objects = Objects.GetSnapshot();
            var metrics = new ObjectPerformanceMetrics
            {
                TotalObjects = objects.Count,
                StaticChunksTotal = _staticChunkCountWithObjects
            };

            var cam = Camera.Instance;
            var frustum = cam?.Frustum;
            if (cam == null || frustum == null) return;

            Vector2 cam2D = new(cam.Position.X, cam.Position.Y);
            float maxDist = cam.ViewFar + CullingOffset;
            float maxDistSq = maxDist * maxDist;

            if (_staticChunkCacheDirty || !_staticChunksReady)
            {
                BuildStaticChunkCache();
                _staticChunkCacheDirty = false;
            }

            bool chunkCullingActive = _staticChunksReady && _staticChunkCountWithObjects > 0;
            if (chunkCullingActive)
            {
                UpdateStaticChunkVisibility(cam2D, maxDistSq, frustum);
            }

            // Classify objects using the cached snapshot to avoid per-object locks
            for (int i = 0; i < objects.Count; i++)
            {
                var obj = objects[i];
                if (chunkCullingActive && IsChunkableStaticObject(obj))
                    continue; // Static objects handled via chunk culling

                ClassifyObject(obj, cam2D, maxDistSq, frustum, ref metrics, skipViewCheck: false);
            }

            // Chunk-based culling/classification for static map objects
            if (chunkCullingActive)
            {
                ClassifyStaticChunks(cam2D, maxDistSq, frustum, ref metrics);
            }

            ObjectMetrics = metrics;

            // Draw solid behind objects
            if (_solidBehind.Count > 1)
            {
                _solidBehind.Sort(Constants.ENABLE_BATCH_OPTIMIZED_SORTING ? _cmpBatchAsc : _cmpAsc);
            }
            DrawListWithSpriteBatchGrouping(_solidBehind, DepthStateDefault, time);

            // Draw transparent objects
            if (_transparentObjects.Count > 1)
            {
                // Transparent rendering requires strict back-to-front ordering, so never batch-optimize.
                _transparentObjects.Sort(_cmpDesc);
            }
            DrawListWithSpriteBatchGrouping(_transparentObjects, DepthStateDepthRead, time);

            // Draw solid in front objects
            if (_solidInFront.Count > 1)
            {
                _solidInFront.Sort(Constants.ENABLE_BATCH_OPTIMIZED_SORTING ? _cmpBatchAsc : _cmpAsc);
            }
            DrawListWithSpriteBatchGrouping(_solidInFront, DepthStateDefault, time);

            // Draw post-pass (DrawAfter)
            DrawAfterPass(_solidBehind, DepthStateDefault, time);
            DrawAfterPass(_transparentObjects, DepthStateDepthRead, time);
            DrawAfterPass(_solidInFront, DepthStateDefault, time);
        }

        private void DrawListWithSpriteBatchGrouping(List<WorldObject> list, DepthStencilState depthState, GameTime time)
        {
            if (list.Count == 0)
                return;

            SetDepthState(depthState);

            var spriteBatch = GraphicsManager.Instance.Sprite;
            Helpers.SpriteBatchScope? scope = null;
            BlendState currentBlend = null;
            SamplerState currentSampler = null;
            DepthStencilState currentBatchDepth = null;

            for (int i = 0; i < list.Count; i++)
            {
                var obj = list[i];
                if (obj == null)
                    continue;

                obj.DepthState = depthState;

                bool usesSpriteBatch =
                    obj is SpriteObject ||
                    obj is WaterMistParticleSystem ||
                    obj is ElfBuffOrbTrail;

                if (usesSpriteBatch)
                {
                    var blend = obj.BlendState ?? BlendState.AlphaBlend;
                    SamplerState sampler;
                    if (obj is WaterMistParticleSystem || obj is ElfBuffOrbTrail)
                    {
                        sampler = SamplerState.LinearClamp;
                    }
                    else
                    {
                        sampler = ReferenceEquals(blend, BlendState.Additive)
                            ? GraphicsManager.GetQualityLinearSamplerState()
                            : GraphicsManager.GetQualitySamplerState();
                    }
                    var batchDepth = obj is WaterMistParticleSystem ? DepthStencilState.DepthRead : depthState;

                    if (scope == null ||
                        !ReferenceEquals(blend, currentBlend) ||
                        !ReferenceEquals(sampler, currentSampler) ||
                        !ReferenceEquals(batchDepth, currentBatchDepth))
                    {
                        scope?.Dispose();
                        scope = new Helpers.SpriteBatchScope(spriteBatch, SpriteSortMode.Deferred, blend, sampler, batchDepth);
                        currentBlend = blend;
                        currentSampler = sampler;
                        currentBatchDepth = batchDepth;
                    }

                    obj.Draw(time);
                }
                else
                {
                    scope?.Dispose();
                    scope = null;
                    currentBlend = null;
                    currentSampler = null;
                    currentBatchDepth = null;

                    obj.Draw(time);
                }

                obj.RenderOrder = ++_renderCounter;
            }

            scope?.Dispose();
        }

        private void DrawAfterPass(List<WorldObject> list, DepthStencilState state, GameTime time)
        {
            if (list.Count == 0) return;
            SetDepthState(state);
            foreach (var obj in list)
                obj.DrawAfter(time);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetDepthState(DepthStencilState state)
        {
            if (_currentDepthState != state)
            {
                GraphicsDevice.DepthStencilState = state;
                _currentDepthState = state;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ClassifyObject(WorldObject obj, Vector2 cam2D, float maxDistSq, BoundingFrustum frustum,
            ref ObjectPerformanceMetrics metrics, bool skipViewCheck)
        {
            if (obj == null) return;
            if (obj.Status == GameControlStatus.Disposed || !obj.Visible) return;

            metrics.ConsideredForRender++;

            if (!skipViewCheck && !IsObjectInView(obj, cam2D, maxDistSq, frustum))
            {
                metrics.CulledByFrustum++;
                return;
            }

            if (obj.IsTransparent)
            {
                _transparentObjects.Add(obj);
                metrics.DrawnTransparent++;
            }
            else if (obj.AffectedByTransparency)
            {
                _solidBehind.Add(obj);
                metrics.DrawnSolid++;
            }
            else
            {
                _solidInFront.Add(obj);
                metrics.DrawnSolid++;
            }
        }

        private void ClassifyStaticChunks(Vector2 cam2D, float maxDistSq, BoundingFrustum frustum,
            ref ObjectPerformanceMetrics metrics)
        {
            if (_staticChunks == null) return;

            for (int i = 0; i < _staticChunks.Length; i++)
            {
                var chunk = _staticChunks[i];
                if (chunk.Objects.Count == 0) continue;

                if (!chunk.IsVisible)
                {
                    metrics.StaticChunksCulled++;
                    // Count all valid objects in this chunk as culled
                    for (int n = 0; n < chunk.Objects.Count; n++)
                    {
                        var obj = chunk.Objects[n];
                        if (obj == null) continue;
                        if (obj.Status == GameControlStatus.Disposed) continue;
                        metrics.ConsideredForRender++;
                        metrics.CulledByFrustum++;
                        metrics.StaticObjectsCulledByChunk++;
                    }
                    continue;
                }

                metrics.StaticChunksVisible++;
                for (int n = 0; n < chunk.Objects.Count; n++)
                    ClassifyObject(chunk.Objects[n], cam2D, maxDistSq, frustum, ref metrics, skipViewCheck: true);
            }
        }

        // --- View Frustum & Culling ---

        public bool IsObjectInView(WorldObject obj)
        {
            var pos3 = obj.WorldPosition.Translation;
            var cam = Camera.Instance;
            if (cam == null) return false;

            var cam2 = new Vector2(cam.Position.X, cam.Position.Y);
            var obj2 = new Vector2(pos3.X, pos3.Y);
            var maxDist = cam.ViewFar + CullingOffset;
            if (Vector2.DistanceSquared(cam2, obj2) > maxDist * maxDist)
                return false;

            return cam.Frustum.Contains(obj.BoundingBoxWorld) != ContainmentType.Disjoint;
        }

        // Fast path for loops where camera info is already cached
        private static bool IsObjectInView(WorldObject obj, Vector2 cam2, float maxDistSq, BoundingFrustum frustum)
        {
            var pos3 = obj.WorldPosition.Translation;
            var obj2 = new Vector2(pos3.X, pos3.Y);
            if (Vector2.DistanceSquared(cam2, obj2) > maxDistSq)
                return false;

            return frustum != null && frustum.Contains(obj.BoundingBoxWorld) != ContainmentType.Disjoint;
        }

        private void UpdateStaticChunkVisibility(Vector2 cam2, float maxDistSq, BoundingFrustum frustum)
        {
            if (_staticChunks == null || frustum == null) return;

            const float DistancePadding = 400f;
            float paddedMaxDistSq = maxDistSq + DistancePadding * DistancePadding;

            for (int i = 0; i < _staticChunks.Length; i++)
            {
                var chunk = _staticChunks[i];
                if (!chunk.HasBounds || chunk.Objects.Count == 0)
                {
                    chunk.IsVisible = false;
                    continue;
                }

                if (Vector2.DistanceSquared(cam2, chunk.Center2D) > paddedMaxDistSq)
                {
                    chunk.IsVisible = false;
                    continue;
                }

                chunk.IsVisible = frustum.Contains(chunk.Bounds) != ContainmentType.Disjoint;
            }
        }

        // --- NEW METHOD FOR LIGHT CULLING ---
        /// <summary>
        /// Efficiently checks if a dynamic light's area of effect intersects with the camera's view frustum.
        /// </summary>
        /// <param name="light">The dynamic light to check.</param>
        /// <returns>True if the light's sphere is at least partially in view, otherwise false.</returns>
        public bool IsLightInView(DynamicLight light)
        {
            var frustum = Camera.Instance?.Frustum;
            if (frustum == null) return true;

            // Create a bounding sphere representing the light's full radius of effect.
            var lightSphere = new BoundingSphere(light.Position, light.Radius);

            // Use the highly optimized Intersects check. This is much faster than manual distance calculations.
            return frustum.Intersects(lightSphere);
        }

        private void BuildStaticChunkCache()
        {
            _staticChunksReady = false;
            _staticChunkCountWithObjects = 0;
            _staticChunkCacheDirty = false;

            _staticChunkGridSize = Constants.TERRAIN_SIZE / StaticChunkSizeTiles;
            if (_staticChunkGridSize <= 0)
                return;

            _staticChunkWorldSize = StaticChunkSizeTiles * Constants.TERRAIN_SCALE;
            int chunkCount = _staticChunkGridSize * _staticChunkGridSize;
            _staticChunks = new StaticChunk[chunkCount];
            for (int i = 0; i < chunkCount; i++)
                _staticChunks[i] = new StaticChunk();

            var snapshot = Objects.GetSnapshot();
            for (int i = 0; i < snapshot.Count; i++)
            {
                var obj = snapshot[i];
                if (!IsChunkableStaticObject(obj))
                    continue;

                // Use Position (object-space) to bucket; safer if WorldPosition not yet recalculated
                var pos = obj.Position;
                int cx = (int)(pos.X / _staticChunkWorldSize);
                int cy = (int)(pos.Y / _staticChunkWorldSize);
                cx = Math.Max(0, Math.Min(cx, _staticChunkGridSize - 1));
                cy = Math.Max(0, Math.Min(cy, _staticChunkGridSize - 1));

                int idx = cy * _staticChunkGridSize + cx;
                var chunk = _staticChunks[idx];
                chunk.Objects.Add(obj);

                var bbox = obj.BoundingBoxWorld;
                if (chunk.HasBounds)
                    chunk.Bounds = BoundingBox.CreateMerged(chunk.Bounds, bbox);
                else
                {
                    chunk.Bounds = bbox;
                    chunk.HasBounds = true;
                }
            }

            bool anyObjects = false;
            for (int i = 0; i < _staticChunks.Length; i++)
            {
                var chunk = _staticChunks[i];
                if (!chunk.HasBounds) continue;

                chunk.Center2D = new Vector2(
                    (chunk.Bounds.Min.X + chunk.Bounds.Max.X) * 0.5f,
                    (chunk.Bounds.Min.Y + chunk.Bounds.Max.Y) * 0.5f);
                chunk.IsVisible = false;
                if (chunk.Objects.Count > 0)
                {
                    anyObjects = true;
                    _staticChunkCountWithObjects++;
                }
            }

            _staticChunksReady = anyObjects;
        }

        // --- Map Tile Initialization ---

        protected virtual void CreateMapTileObjects()
        {
            var defaultType = typeof(MapTileObject);
            for (int i = 0; i < MapTileObjects.Length; i++)
                MapTileObjects[i] = defaultType;
        }

        // --- Disposal ---

        public override void Dispose()
        {
            var sw = Stopwatch.StartNew();

            // Dispose and remove all objects except the local player
            foreach (var obj in Objects.ToArray())
            {
                if (this is WalkableWorldControl wc &&
                    obj is PlayerObject player &&
                    wc.Walker == player)
                    continue;

                RemoveObject(obj);
                obj.Dispose();
            }

            Objects.Clear();
            WalkerObjectsById.Clear();
            _walkers.Clear();
            _players.Clear();
            _monsters.Clear();
            _droppedItems.Clear();

            sw.Stop();
            var elapsedObjects = sw.ElapsedMilliseconds;
            sw.Restart();

            base.Dispose();

            sw.Stop();
            var elapsedBase = sw.ElapsedMilliseconds;
            _logger?.LogDebug($"Dispose WorldControl {WorldIndex} - Objects: {elapsedObjects}ms, Base: {elapsedBase}ms");
        }
    }
}
