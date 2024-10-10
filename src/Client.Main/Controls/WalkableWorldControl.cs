using Client.Main.Controllers;
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
        public Vector3 MoveTargetPosition { get; private set; }

        public bool IsMoving => Vector3.Distance(MoveTargetPosition, TargetPosition) > 0f;
        public byte PositionX { get; set; } = 138;
        public byte PositionY { get; set; } = 124;

        public override Vector3 TargetPosition
        {
            get
            {
                var x = PositionX * Constants.TERRAIN_SCALE + 0.5f * Constants.TERRAIN_SCALE;
                var y = PositionY * Constants.TERRAIN_SCALE + 0.5f * Constants.TERRAIN_SCALE;
                var v = new Vector3(x, y, Terrain.RequestTerrainHeight(x, y));
                return v;
            }
        }

        public override async Task Load()
        {
            MoveTargetPosition = Vector3.Zero;
            await base.Load();
        }

        public override void Update(GameTime time)
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

            base.Update(time);
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
            Vector3 moveVector = direction * 1000f * deltaTime;

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
