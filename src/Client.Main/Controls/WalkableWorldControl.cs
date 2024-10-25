using Client.Main.Controllers;
using Client.Main.Models;
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

        public override async Task Load()
        {
            Objects.Add(_cursor = new CursorObject());
            await base.Load();
        }

        public override void Update(GameTime time)
        {
            if (Status != GameControlStatus.Ready || !Visible) return;

            if (Objects[^1] != Walker)
            {
                //var currentPosition = Objects.IndexOf(Walker);
                //Objects.RemoveAt(currentPosition);
                //Objects.Insert(Objects.Count, Walker);
            }

            CalculateMouseTilePos();

            if (Scene.MouseHoverControl == this && MuGame.Instance.Mouse.LeftButton == ButtonState.Pressed && _cursorNextMoveTime <= 0)
            {
                _cursorNextMoveTime = 250;
                var newPosition = new Vector2(MouseTileX, MouseTileY);

                if (!IsWalkable(newPosition))
                    return;

                var x = newPosition.X * Constants.TERRAIN_SCALE;
                var y = newPosition.Y * Constants.TERRAIN_SCALE;
                var pos = new Vector3(x, y, Terrain.RequestTerrainHeight(x, y) + ExtraHeight);
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
            var mouseRay = MuGame.Instance.MouseRay;

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
