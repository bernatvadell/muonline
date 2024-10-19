using Client.Data.ATT;
using Client.Data.CWS;
using Client.Data.OBJS;
using Client.Main.Models;
using Client.Main.Objects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Client.Main.Controls
{
    public abstract class WorldControl : GameControl
    {
        public TerrainControl Terrain { get; }
        public short WorldIndex { get; private set; }
        public List<WorldObject> Objects { get; private set; } = [];
        public Type[] MapTileObjects { get; } = new Type[Constants.TERRAIN_SIZE];

        public WorldControl(short worldIndex)
        {
            AutoSize = false;
            Width = MuGame.Instance.Width;
            Height = MuGame.Instance.Height;
            WorldIndex = worldIndex;
            Controls.Add(Terrain = new TerrainControl());
            Terrain.WorldIndex = worldIndex;
        }

        public void ChangeWorld(short worldIndex)
        {
            WorldIndex = worldIndex;

            if (Status == GameControlStatus.NonInitialized)
                return;

            Task.Run(() => Initialize());
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
                    if (instance != null) tasks.Add(AddObjectAsync(instance));
                }
                await Task.WhenAll(tasks);
            }

        }
        public override void Update(GameTime time)
        {
            base.Update(time);

            for (var i = 0; i < Objects.Count; i++)
                Objects[i].Update(time);
        }
        public override void Draw(GameTime time)
        {
            base.Draw(time);
            RenderObjects(time);
        }

        public bool IsWalkable(Vector2 position)
        {
            var terrainFlag = Terrain.RequestTerraingFlag((int)position.X, (int)position.Y);
            return !terrainFlag.HasFlag(TWFlags.NoMove);
        }

        public void AddObject(WorldObject obj)
        {
            lock (Objects)
            {
                if (Objects.Contains(obj))
                    return;

                Objects.Add(obj);
            }

            Task.Run(() => obj.Load());
        }

        public async Task AddObjectAsync(WorldObject obj)
        {
            lock (Objects)
            {
                if (Objects.Contains(obj))
                    return;

                Objects.Add(obj);
            }

            await obj.Load();
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
            base.Dispose();

            var objects = Objects.ToArray();

            for (var i = 0; i < objects.Length; i++)
                objects[i].Dispose();

            GC.SuppressFinalize(this);
        }
    }
}
