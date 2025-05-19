using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls.UI.Game; // Assuming ExtendedUIControl might be here or in Models
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Controls.UI
{
    public class TextureControl : ExtendedUIControl // Assuming ExtendedUIControl is your base for UI elements
    {
        public Texture2D Texture { get; protected set; }
        private string _texturePath;
        public Rectangle TextureRectangle { get; set; }
        public virtual Rectangle SourceRectangle => TextureRectangle;

        public new float Alpha { get; set; } = 1f;
        public string TexturePath { get => _texturePath; set { if (_texturePath != value) { _texturePath = value; OnChangeTexturePath(); } } }
        public BlendState BlendState { get; set; } = BlendState.AlphaBlend;

        public override async Task Initialize()
        {
            // Prepare texture only if path is valid
            if (!string.IsNullOrEmpty(TexturePath)) // Check added in SpriteControl, but good to have here too for direct TextureControl usage
            {
                await TextureLoader.Instance.Prepare(TexturePath);
            }
            await base.Initialize();
        }

        public override async Task Load()
        {
            await LoadTexture();
            await base.Load();
        }

        public override void Draw(GameTime gameTime)
        {
            if (Status != GameControlStatus.Ready || !Visible || Texture == null)
                return;

            GraphicsManager.Instance.Sprite.Draw(
                Texture,
                DisplayRectangle,
                SourceRectangle,
                Color.White * Alpha);

            base.Draw(gameTime);
        }

        private void OnChangeTexturePath()
        {
            if (Status == GameControlStatus.Ready || Status == GameControlStatus.Initializing)
            {
                Task.Run(async () => await LoadTexture());
            }
        }

        protected virtual async Task LoadTexture()
        {
            if (string.IsNullOrEmpty(TexturePath))
            {
                Texture = null; // Explicitly ensure Texture is null
                if (AutoViewSize) ViewSize = Point.Zero; // Reset ViewSize if auto and no texture
                // Debug.WriteLineIf(TexturePath == null || TexturePath == "", $"TextureControl: TexturePath is null or empty for {this.GetType().Name}. Skipping texture load.");
                return; // Do not attempt to load if path is invalid
            }

            await TextureLoader.Instance.Prepare(TexturePath);
            Texture = TextureLoader.Instance.GetTexture2D(TexturePath);

            if (Texture == null)
            {
                if (AutoViewSize) ViewSize = Point.Zero;
                System.Diagnostics.Debug.WriteLine($"Warning: Failed to load texture for TextureControl: {TexturePath}");
                return;
            }

            // If AutoViewSize is true, set ViewSize to texture dimensions.
            // If TextureRectangle is not set, default to full texture.
            if (AutoViewSize)
            {
                ViewSize = new Point(Texture.Width, Texture.Height);
            }

            if (TextureRectangle == Rectangle.Empty)
            {
                TextureRectangle = new Rectangle(0, 0, Texture.Width, Texture.Height);
            }
        }
    }
}