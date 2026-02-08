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
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Client.Main.Controls
{
    // Shared helper for reference comparison in comparers.
    // Uses RuntimeHelpers.GetHashCode for grouping objects by identity (same texture/model/blend state).
    // The ordering is NOT deterministic across GC compactions, but that is acceptable here because
    // the goal is batching identical resources together, not producing a stable sort order.
    internal static class ComparerHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CompareRefs<T>(T a, T b) where T : class
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a is null) return -1;
            if (b is null) return 1;
            return RuntimeHelpers.GetHashCode(a).CompareTo(RuntimeHelpers.GetHashCode(b));
        }
    }

    // Comparers for sorting world objects by depth
    sealed class WorldObjectDepthAsc : IComparer<WorldObject>
    {
        public static readonly WorldObjectDepthAsc Instance = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(WorldObject a, WorldObject b)
        {
            int cmp = a.Depth.CompareTo(b.Depth);
            if (cmp != 0) return cmp;

            // Tie-break for deterministic ordering (prevents flicker when many objects share the same depth).
            return a.NetworkId.CompareTo(b.NetworkId);
        }
    }

    sealed class WorldObjectDepthDesc : IComparer<WorldObject>
    {
        public static readonly WorldObjectDepthDesc Instance = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(WorldObject a, WorldObject b)
        {
            int cmp = b.Depth.CompareTo(a.Depth);
            if (cmp != 0) return cmp;

            // Tie-break for deterministic ordering (prevents flicker when many objects share the same depth).
            return b.NetworkId.CompareTo(a.NetworkId);
        }
    }

    // Optimized comparer that sorts by Model+Texture first, then depth
    // This minimizes state changes and improves GPU cache coherency
    sealed class WorldObjectBatchOptimizedAsc : IComparer<WorldObject>
    {
        public static readonly WorldObjectBatchOptimizedAsc Instance = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(WorldObject a, WorldObject b)
        {
            if (a is ModelObject ma && b is ModelObject mb)
            {
                // Prioritize by texture and blend state to minimize state changes
                int texCmp = ComparerHelper.CompareRefs(ma.GetSortTextureHint(), mb.GetSortTextureHint());
                if (texCmp != 0) return texCmp;

                int blendCmp = ComparerHelper.CompareRefs(ma.BlendState, mb.BlendState);
                if (blendCmp != 0) return blendCmp;

                int modelCmp = ComparerHelper.CompareRefs(ma.Model, mb.Model);
                if (modelCmp != 0) return modelCmp;
            }

            // Then by depth for correct rendering order
            int depthCmp = a.Depth.CompareTo(b.Depth);
            if (depthCmp != 0) return depthCmp;

            return a.NetworkId.CompareTo(b.NetworkId);
        }
    }

    sealed class WorldObjectBatchOptimizedDesc : IComparer<WorldObject>
    {
        public static readonly WorldObjectBatchOptimizedDesc Instance = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(WorldObject a, WorldObject b)
        {
            if (a is ModelObject ma && b is ModelObject mb)
            {
                // Prioritize by texture and blend state to minimize state changes
                int texCmp = ComparerHelper.CompareRefs(ma.GetSortTextureHint(), mb.GetSortTextureHint());
                if (texCmp != 0) return texCmp;

                int blendCmp = ComparerHelper.CompareRefs(ma.BlendState, mb.BlendState);
                if (blendCmp != 0) return blendCmp;

                int modelCmp = ComparerHelper.CompareRefs(ma.Model, mb.Model);
                if (modelCmp != 0) return modelCmp;
            }

            // Then by depth (descending) for correct rendering order
            int depthCmp = b.Depth.CompareTo(a.Depth);
            if (depthCmp != 0) return depthCmp;

            return b.NetworkId.CompareTo(a.NetworkId);
        }
    }

    /// <summary>
    /// Base class for rendering and managing world objects in a game scene.
    /// </summary>
    public abstract class WorldControl : GameControl
    {
        // --- Fields & Constants ---
        private int _renderCounter;
        private DepthStencilState _currentDepthState = DepthStencilState.Default;
        private static readonly DepthStencilState DepthStateDefault = DepthStencilState.Default;
        private static readonly DepthStencilState DepthStateDepthRead = DepthStencilState.DepthRead;


        private readonly List<WorldObject> _solidBehind = [];
        private readonly List<WorldObject> _transparentObjects = [];
        private readonly List<WorldObject> _solidInFront = [];
        private readonly List<WalkerObject> _walkers = [];
        private readonly List<PlayerObject> _players = [];
        private readonly List<MonsterObject> _monsters = [];
        private readonly List<DroppedItemObject> _droppedItems = [];
        private readonly Queue<WorldObject> _objectsToInitialize = [];
        private readonly List<WorldObject> _visibleObjects = [];
        private bool _dirtyVisibleObjects = true;

        public Dictionary<ushort, WalkerObject> WalkerObjectsById { get; } = [];

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

            Camera.Instance.CameraMoved += Camera_Moved;
        }

        private void Camera_Moved(object sender, EventArgs e)
        {
            _dirtyVisibleObjects = true;
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

            // Load camera settings
            var capPath = Path.Combine(dataPath, worldFolder, "Camera_Angle_Position.bmd");
            if (File.Exists(capPath))
            {
                var capReader = new CAPReader();
                var data = await capReader.Load(capPath);
                Camera.Instance.FOV = data.CameraFOV * Constants.FOV_SCALE;
                Camera.Instance.Position = data.CameraPosition;
                Camera.Instance.Target = data.HeroPosition;
            }

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

            // tasks.Add(Container.Load());
            await Task.WhenAll(tasks);

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
        }

        public override void Update(GameTime time)
        {
            base.Update(time);

            if (Status != GameControlStatus.Ready) return;

            if (_objectsToInitialize.Count > 0)
            {
                int initCount = Math.Min(100, _objectsToInitialize.Count);
                for (int i = 0; i < initCount; i++)
                {
                    var obj = _objectsToInitialize.Dequeue();
                    obj.Load().ConfigureAwait(false);
                }
            }

            if (_dirtyVisibleObjects)
            {
                _visibleObjects.Clear();

                for (var i = 0; i < Objects.Count; i++)
                {
                    var obj = Objects[i];

                    if (!obj.Visible)
                        continue;

                    if (obj is EffectObject || Camera.Instance.Frustum.Intersects(obj.BoundingBoxWorld))
                        _visibleObjects.Add(obj);
                }

                _dirtyVisibleObjects = false;
            }

            var objects = _visibleObjects;
            for (int i = objects.Count - 1; i >= 0; i--)
                objects[i].Update(time);
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
            e.Control.HiddenChanged += Object_HiddenChanged;

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

            _visibleObjects.Add(e.Control);
        }

        private void Object_HiddenChanged(object sender, EventArgs e)
        {
            var obj = sender as WorldObject;
            if (obj.Hidden) _visibleObjects.Remove(obj);
            else _visibleObjects.Add(obj);
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

            e.Control.HiddenChanged -= Object_HiddenChanged;

            _visibleObjects.Remove(e.Control);
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

            var objects = _visibleObjects;

            for (var i = 0; i < objects.Count; i++)
            {
                var obj = objects[i];

                if (!obj.Visible)
                    continue;

                if (obj is WalkerObject)
                {
                    _solidInFront.Add(obj);
                }
                else if (obj.IsTransparent)
                {
                    _transparentObjects.Add(obj);
                }
                else if (obj.AffectedByTransparency)
                {
                    _solidBehind.Add(obj);
                }
                else
                {
                    _solidInFront.Add(obj);
                }
            }

            // Sort lists
            if (_solidBehind.Count > 1)
                _solidBehind.Sort(Constants.ENABLE_BATCH_OPTIMIZED_SORTING ? WorldObjectBatchOptimizedAsc.Instance : WorldObjectDepthAsc.Instance);

            if (_transparentObjects.Count > 1)
                _transparentObjects.Sort(WorldObjectDepthDesc.Instance);

            if (_solidInFront.Count > 1)
                _solidInFront.Sort(Constants.ENABLE_BATCH_OPTIMIZED_SORTING ? WorldObjectBatchOptimizedAsc.Instance : WorldObjectDepthAsc.Instance);


            // Draws
            DrawListWithSpriteBatchGrouping(_solidBehind, DepthStateDefault, time);
            DrawListWithSpriteBatchGrouping(_transparentObjects, DepthStateDepthRead, time);
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
            var objCount = list.Count;
            if (objCount == 0) return;
            SetDepthState(state);
            for (var i = 0; i < objCount; i++)
                list[i].DrawAfter(time);
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

        // Fast path for loops where camera info is already cached
        private static bool IsObjectInView(WorldObject obj, Vector2 cam2, float maxDistSq, BoundingFrustum frustum)
        {
            var pos3 = obj.WorldPosition.Translation;
            var obj2 = new Vector2(pos3.X, pos3.Y);
            if (Vector2.DistanceSquared(cam2, obj2) > maxDistSq)
                return false;

            return frustum != null && frustum.Contains(obj.BoundingBoxWorld) != ContainmentType.Disjoint;
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

        public void OnWorldObjectStatusChanged(WorldObject worldObject)
        {
            if (worldObject.Status == GameControlStatus.NonInitialized)
            {
                _objectsToInitialize.Enqueue(worldObject);
            }
        }
    }
}
