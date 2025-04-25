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
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Client.Main.Controls
{

    sealed class WorldObjectDepthAsc : IComparer<WorldObject>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(WorldObject a, WorldObject b)
            => a.Depth.CompareTo(b.Depth);
    }

    sealed class WorldObjectDepthDesc : IComparer<WorldObject>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(WorldObject a, WorldObject b)
            => b.Depth.CompareTo(a.Depth);
    }
    
    public abstract class WorldControl : GameControl
    {
        public string BackgroundMusicPath { get; set; }
        public TerrainControl Terrain { get; }
        public short WorldIndex { get; private set; }
        public ChildrenCollection<WorldObject> Objects { get; private set; } = new ChildrenCollection<WorldObject>(null);
        public Type[] MapTileObjects { get; } = new Type[Constants.TERRAIN_SIZE];
        private int renderCounter = 0;

        private readonly List<WorldObject> solidBehind = new List<WorldObject>();
        private readonly List<WorldObject> transparentObjects = new List<WorldObject>();
        private readonly List<WorldObject> solidInFront = new List<WorldObject>();

        private DepthStencilState _currentDepthState = DepthStencilState.Default;
        private readonly WorldObjectDepthAsc _cmpAsc = new();
        private readonly WorldObjectDepthDesc _cmpDesc = new();

        private static readonly DepthStencilState DepthStateDefault = DepthStencilState.Default;
        private static readonly DepthStencilState DepthStateDepthRead = DepthStencilState.DepthRead;

        private BoundingFrustum boundingFrustum;

        private readonly float cullingOffset = 500f;

        public WorldControl(short worldIndex)
        {
            AutoViewSize = false;
            ViewSize = new(MuGame.Instance.Width, MuGame.Instance.Height);
            WorldIndex = worldIndex;
            Controls.Add(Terrain = new TerrainControl() { WorldIndex = worldIndex });
            Objects.ControlAdded += Object_Added;

            Camera.Instance.CameraMoved += OnCameraMoved;

            UpdateBoundingFrustum();
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

        private void OnCameraMoved(object sender, EventArgs e)
        {
            UpdateBoundingFrustum();
        }

        private void Object_Added(object sender, ChildrenEventArgs<WorldObject> e)
        {
            e.Control.World = this;
        }

        public override async Task Load()
        {
            await base.Load();

            CreateMapTileObjects();

            var worldFolder = $"World{WorldIndex}";

            Camera.Instance.AspectRatio = GraphicsDevice.Viewport.AspectRatio;

            var tasks = new List<Task>();

            var objReader = new OBJReader();
            var objectPath = Path.Combine(Constants.DataPath, worldFolder, $"EncTerrain{WorldIndex}.obj");

            if (File.Exists(objectPath))
            {
                OBJ obj = await objReader.Load(objectPath);

                foreach (var mapObj in obj.Objects)
                {
                    var instance = WorldObjectFactory.CreateMapTileObject(this, mapObj);
                    if (instance != null) tasks.Add(instance.Load());
                }
            }

            await Task.WhenAll(tasks);

            var cameraAnglePositionPath = Path.Combine(Constants.DataPath, worldFolder, "Camera_Angle_Position.bmd");
            if (File.Exists(cameraAnglePositionPath))
            {
                var capReader = new CAPReader();
                var data = await capReader.Load(cameraAnglePositionPath);

                Camera.Instance.FOV = data.CameraFOV;
                Camera.Instance.Position = data.CameraPosition;
                Camera.Instance.Target = data.HeroPosition;
            }

            if (!string.IsNullOrEmpty(BackgroundMusicPath))
            {
                SoundController.Instance.PlayBackgroundMusic(BackgroundMusicPath);
            }
            else
            {
                SoundController.Instance.StopBackgroundMusic();
            }
        }

        public override void AfterLoad()
        {
            base.AfterLoad();

            SendToBack();
        }

        public override void Update(GameTime time)
        {
            base.Update(time);
            if (Status != GameControlStatus.Ready)
                return;

            foreach (var obj in Objects)
                obj.Update(time);
        }

        public override void Draw(GameTime time)
        {
            if (Status != GameControlStatus.Ready)
                return;

            base.Draw(time);
            RenderObjects(time);
        }

        public bool IsWalkable(Vector2 position)
        {
            var terrainFlag = Terrain.RequestTerraingFlag((int)position.X, (int)position.Y);
            return !terrainFlag.HasFlag(TWFlags.NoMove);
        }

        protected virtual void CreateMapTileObjects()
        {
            var typeMapObject = typeof(MapTileObject);

            for (var i = 0; i < MapTileObjects.Length; i++)
                MapTileObjects[i] = typeMapObject;
        }

        private void RenderObjects(GameTime gameTime)
        {
            renderCounter = 0;

            solidBehind.Clear();
            transparentObjects.Clear();
            solidInFront.Clear();

            foreach (var obj in Objects)
            {
                if (!IsObjectInView(obj)) continue;

                if (obj.IsTransparent)
                    transparentObjects.Add(obj);
                else if (obj.AffectedByTransparency)
                    solidBehind.Add(obj);
                else
                    solidInFront.Add(obj);
            }

            if (solidBehind.Count > 1) solidBehind.Sort(_cmpAsc);
            SetDepthState(DepthStateDefault);
            foreach (var obj in solidBehind)
            {
                obj.DepthState = DepthStateDefault;
                obj.Draw(gameTime);
                obj.RenderOrder = ++renderCounter;
            }

            if (transparentObjects.Count > 1) transparentObjects.Sort(_cmpDesc);
            if (transparentObjects.Count > 0)
                SetDepthState(DepthStateDepthRead);
            foreach (var obj in transparentObjects)
            {
                obj.DepthState = DepthStateDepthRead;
                obj.Draw(gameTime);
                obj.RenderOrder = ++renderCounter;
            }

            if (solidInFront.Count > 1) solidInFront.Sort(_cmpAsc);
            if (solidInFront.Count > 0)
                SetDepthState(DepthStateDefault);
            foreach (var obj in solidInFront)
            {
                obj.DepthState = DepthStateDefault;
                obj.Draw(gameTime);
                obj.RenderOrder = ++renderCounter;
            }

            if (solidBehind.Count > 0)
            {
                SetDepthState(DepthStateDefault);
                foreach (var obj in solidBehind) obj.DrawAfter(gameTime);
            }

            if (transparentObjects.Count > 0)
            {
                SetDepthState(DepthStateDepthRead);
                foreach (var obj in transparentObjects) obj.DrawAfter(gameTime);
            }

            if (solidInFront.Count > 0)
            {
                SetDepthState(DepthStateDefault);
                foreach (var obj in solidInFront) obj.DrawAfter(gameTime);
            }
        }


        private bool IsObjectInView(WorldObject obj)
        {
            Vector2 cam2D = new(Camera.Instance.Position.X, Camera.Instance.Position.Y);
            if (Vector2.DistanceSquared(cam2D, new Vector2(obj.Position.X, obj.Position.Y))
                > (Camera.Instance.ViewFar + cullingOffset) * (Camera.Instance.ViewFar + cullingOffset))
                return false;

            return boundingFrustum.Contains(new BoundingSphere(obj.Position, cullingOffset))
                   != ContainmentType.Disjoint;
        }

        private void UpdateBoundingFrustum()
        {
            Matrix view = Camera.Instance.View;
            Matrix projection = Camera.Instance.Projection;
            Matrix viewProjection = view * projection;
            boundingFrustum = new BoundingFrustum(viewProjection);
        }

        public override void Dispose()
        {
            var sw = Stopwatch.StartNew();

            var objects = Objects.ToArray();

            for (var i = 0; i < objects.Length; i++)
            {
                if (this is WalkableWorldControl walkeableWorld && objects[i] is PlayerObject player && walkeableWorld.Walker == player)
                    continue;

                objects[i].Dispose();
            }

            Objects.Clear();

            sw.Stop();

            var elapsedDisposingObjects = sw.ElapsedMilliseconds;

            sw.Restart();

            base.Dispose();

            sw.Stop();

            var elapsedDisposingBase = sw.ElapsedMilliseconds;

            Debug.WriteLine($"Dispose WorldControl {WorldIndex} - Disposing Objects: {elapsedDisposingObjects}ms - Disposing Base: {elapsedDisposingBase}ms");
        }
    }
}
