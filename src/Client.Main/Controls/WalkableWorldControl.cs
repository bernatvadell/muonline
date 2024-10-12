using Client.Main.Controllers;
using Client.Main.Objects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Controls
{
    public abstract class WalkableWorldControl(short worldIndex) : WorldControl(worldIndex)
    {
        private CursorObject _cursor;
        public float _cursorNextMoveTime;

        public WalkerObject Walker { get; set; }
        public Vector3 MoveTargetPosition { get; private set; }
        public float MoveSpeed { get; set; } = 250f;
        public bool IsMoving => Vector3.Distance(MoveTargetPosition, TargetPosition) > 0f;

        public byte MouseTileX { get; set; } = 0;
        public byte MouseTileY { get; set; } = 0;

        public override Vector3 TargetPosition
        {
            get
            {
                var x = Walker.Location.X * Constants.TERRAIN_SCALE + 0.5f * Constants.TERRAIN_SCALE;
                var y = Walker.Location.Y * Constants.TERRAIN_SCALE + 0.5f * Constants.TERRAIN_SCALE;
                var v = new Vector3(x, y, Terrain.RequestTerrainHeight(x, y));
                return v;
            }
        }

        public override async Task Load()
        {
            await AddObjectAsync(_cursor = new CursorObject());
            MoveTargetPosition = Vector3.Zero;
            await base.Load();
        }

        public override void Update(GameTime time)
        {
            if (Status != GameControlStatus.Ready || !Visible) return;

            CalculateMouseTilePos();

            if (MuGame.Instance.Mouse.LeftButton == ButtonState.Pressed && _cursorNextMoveTime <= 0)
            {
                _cursorNextMoveTime = 250;
                Walker.Location = new Vector2(MouseTileX, MouseTileY);
                var x = Walker.Location.X * Constants.TERRAIN_SCALE;
                var y = Walker.Location.Y * Constants.TERRAIN_SCALE;
                var pos = new Vector3(x, y, Terrain.RequestTerrainHeight(x, y));
                _cursor.Position = pos - new Vector3(-50f, -40f, 0);
            }
            else if (_cursorNextMoveTime > 0)
            {
                _cursorNextMoveTime -= (float)time.ElapsedGameTime.TotalMilliseconds;
            }

            MoveCameraPosition(time);

            base.Update(time);
        }

        private void CalculateMouseTilePos()
        {
            var mouseRay = MuGame.Instance.MouseRay;

            float maxDistance = 10000f;
            float stepSize = Constants.TERRAIN_SCALE / 10f;
            float currentDistance = 0f;

            Vector3 lastPosition = mouseRay.Position;
            float lastHeightDifference = lastPosition.Z - Terrain.RequestTerrainHeight(lastPosition.X, lastPosition.Y);

            bool hit = false;
            Vector3 hitPosition = Vector3.Zero;

            while (currentDistance < maxDistance)
            {
                currentDistance += stepSize;
                Vector3 position = mouseRay.Position + mouseRay.Direction * currentDistance;
                float terrainHeight = Terrain.RequestTerrainHeight(position.X, position.Y);

                float heightDifference = position.Z - terrainHeight;

                if (lastHeightDifference > 0 && heightDifference <= 0)
                {
                    hit = true;
                    float t = lastHeightDifference / (lastHeightDifference - heightDifference);
                    hitPosition = Vector3.Lerp(lastPosition, position, t);

                    break;
                }

                lastPosition = position;
                lastHeightDifference = heightDifference;
            }

            if (hit)
            {
                float terrainX = hitPosition.X;
                float terrainY = hitPosition.Y;

                int gridX = (int)(terrainX / Constants.TERRAIN_SCALE);
                int gridY = (int)(terrainY / Constants.TERRAIN_SCALE);

                gridX = Math.Clamp(gridX, 0, Constants.TERRAIN_SIZE - 1);
                gridY = Math.Clamp(gridY, 0, Constants.TERRAIN_SIZE - 1);

                MouseTileX = (byte)gridX;
                MouseTileY = (byte)gridY;
            }
            else
            {
                MouseTileX = 0;
                MouseTileY = 0;
            }
        }

        private void MoveCameraPosition(GameTime time)
        {
            if (MoveTargetPosition == Vector3.Zero)
            {
                MoveTargetPosition = TargetPosition;
                UpdateCameraPosition(MoveTargetPosition);
                return;
            }

            if (!IsMoving)
            {
                MoveTargetPosition = TargetPosition;
                return;
            }

            Vector3 direction = TargetPosition - MoveTargetPosition;
            direction.Normalize();

            float deltaTime = (float)time.ElapsedGameTime.TotalSeconds;
            Vector3 moveVector = direction * MoveSpeed * deltaTime;

            // Verifica si la distancia a mover excede la distancia restante al objetivo
            if (moveVector.Length() > (MoveTargetPosition - TargetPosition).Length())
            {
                UpdateCameraPosition(TargetPosition);
            }
            else
            {
                UpdateCameraPosition(MoveTargetPosition + moveVector);
            }
        }

        private void UpdateCameraPosition(Vector3 position)
        {
            MoveTargetPosition = position;

            var cameraDistance = 1000f;

            var p = new Vector3(0, -cameraDistance, 0f);
            var m = MathUtils.AngleMatrix(new Vector3(0, 0, -48.5f));
            var t = MathUtils.VectorIRotate(p, m);

            Camera.Instance.FOV = 35;
            Camera.Instance.Position = position + t + new Vector3(0, 0, cameraDistance);
            Camera.Instance.Target = position;
        }
    }
}
