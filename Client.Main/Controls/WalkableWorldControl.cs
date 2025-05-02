﻿using Client.Main.Controllers;
using Client.Main.Models;
using Client.Main.Objects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Threading.Tasks;

namespace Client.Main.Controls
{
    public abstract class WalkableWorldControl : WorldControl
    {
        private CursorObject _cursor;
        public float _cursorNextMoveTime;

        public WalkerObject Walker { get; set; }
        public byte MouseTileX { get; set; } = 0;
        public byte MouseTileY { get; set; } = 0;
        public float ExtraHeight { get; set; }

        public WalkableWorldControl(short worldIndex) : base(worldIndex)
        {
            Interactive = true;
        }

        public WalkableWorldControl(short worldIndex, WalkerObject walker)
        : this(worldIndex)
        {
            Walker = walker;     // dzięki temu w blokach inicjalizacyjnych już nie jest null
        }


        public override async Task Load()
        {
            Objects.Add(_cursor = new CursorObject());
            await base.Load();
        }

        public override void Update(GameTime time)
        {
            if (Status != GameControlStatus.Ready || !Visible) return;


            CalculateMouseTilePos();

            if (Scene.MouseHoverControl == this && MuGame.Instance.Mouse.LeftButton == ButtonState.Pressed && _cursorNextMoveTime <= 0)
            {
                _cursorNextMoveTime = 250;
                var newPosition = new Vector2(MouseTileX, MouseTileY);

                if (!IsWalkable(newPosition))
                    return;

                var x = newPosition.X * Constants.TERRAIN_SCALE;
                var y = newPosition.Y * Constants.TERRAIN_SCALE;
                var terrainHeight = Terrain.RequestTerrainHeight(x, y);
                var pos = new Vector3(x, y, terrainHeight + ExtraHeight);
                _cursor.Position = pos - new Vector3(-50f, -40f, 0);
                Walker.MoveTo(newPosition);
            }
            else if (_cursorNextMoveTime > 0)
            {
                _cursorNextMoveTime -= (float)time.ElapsedGameTime.TotalMilliseconds;
            }

            base.Update(time);
        }

        private void CalculateMouseTilePos()
        {
            Vector2 mousePos = Mouse.GetState().Position.ToVector2();
            var viewport = GraphicsManager.Instance.GraphicsDevice.Viewport;

            Vector3 nearPoint = viewport.Unproject(new Vector3(mousePos, 0f), Camera.Instance.Projection, Camera.Instance.View, Matrix.Identity);
            Vector3 farPoint = viewport.Unproject(new Vector3(mousePos, 1f), Camera.Instance.Projection, Camera.Instance.View, Matrix.Identity);

            Vector3 rayDirection = farPoint - nearPoint;
            rayDirection.Normalize();

            Ray mouseRay = new Ray(nearPoint, rayDirection);

            float maxDistance = 10000f;
            float stepSize = Constants.TERRAIN_SCALE / 10f;
            float currentDistance = 0f;

            Vector3 lastPosition = mouseRay.Position;
            float lastHeightDifference = lastPosition.Z - Terrain.RequestTerrainHeight(lastPosition.X, lastPosition.Y) + ExtraHeight;

            bool hit = false;
            Vector3 hitPosition = Vector3.Zero;

            while (currentDistance < maxDistance)
            {
                currentDistance += stepSize;
                Vector3 position = mouseRay.Position + mouseRay.Direction * currentDistance;
                float terrainHeight = Terrain.RequestTerrainHeight(position.X, position.Y) + ExtraHeight;
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
    }
}
