using Client.Data.ATT;
using Client.Data.BMD;
using Client.Data.CWS;
using Client.Data.MAP;
using Client.Data.OBJS;
using Client.Data.OZB;
using Client.Data.Texture;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Objects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;

namespace Client.Main.Controls
{
    public class WorldControl : GameControl
    {
        private GraphicsDevice _graphicsDevice;
        private BasicEffect _debugEffect;
        private BasicEffect _objectEffect;

        private CameraTourController _tourController;

        private int _front = 0;
        private int _lateral = 0;
        private int _height = 0;
        private int _headY = 0;
        private int _headX = 0;

        public TerrainControl Terrain { get; }
        public short WorldIndex { get; private set; }
        public bool TourMode { get; private set; }

        public List<WorldObject> Objects { get; private set; } = [];

        public Type[] MapTileObjects { get; } = new Type[(Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE_MASK) / 4];

        public WorldControl(short worldIndex, bool tourMode = false)
        {
            WorldIndex = worldIndex;
            TourMode = tourMode;

            Controls.Add(Terrain = new TerrainControl());
            Terrain.WorldIndex = worldIndex;
        }

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            var tasks = new List<Task>
            {
                base.Load(graphicsDevice)
            };

            CreateMapTileObjects();

            var worldFolder = $"World{WorldIndex}";

            _graphicsDevice = graphicsDevice;

            Camera.Instance.AspectRatio = graphicsDevice.Viewport.AspectRatio;

            _debugEffect = new BasicEffect(graphicsDevice)
            {
                VertexColorEnabled = true,
                World = Matrix.Identity
            };

            _objectEffect = new BasicEffect(graphicsDevice)
            {
                TextureEnabled = true,
                VertexColorEnabled = false,
                World = Matrix.Identity
            };

            _objectEffect.DirectionalLight0.Enabled = false;
            _objectEffect.DirectionalLight0.DiffuseColor = new Vector3(1, 1, 1);
            _objectEffect.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(1.3f, 0, 2));
            _objectEffect.DirectionalLight0.SpecularColor = new Vector3(0.1f, 0.1f, 0.1f);
            _objectEffect.AmbientLightColor = new Vector3(0.3f, 0.3f, 0.3f);
            _objectEffect.DiffuseColor = new Vector3(1, 1, 1);
            _objectEffect.SpecularPower = 16f;
            _objectEffect.SpecularColor = new Vector3(0, 0, 0);

            var objReader = new OBJReader();

            OBJ obj = await objReader.Load(Path.Combine(Constants.DataPath, worldFolder, $"EncTerrain{WorldIndex}.obj"));

            foreach (var mapObj in obj.Objects)
            {
                var instance = WorldObjectFactory.CreateMapObject(this, mapObj);
                if (instance != null) tasks.Add(AddObject(instance));
            }

            await Task.WhenAll(tasks);

            if (TourMode)
            {
                var cameraWalkScriptReader = new CWSReader();
                var cameraWalkScript = await cameraWalkScriptReader.Load(Path.Combine(Constants.DataPath, worldFolder, $"CWScript{WorldIndex}.cws"));
                _tourController = new CameraTourController(cameraWalkScript.WayPoints, true, this);
            };
        }

        public override void Update(GameTime time)
        {
            base.Update(time);

            if (TourMode)
                _tourController?.Update(time);

            var state = Keyboard.GetState();

            if (state.IsKeyDown(Keys.W)) _front += 10;
            if (state.IsKeyDown(Keys.S)) _front -= 10;
            if (state.IsKeyDown(Keys.A)) _lateral -= 10;
            if (state.IsKeyDown(Keys.D)) _lateral += 10;
            if (state.IsKeyDown(Keys.Space)) _height += 10;
            if (state.IsKeyDown(Keys.LeftControl)) _height -= 10;

            if (state.IsKeyDown(Keys.Left)) _headX -= 10;
            if (state.IsKeyDown(Keys.Right)) _headX += 10;
            if (state.IsKeyDown(Keys.Up)) _headY += 10;
            if (state.IsKeyDown(Keys.Down)) _headY -= 10;

            if (!TourMode)
            {
                Camera.Instance.Position = new Vector3(100 + _lateral, 900 + _front, 330 + _height);
                Camera.Instance.Target = new Vector3(100 + _lateral + _headX, 1200 + _front, 110 + _height + _headY);
            }

            _objectEffect.Projection = _debugEffect.Projection = Camera.Instance.Projection;
            _objectEffect.View = _debugEffect.View = Camera.Instance.View;

            foreach (var obj in Objects)
                obj.Update(time);
        }

        public override void Draw(GameTime time)
        {
            base.Draw(time);

            RenderObjects(time);
            // DrawTargetIndicator();
        }

        public async Task AddObject(WorldObject obj)
        {
            lock (Objects)
            {
                if (Objects.Contains(obj))
                    return;

                Objects.Add(obj);
            }

            await obj.Load(_graphicsDevice);
        }

        protected virtual void CreateMapTileObjects()
        {
            var typeMapObject = typeof(Objects.MapObject);

            for (var i = 0; i < MapTileObjects.Length; i++)
                MapTileObjects[i] = typeMapObject;
        }

        private void RenderObjects(GameTime gameTime)
        {
            foreach (var obj in Objects)
                obj.Draw(_objectEffect, gameTime);
        }

        private void DrawTargetIndicator()
        {
            // Define los vértices del triángulo
            var triangleVertices = new VertexPositionColor[3];
            float size = 45f; // Tamaño del triángulo

            // Vértice 1 (Centro)
            triangleVertices[0] = new VertexPositionColor(Camera.Instance.Target, Color.Yellow);

            // Vértice 2 (A la derecha)
            triangleVertices[1] = new VertexPositionColor(new Vector3(Camera.Instance.Target.X + size, Camera.Instance.Target.Y, Camera.Instance.Target.Z), Color.Yellow);

            // Vértice 3 (Arriba)
            triangleVertices[2] = new VertexPositionColor(new Vector3(Camera.Instance.Target.X, Camera.Instance.Target.Y + size, Camera.Instance.Target.Z), Color.Yellow);

            // Usamos el BasicEffect para renderizar el triángulo
            foreach (var pass in _debugEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, triangleVertices, 0, 1);
            }
        }
    }
}
