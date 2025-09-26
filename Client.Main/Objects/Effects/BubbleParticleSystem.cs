using Client.Main.Controllers;
using Client.Main.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Threading.Tasks;

namespace Client.Main.Objects.Effects
{
    public class BubbleParticleSystem : WorldObject
    {
        // Each bubble info. Using a struct avoids heap allocations
        private struct Bubble
        {
            public Vector3 BasePosition;  // Original spawn position (used for oscillation)
            public Vector3 Position;      // Current position
            public float Speed;           // Vertical speed
            public float Size;            // Diameter of the bubble
            public float Life;            // How long the bubble has existed
            public float MaxLife;         // Max lifetime before reset

            // Horizontal oscillation parameters
            public float SwayAmplitude;   // Maximum offset in the horizontal axis
            public float SwayFrequency;   // Frequency of oscillation
            public float SwayPhase;       // Phase offset for oscillation
        }

        private readonly Bubble[] _bubbles = new Bubble[MAX_BUBBLES];
        private readonly Random _rnd = new Random();

        // Texture for the bubble (PNG with transparency)
        private Texture2D _bubbleTexture;

        // Adjusted parameters
        private const int MAX_BUBBLES = 20; // Fewer bubbles for a cleaner effect
        private const float MIN_SPEED = 10f;
        private const float MAX_SPEED = 50f;
        private const float MIN_SIZE = 5f;  // Larger bubbles
        private const float MAX_SIZE = 10f;
        private const float MIN_LIFE = 15f;   // Longer lifetime
        private const float MAX_LIFE = 25f;

        public override async Task Load()
        {
            // Load bubble texture (ensure that Bubbles.png has proper transparency settings)
            _bubbleTexture = MuGame.Instance.Content.Load<Texture2D>("Bubbles");

            // Generate initial bubbles
            for (int i = 0; i < MAX_BUBBLES; i++)
                _bubbles[i] = CreateBubble(randomizeLife: true);

            await base.Load();
        }

        public override void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Update each bubble
            for (int i = 0; i < _bubbles.Length; i++)
            {
                var b = _bubbles[i];
                b.Life += dt;

                SyncBubbleTransform(ref b);

                // If bubble's lifetime is over, reset it
                if (b.Life >= b.MaxLife)
                {
                    _bubbles[i] = CreateBubble();
                }
                else
                {
                    _bubbles[i] = b;
                }
            }

            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            if (_bubbleTexture == null)
                return;

            // Don't render bubbles in normal Draw pass
            DrawBoundingBox2D();
            base.Draw(gameTime);
        }

        public override void DrawAfter(GameTime gameTime)
        {
            if (_bubbleTexture == null)
                return;

            var sb = GraphicsManager.Instance.Sprite;

            // Render bubbles with depth write enabled so they can occlude other objects
            using (new SpriteBatchScope(
                       sb,
                       SpriteSortMode.Immediate,
                       BlendState.AlphaBlend,
                       SamplerState.LinearClamp,
                       DepthStencilState.Default,
                       RasterizerState.CullNone))
            {
                for (int i = 0; i < _bubbles.Length; i++)
                    DrawBubbleBillboard(_bubbles[i]);
            }

            base.DrawAfter(gameTime);
        }

        private new void DrawBoundingBox2D()
        {
            // Use the public Font instead of the private `_font`
            var font = GraphicsManager.Instance.Font;
            if (!(Constants.DRAW_BOUNDING_BOXES && IsMouseHover && font != null))
                return;

            // Build the diagnostic text
            var sbInfo = new System.Text.StringBuilder();
            sbInfo.AppendLine(GetType().Name);
            sbInfo.Append("Type ID: ").AppendLine(Type.ToString());
            sbInfo.Append("Alpha: ").AppendLine(TotalAlpha.ToString());
            sbInfo.Append("X: ").Append(Position.X).Append(" Y: ").Append(Position.Y)
                  .Append(" Z: ").AppendLine(Position.Z.ToString());
            sbInfo.Append("Depth: ").AppendLine(Depth.ToString());
            sbInfo.Append("Render order: ").AppendLine(RenderOrder.ToString());
            sbInfo.Append("DepthStencilState: ").Append(DepthState.Name);
            string objectInfo = sbInfo.ToString();

            float scaleFactor = DebugFontSize / Constants.BASE_FONT_SIZE;
            Vector2 textSize = font.MeasureString(objectInfo) * scaleFactor;

            // Project into screen space
            Vector3 worldTextPos = new Vector3(
                (BoundingBoxWorld.Min.X + BoundingBoxWorld.Max.X) / 2,
                BoundingBoxWorld.Max.Y + 0.5f,
                (BoundingBoxWorld.Min.Z + BoundingBoxWorld.Max.Z) / 2
            );
            Vector3 proj = GraphicsDevice.Viewport.Project(
                worldTextPos,
                Camera.Instance.Projection,
                Camera.Instance.View,
                Matrix.Identity
            );

            // Projected coordinates are already in the correct space

            Vector2 baseTextPos = new Vector2(proj.X - textSize.X / 2, proj.Y);

            // Save prior GPU states
            var prevBlend = GraphicsDevice.BlendState;
            var prevDepth = GraphicsDevice.DepthStencilState;
            var prevRaster = GraphicsDevice.RasterizerState;

            var sb = GraphicsManager.Instance.Sprite;
            var pixel = GraphicsManager.Instance.Pixel;  // public 1×1 white texture

            // Draw background+border+text inside a SpriteBatchScope
            using (new SpriteBatchScope(
                sb,
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone))
            {
                // background
                var bgRect = new Rectangle(
                    (int)baseTextPos.X - 5,
                    (int)baseTextPos.Y - 5,
                    (int)textSize.X + 10,
                    (int)textSize.Y + 10
                );
                sb.Draw(pixel, bgRect, new Color(0, 0, 0, 180));

                // border
                var borderRect = new Rectangle(
                    bgRect.X - 1,
                    bgRect.Y - 1,
                    bgRect.Width + 2,
                    bgRect.Height + 2
                );
                sb.Draw(pixel, borderRect, Color.White * 0.3f);

                // text
                sb.DrawString(
                    font,
                    objectInfo,
                    baseTextPos,
                    Color.Yellow,
                    0f,
                    Vector2.Zero,
                    scaleFactor,
                    SpriteEffects.None,
                    0f);
            }

            // restore GPU states
            GraphicsDevice.BlendState = prevBlend;
            GraphicsDevice.DepthStencilState = prevDepth;
            GraphicsDevice.RasterizerState = prevRaster;
        }

        private void DrawBubbleBillboard(Bubble bubble)
        {
            // Project the bubble's 3D position to 2D screen coordinates
            Vector3 screenPos = GraphicsDevice.Viewport.Project(
                bubble.Position,
                Camera.Instance.Projection,
                Camera.Instance.View,
                Matrix.Identity
            );

            // Skip if behind camera
            if (screenPos.Z < 0f || screenPos.Z > 1f)
                return;

            // Apply render scale to bubble size only (keep original positioning)
            float scale = (bubble.Size / _bubbleTexture.Width) * Constants.RENDER_SCALE;
            var spriteBatch = GraphicsManager.Instance.Sprite;

            // Use actual depth from projection for proper depth testing
            spriteBatch.Draw(
                _bubbleTexture,
                new Vector2(screenPos.X, screenPos.Y),
                null,
                Color.White,
                0f,
                new Vector2(_bubbleTexture.Width / 2f, _bubbleTexture.Height / 2f),
                scale,
                SpriteEffects.None,
                screenPos.Z  // Use actual projected depth
            );
        }

        private static void SyncBubbleTransform(ref Bubble bubble)
        {
            // Keep a stabilized Y axis while animating X/Z offsets
            bubble.Position.Y = bubble.BasePosition.Y;
            bubble.Position.Z = bubble.BasePosition.Z + bubble.Speed * bubble.Life;
            bubble.Position.X = bubble.BasePosition.X + bubble.SwayAmplitude * (float)Math.Sin(bubble.Life * bubble.SwayFrequency + bubble.SwayPhase);
        }

        private Bubble CreateBubble(bool randomizeLife = false)
        {
            Bubble b = new Bubble();
            float range = 100f; // Area in which bubbles can spawn
            float x = (float)(_rnd.NextDouble() * range - range / 2) + Position.X;
            float y = (float)(_rnd.NextDouble() * range - range / 2) + Position.Y;
            float z = Position.Z; // Start at base Z level

            // Set base position and initial position
            b.BasePosition = new Vector3(x, y, z);
            b.Position = b.BasePosition;

            b.Speed = MathHelper.Lerp(MIN_SPEED, MAX_SPEED, (float)_rnd.NextDouble());
            b.Size = MathHelper.Lerp(MIN_SIZE, MAX_SIZE, (float)_rnd.NextDouble());
            b.MaxLife = MathHelper.Lerp(MIN_LIFE, MAX_LIFE, (float)_rnd.NextDouble());
            b.Life = randomizeLife ? (float)_rnd.NextDouble() * b.MaxLife : 0f;

            // Set random oscillation parameters
            b.SwayAmplitude = MathHelper.Lerp(5f, 20f, (float)_rnd.NextDouble());
            b.SwayFrequency = MathHelper.Lerp(1f, 2f, (float)_rnd.NextDouble());
            b.SwayPhase = (float)_rnd.NextDouble() * MathHelper.TwoPi;

            SyncBubbleTransform(ref b);

            return b;
        }
    }
}
