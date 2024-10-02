using Client.Main.Content;
using Client.Main.Controls;
using Client.Main.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Client.Main
{
    public class MuGame : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private bool _loaded = false;

        public GameControl ActiveScene;

        public MuGame()
        {
            _graphics = new GraphicsDeviceManager(this);
            _graphics.PreferredBackBufferWidth = 1280;
            _graphics.PreferredBackBufferHeight = 768;
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            ActiveScene = new GameScene();
        }

        protected override void Initialize()
        {
            base.Initialize();
        }

        protected override async void LoadContent()
        {
            BMDLoader.Instance.SetGraphicsDevice(GraphicsDevice);
            TextureLoader.Instance.SetGraphicsDevice(GraphicsDevice);

            await ActiveScene?.Load(GraphicsDevice);
            _loaded = true;
        }

        protected override void Update(GameTime gameTime)
        {
            if (!_loaded) return;
            FPSCounter.Instance.CalcFPS(gameTime);
            ActiveScene?.Update(gameTime);
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            if (_loaded) ActiveScene?.Draw(gameTime);

            base.Draw(gameTime);
        }
    }
}
