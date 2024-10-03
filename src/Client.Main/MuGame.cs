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
        private SpriteFont _font;

        public MuGame()
        {
            _graphics = new GraphicsDeviceManager(this);
            _graphics.PreferredBackBufferWidth = 800;
            _graphics.PreferredBackBufferHeight = 600;
            _graphics.SynchronizeWithVerticalRetrace = false;
            IsFixedTimeStep = false;

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

            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _font = Content.Load<SpriteFont>("Arial");

            await ActiveScene?.Initialize(GraphicsDevice);
            _loaded = true;
        }

        protected override void Update(GameTime gameTime)
        {
            FPSCounter.Instance.CalcFPS(gameTime);

            if (_loaded) ActiveScene?.Update(gameTime);

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.BlendState = BlendState.Opaque;

            if (_loaded) ActiveScene?.Draw(gameTime);

            _spriteBatch.Begin();
            _spriteBatch.DrawString(_font, $"FPS: {(int)FPSCounter.Instance.FPS_AVG}", new Vector2(10, 10), Color.White);

            if(ActiveScene is GameScene gameScene)
            {
                _spriteBatch.DrawString(_font, $"PX: {gameScene.PositionX}, PY: {gameScene.PositionY}", new Vector2(10, 30), Color.White);
            }

            _spriteBatch.End();

            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;

            base.Draw(gameTime);
        }
    }
}
