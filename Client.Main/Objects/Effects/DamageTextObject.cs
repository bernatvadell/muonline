using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;
using Client.Main.Controllers;  // For GraphicsManager
using Client.Main.Models;
using Client.Main.Helpers;

namespace Client.Main.Objects.Effects
{
    public class DamageTextObject : WorldObject
    {
        public string Text { get; }
        public Color TextColor { get; }

        private const float Lifetime = 1.2f;          // Total lifetime in seconds
        private float _elapsedTime = 0f;
        private Vector2 _screenPosition;
        private const float VerticalSpeed = 40f;      // Pixels per second
        private const float InitialZOffset = 40f;     // Initial upward offset

        public DamageTextObject(string text, Vector3 worldHitPosition, Color color)
        {
            Text = text;
            Position = worldHitPosition + Vector3.UnitZ * InitialZOffset;
            TextColor = color;
            Alpha = 1.0f;
            Scale = 1.0f;
            IsTransparent = true;
            AffectedByTransparency = false;
            Status = GameControlStatus.Ready;
        }

        public override Task Load()
        {
            Status = GameControlStatus.Ready;
            return Task.CompletedTask;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (Status != GameControlStatus.Ready) return;

            float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _elapsedTime += delta;

            // Fade out after 40% of lifetime
            float fadeStart = Lifetime * 0.4f;
            if (_elapsedTime > fadeStart)
            {
                Alpha = MathHelper.Clamp(1.0f - (_elapsedTime - fadeStart) / (Lifetime - fadeStart), 0f, 1f);
            }

            // Move upward
            Position += Vector3.UnitZ * VerticalSpeed * delta;
            RecalculateWorldPosition();

            if (_elapsedTime >= Lifetime)
            {
                World?.RemoveObject(this);
                Dispose();
                return;
            }

            // Project to screen coordinates
            Vector3 proj = GraphicsDevice.Viewport.Project(
                WorldPosition.Translation,
                Camera.Instance.Projection,
                Camera.Instance.View,
                Matrix.Identity);

            // Hide if behind camera or out of view
            Hidden = proj.Z < 0f || proj.Z > 1f;
            if (!Hidden)
            {
                _screenPosition = new Vector2(proj.X, proj.Y);
            }
        }

        public override void Draw(GameTime gameTime)
        {
            // No 3D drawing
        }

        public override void DrawAfter(GameTime gameTime)
        {
            if (!Visible)
                return;

            var spriteBatch = GraphicsManager.Instance.Sprite;
            var font = GraphicsManager.Instance.Font;
            if (spriteBatch == null || font == null)
                return;

            float fontSize = 14f;
            float scaleFactor = fontSize / Constants.BASE_FONT_SIZE;
            Vector2 origin = font.MeasureString(Text) * 0.5f;
            Color color = TextColor * Alpha;

            using (new SpriteBatchScope(
                spriteBatch,
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.LinearClamp,
                DepthStencilState.None,
                RasterizerState.CullNone))
            {
                spriteBatch.DrawString(
                    font,
                    Text,
                    _screenPosition,
                    color,
                    0f,
                    origin,
                    scaleFactor,
                    SpriteEffects.None,
                    0f);
            }
        }

    }
}
