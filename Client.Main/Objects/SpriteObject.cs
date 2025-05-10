using Client.Data.Texture;
using Client.Main.Content;
using Client.Main.Controllers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    public abstract class SpriteObject : WorldObject
    {
        private Vector2 _screenPosition;
        private float _scaleMix = 1f;

        protected SpriteBatch SpriteBatch { get; private set; }
        protected Texture2D SpriteTexture { get; private set; }
        protected TextureData TextureData { get; private set; }

        public abstract string TexturePath { get; }

        public SpriteObject()
        {
            BoundingBoxColor = Color.Red;
        }

        public override async Task Load()
        {
            await base.Load();

            TextureData = await TextureLoader.Instance.Prepare(TexturePath);

            if (TextureData != null)
            {
                SpriteBatch = new SpriteBatch(GraphicsDevice);
                SpriteTexture = TextureLoader.Instance.GetTexture2D(TexturePath);
            }
            else
            {
                Status = Models.GameControlStatus.Error;
            }
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);

            if (!Visible || SpriteTexture == null)
                return;

            // Project world position to screen
            Vector3 projected = GraphicsDevice.Viewport.Project(
                WorldPosition.Translation,
                Camera.Instance.Projection,
                Camera.Instance.View,
                Matrix.Identity);

            float layerDepth = MathHelper.Clamp(projected.Z, 0f, 1f);

            // If SpriteBatch is not begun, open a local SpriteBatchScope
            if (!Helpers.SpriteBatchScope.BatchIsBegun)
            {
                using (new Helpers.SpriteBatchScope(GraphicsManager.Instance.Sprite, SpriteSortMode.Deferred, BlendState, SamplerState.PointClamp, DepthState))
                {
                    DrawSprite(projected, layerDepth);
                }
            }
            else
            {
                DrawSprite(projected, layerDepth);
            }
        }

        protected void DrawSprite(Vector3 projected, float layerDepth)
        {
            var sb = GraphicsManager.Instance.Sprite;
            Color color = (BlendState == BlendState.Additive)
                ? Color.White * TotalAlpha
                : (LightEnabled ? new Color(Light) * TotalAlpha : Color.White * TotalAlpha);

            sb.Draw(
                SpriteTexture,
                new Vector2(projected.X, projected.Y),
                null,
                color,
                TotalAngle.Z,
                new Vector2(SpriteTexture.Width / 2f, SpriteTexture.Height / 2f),
                _scaleMix,
                SpriteEffects.None,
                layerDepth);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (!Visible) return;

            var screenPosition = GraphicsDevice.Viewport.Project(
                WorldPosition.Translation,
                Camera.Instance.Projection,
                Camera.Instance.View,
                Matrix.Identity
            );

            _screenPosition = new Vector2(screenPosition.X, screenPosition.Y);

            Vector3 worldScale = new Vector3(
                WorldPosition.Right.Length(),
                WorldPosition.Up.Length(),
                WorldPosition.Backward.Length()
            );

            float distanceToCamera = Vector3.Distance(Camera.Instance.Position, WorldPosition.Translation);
            float scaleFactor = Scale / (MathF.Max(distanceToCamera, 0.1f) / Constants.TERRAIN_SIZE);
            _scaleMix = scaleFactor * worldScale.X;
        }

        public override void Dispose()
        {
            base.Dispose();

            SpriteBatch?.Dispose();
            SpriteTexture = null;
        }
    }
}
