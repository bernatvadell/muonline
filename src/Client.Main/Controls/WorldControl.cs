using Client.Data.ATT;
using Client.Data.CWS;
using Client.Data.OBJS;
using Client.Main.Models;
using Client.Main.Objects;
using Client.Main.Objects.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Client.Main.Controls
{
    public abstract class WorldControl : GameControl
    {
        public TerrainControl Terrain { get; }
        public short WorldIndex { get; private set; }
        public ChildrenCollection<WorldObject> Objects { get; private set; } = new ChildrenCollection<WorldObject>(null);
        public Type[] MapTileObjects { get; } = new Type[Constants.TERRAIN_SIZE];

        public WorldControl(short worldIndex)
        {
            AutoSize = false;
            Width = MuGame.Instance.Width;
            Height = MuGame.Instance.Height;
            WorldIndex = worldIndex;
            Controls.Add(Terrain = new TerrainControl() { WorldIndex = worldIndex });

            Objects.ControlAdded += Object_Added;
        }

        private void Object_Added(object sender, ChildrenEventArgs<WorldObject> e)
        {
            e.Control.World = this;
        }

        public void ChangeWorld(short worldIndex)
        {
            WorldIndex = worldIndex;

            if (Status == GameControlStatus.NonInitialized)
                return;

            Task.Run(() => Initialize()).ConfigureAwait(false);
        }

        public override async Task Load()
        {
            await base.Load();

            CreateMapTileObjects();

            var worldFolder = $"World{WorldIndex}";


            Camera.Instance.AspectRatio = GraphicsDevice.Viewport.AspectRatio;

            var objReader = new OBJReader();

            var objectPath = Path.Combine(Constants.DataPath, worldFolder, $"EncTerrain{WorldIndex}.obj");

            if (File.Exists(objectPath))
            {
                var tasks = new List<Task>();
                OBJ obj = await objReader.Load(objectPath);

                foreach (var mapObj in obj.Objects)
                {
                    var instance = WorldObjectFactory.CreateMapTileObject(this, mapObj);
                    if (instance != null) tasks.Add(instance.Load());
                }
                await Task.WhenAll(tasks);
            }

        }
        public override void Update(GameTime time)
        {
            base.Update(time);

            if (Status != GameControlStatus.Ready)
                return;

            for (var i = 0; i < Objects.Count; i++)
                Objects[i].Update(time);
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
            var typeMapObject = typeof(Objects.MapTileObject);

            for (var i = 0; i < MapTileObjects.Length; i++)
                MapTileObjects[i] = typeMapObject;
        }

        private void RenderObjects(GameTime gameTime)
        {
            var objs = Objects.ToArray();

            foreach (var obj in objs)
                obj.Draw(gameTime);

            foreach (var obj in objs)
                obj.DrawAfter(gameTime);
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
