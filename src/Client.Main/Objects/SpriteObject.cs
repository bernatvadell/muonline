using Client.Data.Texture;
using Client.Main.Content;
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
        public override bool Ready => SpriteTexture != null;

        public SpriteObject()
        {
            BoundingBoxColor = Color.Red;
        }

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            await base.Load(graphicsDevice);

            TextureData = await TextureLoader.Instance.Prepare(TexturePath);

            if (TextureData != null)
            {
                SpriteBatch = new SpriteBatch(GraphicsDevice);
                SpriteTexture = TextureLoader.Instance.GetTexture2D(TexturePath);
                BoundingBoxLocal = new BoundingBox(new Vector3(-40, -40, 0), new Vector3(40, 40, 80));
            }
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);

            if (!Visible) return;

            SpriteBatch.Begin(
                blendState: BlendState
            );

            SpriteBatch.Draw(
                texture: SpriteTexture,
                position: _screenPosition,
                sourceRectangle: null,
                color: LightEnabled ? new Color(Light) * TotalAlpha : Color.White * TotalAlpha,
                rotation: TotalAngle.Z,
                origin: new Vector2(SpriteTexture.Width / 2, SpriteTexture.Height / 2),
                scale: _scaleMix,
                effects: SpriteEffects.None,
                layerDepth: 0
            );

            SpriteBatch.End();

            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (!Ready) return;

            var screenPosition = GraphicsDevice.Viewport.Project(
                Origin,
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
