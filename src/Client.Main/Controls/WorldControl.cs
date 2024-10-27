using Client.Data.ATT;
using Client.Data.BMD;
using Client.Data.CAP;
using Client.Data.CWS;
using Client.Data.OBJS;
using Client.Main.Content;
using Client.Main.Controllers;
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
        public string BackgroundMusicPath { get; set; }
        public TerrainControl Terrain { get; }
        public short WorldIndex { get; private set; }
        public ChildrenCollection<WorldObject> Objects { get; private set; } = new ChildrenCollection<WorldObject>(null);
        public Type[] MapTileObjects { get; } = new Type[Constants.TERRAIN_SIZE];

        public WorldControl(short worldIndex)
        {
            AutoViewSize = false;
            ViewSize = new(MuGame.Instance.Width, MuGame.Instance.Height);
            WorldIndex = worldIndex;
            Controls.Add(Terrain = new TerrainControl() { WorldIndex = worldIndex });
            Objects.ControlAdded += Object_Added;
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

                //float x = data.CameraDistance * (float)Math.Cos(data.CameraAngle.X) * (float)Math.Sin(data.CameraAngle.Z);
                //float y = data.CameraDistance * (float)Math.Cos(data.CameraAngle.X) * (float)Math.Cos(data.CameraAngle.Z);
                //float z = data.CameraDistance * (float)Math.Sin(data.CameraAngle.X) + data.CameraZDistance;

                //var cameraOffset = new Vector3(x, y, z + 150);

                //var terrainHeight = Terrain.RequestTerrainHeight(data.HeroPosition.X, data.HeroPosition.Y);
                //var targetOffset = new Vector3(0, 0, terrainHeight);

                Camera.Instance.FOV = data.CameraFOV;
                Camera.Instance.Position = data.CameraPosition;
                Camera.Instance.Target = data.HeroPosition;
            }

            SoundController.Instance.PlayBackgroundMusic(BackgroundMusicPath);
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
