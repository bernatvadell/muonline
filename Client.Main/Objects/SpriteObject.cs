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

            if (!Visible)
                return;

            Vector3 projected = GraphicsDevice.Viewport.Project(
                 WorldPosition.Translation,
                 Camera.Instance.Projection,
                 Camera.Instance.View,
                 Matrix.Identity);

            float layerDepth = MathHelper.Clamp(projected.Z, 0f, 1f);

            SpriteBatch.Begin(
                 sortMode: SpriteSortMode.BackToFront,
                 blendState: BlendState,
                 samplerState: null,
                 depthStencilState: DepthStencilState.DepthRead,
                 rasterizerState: null,
                 effect: null,
                 transformMatrix: null);

            SpriteBatch.Draw(
                 texture: SpriteTexture,
                 position: new Vector2(projected.X, projected.Y),
                 sourceRectangle: null,
                 color: LightEnabled ? new Color(Light) * TotalAlpha : Color.White * TotalAlpha,
                 rotation: TotalAngle.Z,
                 origin: new Vector2(SpriteTexture.Width / 2, SpriteTexture.Height / 2),
                 scale: _scaleMix,
                 effects: SpriteEffects.None,
                 layerDepth: layerDepth);

            SpriteBatch.End();

            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;
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
