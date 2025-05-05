// --- START OF FILE DamageTextObject.cs ---
using Client.Main.Controllers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Text;
using System.Threading.Tasks; // For StringBuilder

namespace Client.Main.Objects.Effects // Or Objects.UI
{
    public class DamageTextObject : WorldObject
    {
        public string Text { get; }
        public Color TextColor { get; }
        public float FontSize { get; }
        public float Lifetime { get; } // Total duration in seconds
        public float RiseSpeed { get; } // Pixels per second to rise
        public float FadeDuration { get; } // Duration of fade-out in seconds

        private float _elapsedTime;
        private Vector2 _screenPosition;
        private float _currentAlpha = 1.0f;
        private SpriteFont _font;
        private StringBuilder _stringBuilder = new StringBuilder(); // Reuse StringBuilder

        public DamageTextObject(string text, Vector3 initialPosition, Color color, float lifetime = 2.0f, float fontSize = 14f, float riseSpeed = 30f, float fadeStartOffset = 0.5f)
        {
            Text = text;
            Position = initialPosition; // Set initial 3D position
            TextColor = color;
            Lifetime = lifetime;
            FontSize = fontSize;
            RiseSpeed = riseSpeed;
            FadeDuration = Math.Max(0.1f, lifetime * fadeStartOffset); // Fade starts in the last part of lifetime
            _elapsedTime = 0f;

            // We don't need a model, so BlendState isn't critical here,
            // but set Alpha for clarity if needed elsewhere.
            BlendState = BlendState.AlphaBlend;
            Alpha = 1.0f; // Initial world object alpha (might not be used directly for text)
        }

        public override Task Load()
        {
            // Font is loaded globally via GraphicsManager
            _font = GraphicsManager.Instance.Font;
            if (_font == null)
            {
                Status = Models.GameControlStatus.Error;
                System.Diagnostics.Debug.WriteLine("Error: DamageTextObject could not get font from GraphicsManager.");
            }
            // No other specific content to load for this object
            return base.Load(); // Marks status as Ready if font is okay
        }

        public override void Update(GameTime gameTime)
        {
            // Don't call base.Update if you don't need its logic (like bounding box checks)
            // base.Update(gameTime);

            if (Status != Models.GameControlStatus.Ready) return;

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _elapsedTime += deltaTime;

            // Make the text rise
            Position = new Vector3(Position.X, Position.Y, Position.Z + RiseSpeed * deltaTime);
            // Recalculate world position if needed (though it's simple translation here)
            RecalculateWorldPosition();

            // Calculate fading alpha
            if (_elapsedTime >= Lifetime - FadeDuration)
            {
                float fadeProgress = (_elapsedTime - (Lifetime - FadeDuration)) / FadeDuration;
                _currentAlpha = MathHelper.Lerp(1.0f, 0.0f, fadeProgress);
            }
            else
            {
                _currentAlpha = 1.0f;
            }

            // Check if lifetime expired
            if (_elapsedTime >= Lifetime)
            {
                Dispose(); // Mark for removal
                return;
            }

            // Project 3D position to 2D screen coordinates for drawing
            Vector3 screenPos3D = GraphicsDevice.Viewport.Project(
                WorldPosition.Translation, // Use the calculated WorldPosition
                Camera.Instance.Projection,
                Camera.Instance.View,
                Matrix.Identity
            );

            // Check if the text is behind the camera
            if (screenPos3D.Z < 0 || screenPos3D.Z > 1)
            {
                // Off-screen or behind camera, effectively invisible
                _screenPosition = new Vector2(-1000, -1000); // Move off-screen
            }
            else
            {
                _screenPosition = new Vector2(screenPos3D.X, screenPos3D.Y);
            }
        }

        // Use DrawAfter to ensure text appears on top of other objects
        public override void DrawAfter(GameTime gameTime)
        {
            if (Status != Models.GameControlStatus.Ready || _font == null || _currentAlpha <= 0.01f)
                return;

            SpriteBatch spriteBatch = GraphicsManager.Instance.Sprite;
            float scaleFactor = FontSize / Constants.BASE_FONT_SIZE;

            // Reuse StringBuilder
            _stringBuilder.Clear();
            _stringBuilder.Append(Text);

            Vector2 textSize = _font.MeasureString(_stringBuilder) * scaleFactor;
            Vector2 textOrigin = textSize / 2f; // Center the text
            Vector2 drawPosition = _screenPosition - textOrigin;

            // Use NonPremultiplied for cleaner text rendering with alpha
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            // Draw shadow first (optional, but common)
            Color shadowColor = Color.Black * (_currentAlpha * 0.6f); // Semi-transparent black shadow
            spriteBatch.DrawString(_font, _stringBuilder, drawPosition + Vector2.One, shadowColor, 0f, Vector2.Zero, scaleFactor, SpriteEffects.None, 0f);

            // Draw main text
            Color finalColor = TextColor * _currentAlpha;
            spriteBatch.DrawString(_font, _stringBuilder, drawPosition, finalColor, 0f, Vector2.Zero, scaleFactor, SpriteEffects.None, 0f);

            spriteBatch.End();

            // Restore default render states if needed (though SpriteBatch usually handles this)
            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;

            // Don't call base.DrawAfter if it does nothing relevant
            // base.DrawAfter(gameTime);
        }

        // Ensure Dispose cleans up if needed, though this object has few resources
        public override void Dispose()
        {
            // If we had specific resources like textures, dispose them here
            base.Dispose(); // Calls Parent?.Children.Remove(this)
        }
    }
}
// --- END OF FILE DamageTextObject.cs ---