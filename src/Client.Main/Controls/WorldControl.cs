using Client.Data.CWS;
using Client.Data.OBJS;
using Client.Main.Controllers;
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
    public class WorldControl : GameControl
    {
        public virtual Vector3 TargetPosition { get; }
        public TerrainControl Terrain { get; }
        public short WorldIndex { get; private set; }
        public List<WorldObject> Objects { get; private set; } = [];
        public Type[] MapTileObjects { get; } = new Type[Constants.TERRAIN_SIZE];

        public WorldControl(short worldIndex)
        {
            WorldIndex = worldIndex;
            Controls.Add(Terrain = new TerrainControl());
            Terrain.WorldIndex = worldIndex;
        }

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            await base.Load(graphicsDevice);

            CreateMapTileObjects();

            var worldFolder = $"World{WorldIndex}";


            Camera.Instance.AspectRatio = graphicsDevice.Viewport.AspectRatio;

            var objReader = new OBJReader();

            var objectPath = Path.Combine(Constants.DataPath, worldFolder, $"EncTerrain{WorldIndex}.obj");

            if (File.Exists(objectPath))
            {
                var tasks = new List<Task>();
                OBJ obj = await objReader.Load(objectPath);

                foreach (var mapObj in obj.Objects)
                {
                    var instance = WorldObjectFactory.CreateMapTileObject(this, mapObj);
                    if (instance != null) tasks.Add(AddObject(instance));
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
        public async Task AddObject(WorldObject obj)
        {
            lock (Objects)
            {
                if (Objects.Contains(obj))
                    return;

                Objects.Add(obj);
            }

            await obj.Load(GraphicsDevice);
        }

        protected virtual void CreateMapTileObjects()
        {
            var typeMapObject = typeof(Objects.MapTileObject);

            for (var i = 0; i < MapTileObjects.Length; i++)
                MapTileObjects[i] = typeMapObject;
        }

        private void RenderObjects(GameTime gameTime)
        {
            foreach (var obj in Objects)
                obj.Draw(gameTime);

            foreach (var obj in Objects)
                obj.DrawAfter(gameTime);
        }

        public override void Dispose()
        {
            base.Dispose();

            var objects = Objects.ToArray();

            for (var i = 0; i < objects.Length; i++)
                objects[i].Dispose();
        }
    }
}
