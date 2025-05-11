using Client.Main.Controllers;
using Client.Main.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Controls.UI.Game
{
    public class LoadingScreenControl : GameControl
    {
        private SpriteFont _font;
        private string _pendingMessage = "Loading…";
        private float _progress = 0f; // Value from 0 to 1
        private BasicEffect _basicEffect; // For drawing the progress bar

        // Progress bar constants
        private const int ProgressBarHeight = 20; // Slightly smaller for GameScene
        private const int ProgressBarMargin = 100; // Margin from screen edges
        private const int ProgressBarYOffset = 50; // Offset from the bottom of the screen

        public string Message
        {
            get => _pendingMessage;
            set => _pendingMessage = value ?? "Loading…";
        }

        public float Progress
        {
            get => _progress;
            set => _progress = MathHelper.Clamp(value, 0f, 1f);
        }

        public override async Task Load()
        {
            _font = GraphicsManager.Instance.Font;
            _basicEffect = new BasicEffect(GraphicsDevice)
            {
                VertexColorEnabled = true,
                Projection = Matrix.CreateOrthographicOffCenter(0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height, 0, 0, 1),
                View = Matrix.Identity,
                World = Matrix.Identity
            };
            await base.Load();
        }

        private VertexPositionColor[] CreateRectangleVertices(Vector2 pos, Vector2 size, Color color)
        {
            return
            [
                new VertexPositionColor(new Vector3(pos.X, pos.Y, 0), color),
                new VertexPositionColor(new Vector3(pos.X + size.X, pos.Y, 0), color),
                new VertexPositionColor(new Vector3(pos.X, pos.Y + size.Y, 0), color),
                new VertexPositionColor(new Vector3(pos.X + size.X, pos.Y + size.Y, 0), color)
            ];
        }

        private void DrawProgressBar()
        {
            if (_basicEffect == null || Progress <= 0f) return; // Don't draw if no progress or effect not ready

            var gd = GraphicsManager.Instance.GraphicsDevice;
            int barWidth = gd.Viewport.Width - (ProgressBarMargin * 2);
            int barX = ProgressBarMargin;
            int barY = gd.Viewport.Height - ProgressBarHeight - ProgressBarYOffset; // Positioned near bottom

            var bgPos = new Vector2(barX, barY);
            var bgSize = new Vector2(barWidth, ProgressBarHeight);
            var progressFillSize = new Vector2(barWidth * Progress, ProgressBarHeight);

            var bgVertices = CreateRectangleVertices(bgPos, bgSize, Color.DarkSlateGray * 0.8f);
            var progressVertices = CreateRectangleVertices(bgPos, progressFillSize, Color.ForestGreen * 0.9f);

            _basicEffect.TextureEnabled = false;
            _basicEffect.VertexColorEnabled = true;

            foreach (var pass in _basicEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, bgVertices, 0, 2);
                if (Progress > 0)
                {
                    gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, progressVertices, 0, 2);
                }
            }
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible) return;

            var gd = GraphicsManager.Instance.GraphicsDevice;
            var spriteBatch = GraphicsManager.Instance.Sprite;

            using (new SpriteBatchScope(
                spriteBatch,
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone))
            {
                // Background Dim
                spriteBatch.Draw(
                    GraphicsManager.Instance.Pixel,
                    new Rectangle(0, 0, gd.Viewport.Width, gd.Viewport.Height),
                    Color.Black * 0.75f); // Semi-transparent black background

                // Loading Message
                if (_font != null)
                {
                    var text = Message;
                    Vector2 size = _font.MeasureString(text);
                    Vector2 pos = new Vector2(
                        (gd.Viewport.Width - size.X) * 0.5f,
                        (gd.Viewport.Height - size.Y) * 0.5f - ProgressBarHeight); // Position text above progress bar

                    spriteBatch.DrawString(_font, text, pos + Vector2.One, Color.Black * 0.7f); // Shadow
                    spriteBatch.DrawString(_font, text, pos, Color.White);
                }
            }

            // Draw Progress Bar using BasicEffect (outside of SpriteBatchScope for Sprite)
            DrawProgressBar();

            // We call base.Draw(gameTime) if LoadingScreenControl itself might have child controls
            // For now, it's simple, so it might not be strictly necessary.
            // base.Draw(gameTime); 
        }
    }
}