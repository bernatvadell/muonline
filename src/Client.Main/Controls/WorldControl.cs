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
        private GraphicsDevice _graphicsDevice;
        private CameraTourController _tourController;
        private Vector3 _currentTargetPosition;


        public bool IsMoving => Vector3.Distance(_currentTargetPosition, TargetPosition) > 1f;
        public byte PositionX { get; set; } = 138;
        public byte PositionY { get; set; } = 124;


        public Vector3 TargetPosition
        {
            get
            {
                var x = PositionX * Constants.TERRAIN_SCALE + 0.5f * Constants.TERRAIN_SCALE;
                var y = PositionY * Constants.TERRAIN_SCALE + 0.5f * Constants.TERRAIN_SCALE;
                var v = new Vector3(x, y, Terrain.RequestTerrainHeight(x, y));
                return v;
            }
        }

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
            await base.Load(graphicsDevice);

            var tasks = new List<Task>();

            CreateMapTileObjects();

            var worldFolder = $"World{WorldIndex}";

            _graphicsDevice = graphicsDevice;

            Camera.Instance.AspectRatio = graphicsDevice.Viewport.AspectRatio;

            _currentTargetPosition = Vector3.Zero;

            await Task.WhenAll(tasks);

            var objReader = new OBJReader();

            var objectPath = Path.Combine(Constants.DataPath, worldFolder, $"EncTerrain{WorldIndex}.obj");

            if (File.Exists(objectPath))
            {
                OBJ obj = await objReader.Load(objectPath);

                foreach (var mapObj in obj.Objects)
                {
                    var instance = WorldObjectFactory.CreateMapTileObject(this, mapObj);
                    if (instance != null) tasks.Add(AddObject(instance));
                }
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
            {
                _tourController?.Update(time);
            }
            else
            {
                if (!IsMoving)
                {
                    var state = Keyboard.GetState();

                    if (state.IsKeyDown(Keys.W))
                    {
                        PositionX -= 1;
                        PositionY += 1;
                    }
                    if (state.IsKeyDown(Keys.A))
                    {
                        PositionX -= 1;
                        PositionY -= 1;
                    }
                    if (state.IsKeyDown(Keys.S))
                    {
                        PositionX += 1;
                        PositionY -= 1;
                    }
                    if (state.IsKeyDown(Keys.D))
                    {
                        PositionX += 1;
                        PositionY += 1;
                    }
                }

                MoveCameraPosition(time);
            }

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

            await obj.Load(_graphicsDevice);
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
        }

        private void MoveCameraPosition(GameTime time)
        {
            if (_currentTargetPosition == Vector3.Zero)
            {
                _currentTargetPosition = TargetPosition;
                UpdateCameraPosition(_currentTargetPosition);
                return;
            }

            if (!IsMoving)
            {
                _currentTargetPosition = TargetPosition;
                return;
            }

            Vector3 direction = TargetPosition - _currentTargetPosition;
            direction.Normalize();

            float deltaTime = (float)time.ElapsedGameTime.TotalSeconds;
            Vector3 moveVector = direction * 300f * deltaTime;

            // Verifica si la distancia a mover excede la distancia restante al objetivo
            if (moveVector.Length() > (_currentTargetPosition - TargetPosition).Length())
            {
                UpdateCameraPosition(TargetPosition);
            }
            else
            {
                UpdateCameraPosition(_currentTargetPosition + moveVector);
            }
        }

        private void UpdateCameraPosition(Vector3 position)
        {
            _currentTargetPosition = position;

            var cameraDistance = 1000f;

            var p = new Vector3(0, -cameraDistance, 0f);
            var m = MathUtils.AngleMatrix(new Vector3(0, 0, -45));
            var t = MathUtils.VectorIRotate(p, m);

            Camera.Instance.Position = position + t + new Vector3(0, 0, cameraDistance - 150f);
            Camera.Instance.Target = position;
        }
    }
}
