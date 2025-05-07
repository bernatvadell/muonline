using Client.Data.ATT;
using Client.Data.CAP;
using Client.Data.OBJS;
using Client.Main.Controllers;
using Client.Main.Models;
using Client.Main.Objects;
using Client.Main.Objects.Player;
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

    /// <summary>
    /// Base class for rendering and managing world objects in a game scene.
    /// </summary>
    public abstract class WorldControl : GameControl
    {
        // --- Fields & Constants ---

        private const float CullingOffset = 800f;

        private int _renderCounter;
        private DepthStencilState _currentDepthState = DepthStencilState.Default;
        private readonly WorldObjectDepthAsc _cmpAsc = new();
        private readonly WorldObjectDepthDesc _cmpDesc = new();
        private static readonly DepthStencilState DepthStateDefault = DepthStencilState.Default;
        private static readonly DepthStencilState DepthStateDepthRead = DepthStencilState.DepthRead;
        private BoundingFrustum _boundingFrustum;

        private readonly List<WorldObject> _solidBehind = new();
        private readonly List<WorldObject> _transparentObjects = new();
        private readonly List<WorldObject> _solidInFront = new();

        protected Dictionary<ushort, WalkerObject> WalkerObjectsById { get; } = new();

        // --- Properties ---

        public string BackgroundMusicPath { get; set; }

        public TerrainControl Terrain { get; }

        public short WorldIndex { get; private set; }

        public ChildrenCollection<WorldObject> Objects { get; private set; }
            = new ChildrenCollection<WorldObject>(null);

        public Type[] MapTileObjects { get; } = new Type[Constants.TERRAIN_SIZE];

        // --- Constructor ---

        public WorldControl(short worldIndex)
        {
            AutoViewSize = false;
            ViewSize = new(MuGame.Instance.Width, MuGame.Instance.Height);
            WorldIndex = worldIndex;

            Controls.Add(Terrain = new TerrainControl { WorldIndex = worldIndex });
            Objects.ControlAdded += OnObjectAdded;

            Camera.Instance.CameraMoved += OnCameraMoved;
            UpdateBoundingFrustum();
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

            // Load camera settings
            var capPath = Path.Combine(dataPath, worldFolder, "Camera_Angle_Position.bmd");
            if (File.Exists(capPath))
            {
                var capReader = new CAPReader();
                var data = await capReader.Load(capPath);
                Camera.Instance.FOV = data.CameraFOV;
                Camera.Instance.Position = data.CameraPosition;
                Camera.Instance.Target = data.HeroPosition;
            }

            // Play or stop background music
            if (!string.IsNullOrEmpty(BackgroundMusicPath))
                SoundController.Instance.PlayBackgroundMusic(BackgroundMusicPath);
            else
                SoundController.Instance.StopBackgroundMusic();
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

            // Iterate over a copy to avoid modification during update
            foreach (var obj in Objects.ToArray())
            {
                if (obj.Status != GameControlStatus.Disposed)
                    obj.Update(time);
            }
        }

        public override void Draw(GameTime time)
        {
            if (Status != GameControlStatus.Ready) return;
            base.Draw(time);
            RenderObjects(time);
        }

        // --- Object Management ---

        public bool IsWalkable(Vector2 position)
        {
            var terrainFlag = Terrain.RequestTerrainFlag((int)position.X, (int)position.Y);
            return !terrainFlag.HasFlag(TWFlags.NoMove);
        }

        private void OnObjectAdded(object sender, ChildrenEventArgs<WorldObject> e)
        {
            e.Control.World = this;
            if (e.Control is WalkerObject walker &&
                walker.NetworkId != 0 &&
                walker.NetworkId != 0xFFFF)
            {
                if (!WalkerObjectsById.TryAdd(walker.NetworkId, walker))
                    Debug.WriteLine($"Warning: Duplicate WalkerObject ID {walker.NetworkId:X4}");
            }
        }

        /// <summary>
        /// Attempts to retrieve a walker by its network ID.
        /// </summary>
        public virtual bool TryGetWalkerById(ushort networkId, out WalkerObject walker)
        {
            if (this is WalkableWorldControl walkable &&
                walkable.Walker?.NetworkId == networkId)
            {
                walker = walkable.Walker;
                return true;
            }
            return WalkerObjectsById.TryGetValue(networkId, out walker);
        }

        public bool ContainsWalkerId(ushort networkId) =>
            WalkerObjectsById.ContainsKey(networkId);

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

            // Classify objects
            foreach (var obj in Objects.ToArray())
            {
                if (obj.Status == GameControlStatus.Disposed || !obj.Visible) continue;
                if (!IsObjectInView(obj)) continue;

                if (obj.IsTransparent)
                    _transparentObjects.Add(obj);
                else if (obj.AffectedByTransparency)
                    _solidBehind.Add(obj);
                else
                    _solidInFront.Add(obj);
            }

            // Draw solid behind objects
            if (_solidBehind.Count > 1) _solidBehind.Sort(_cmpAsc);
            SetDepthState(DepthStateDefault);
            foreach (var obj in _solidBehind)
                DrawObject(obj, time, DepthStateDefault);

            // Draw transparent objects
            if (_transparentObjects.Count > 1) _transparentObjects.Sort(_cmpDesc);
            if (_transparentObjects.Count > 0)
                SetDepthState(DepthStateDepthRead);
            foreach (var obj in _transparentObjects)
                DrawObject(obj, time, DepthStateDepthRead);

            // Draw solid in front objects
            if (_solidInFront.Count > 1) _solidInFront.Sort(_cmpAsc);
            if (_solidInFront.Count > 0)
                SetDepthState(DepthStateDefault);
            foreach (var obj in _solidInFront)
                DrawObject(obj, time, DepthStateDefault);

            // Draw post-pass (DrawAfter)
            DrawAfterPass(_solidBehind, DepthStateDefault, time);
            DrawAfterPass(_transparentObjects, DepthStateDepthRead, time);
            DrawAfterPass(_solidInFront, DepthStateDefault, time);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawObject(WorldObject obj, GameTime time, DepthStencilState state)
        {
            obj.DepthState = state;
            obj.Draw(time);
            obj.RenderOrder = ++_renderCounter;
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

        // --- View Frustum & Culling ---

        private bool IsObjectInView(WorldObject obj)
        {
            var pos3 = obj.WorldPosition.Translation;
            var cam = Camera.Instance;
            if (cam == null) return false;

            var cam2 = new Vector2(cam.Position.X, cam.Position.Y);
            var obj2 = new Vector2(pos3.X, pos3.Y);
            var maxDist = cam.ViewFar + CullingOffset;
            if (Vector2.DistanceSquared(cam2, obj2) > maxDist * maxDist)
                return false;

            if (_boundingFrustum == null) return false;
            return _boundingFrustum.Contains(obj.BoundingBoxWorld) != ContainmentType.Disjoint;
        }

        private void OnCameraMoved(object sender, EventArgs e) =>
            UpdateBoundingFrustum();

        private void UpdateBoundingFrustum()
        {
            var cam = Camera.Instance;
            if (cam == null) return;

            var viewProj = cam.View * cam.Projection;
            _boundingFrustum = new BoundingFrustum(viewProj);
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

            sw.Stop();
            var elapsedObjects = sw.ElapsedMilliseconds;
            sw.Restart();

            base.Dispose();

            sw.Stop();
            var elapsedBase = sw.ElapsedMilliseconds;
            Debug.WriteLine(
                $"Dispose WorldControl {WorldIndex} - Objects: {elapsedObjects}ms, Base: {elapsedBase}ms");
        }
    }
}