using Client.Main.Objects.Effects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    public class CursorObject : WorldObject
    {
        public override async Task Load()
        {
            Children.Add(new MoveTargetPostEffectObject());
            await base.Load();
        }

        public override void Update(GameTime gameTime)
        {
            Vector2 mousePosition = new Vector2(MuGame.Instance.Mouse.Position.X, MuGame.Instance.Mouse.Position.Y);

            Vector3 nearPoint = GraphicsDevice.Viewport.Unproject(
                new Vector3(mousePosition, 0),
                Camera.Instance.Projection,
                Camera.Instance.View,
                Matrix.Identity
            );

            Vector3 farPoint = GraphicsDevice.Viewport.Unproject(
                new Vector3(mousePosition, 1),
                Camera.Instance.Projection,
                Camera.Instance.View,
                Matrix.Identity
            );

            Vector3 direction = Vector3.Normalize(farPoint - nearPoint);

            float zPlane = 210f;

            float t = (zPlane - nearPoint.Z) / direction.Z;

            Vector3 worldPosition = nearPoint + direction * t;

            float terrainHeight = World.Terrain.RequestTerrainHeight(worldPosition.X, worldPosition.Y);

            Position = new Vector3(worldPosition.X, worldPosition.Y, terrainHeight);

            base.Update(gameTime);
        }
    }
}
