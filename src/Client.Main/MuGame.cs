using Client.Main.Content;
using Client.Main.Controls;
using Client.Main.Scenes;
using Client.Main.Worlds;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Client.Main
{
    public class MuGame : Game
    {
        public static MuGame Instance { get; private set; }

        private GraphicsDeviceManager _graphics;
        public static Random Random { get; } = new Random();

        public BaseScene ActiveScene;
        public SpriteBatch SpriteBatch { get; private set; }
        public SpriteFont Font { get; private set; }
        public RenderTarget2D EffectRenderTarget { get; private set; }
        public BlendState InverseDestinationBlend { get; private set; }
        public Effect AlphaRGBEffect { get; set; }

        public int Width => _graphics.PreferredBackBufferWidth;
        public int Height => _graphics.PreferredBackBufferHeight;

        public Texture2D Pixel { get; private set; }
        public MouseState Mouse { get; private set; }
        public KeyboardState Keyboard { get; private set; }
        public Ray MouseRay { get; private set; }

        public MuGame()
        {
            Instance = this;

            _graphics = new GraphicsDeviceManager(this);
            _graphics.PreferredBackBufferWidth = 800;
            _graphics.PreferredBackBufferHeight = 600;

            InverseDestinationBlend = new BlendState
            {
                ColorSourceBlend = Blend.InverseDestinationColor,
                ColorDestinationBlend = Blend.One,
                AlphaSourceBlend = Blend.One,
                AlphaDestinationBlend = Blend.One,
                BlendFactor = Color.White
            };

            if (Constants.UNLIMITED_FPS)
            {
                _graphics.SynchronizeWithVerticalRetrace = false;
                IsFixedTimeStep = false;
            }

            Content.RootDirectory = "Content";
        }

        public void ChangeScene<T>() where T : BaseScene, new()
        {
            ActiveScene?.Dispose();
            ActiveScene = new T();
        }

        protected override void Initialize()
        {
            IsMouseVisible = false;
            base.Initialize();
        }
        protected override void LoadContent()
        {
            BMDLoader.Instance.SetGraphicsDevice(GraphicsDevice);
            TextureLoader.Instance.SetGraphicsDevice(GraphicsDevice);

            Pixel = new Texture2D(GraphicsDevice, 1, 1);
            Pixel.SetData(new[] { Color.White });
            EffectRenderTarget = new RenderTarget2D(GraphicsDevice, 800, 600);
            AlphaRGBEffect = Content.Load<Effect>("AlphaRGB");
            SpriteBatch = new SpriteBatch(GraphicsDevice);
            Font = Content.Load<SpriteFont>("Arial");
            ChangeScene<GameScene>();
        }

        protected override void Update(GameTime gameTime)
        {
            UpdateInputInfo(gameTime);

            ActiveScene?.Update(gameTime);
            base.Update(gameTime);
        }

        public DepthStencilState DisableDepthMask = new()
        {
            DepthBufferEnable = true,
            DepthBufferWriteEnable = false
        };

        private void UpdateInputInfo(GameTime gameTime)
        {
            var mouseState = Microsoft.Xna.Framework.Input.Mouse.GetState();
            var mousePosChanged = Mouse.X != mouseState.X || Mouse.Y != mouseState.Y;

            var windowBounds = Window.ClientBounds;

            var absoluteMousePosition = new Point(
                mouseState.X + windowBounds.X,
                mouseState.Y + windowBounds.Y
            );

            if (IsActive && windowBounds.Contains(absoluteMousePosition))
            {
                Mouse = mouseState;
                Keyboard = Microsoft.Xna.Framework.Input.Keyboard.GetState();
            }
            else
            {
                Mouse = new MouseState(mouseState.X, mouseState.Y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, 0);
                Keyboard = new KeyboardState();
            }

            if (mousePosChanged && Camera.Instance.Position != Vector3.Zero && Camera.Instance.Target != Vector3.Zero)
            {
                var nearPoint = GraphicsDevice.Viewport.Unproject(
                   new Vector3(Mouse.Position.X, Mouse.Position.Y, 0),
                   Camera.Instance.Projection,
                   Camera.Instance.View,
                   Matrix.Identity
               );

                var farPoint = GraphicsDevice.Viewport.Unproject(
                    new Vector3(Mouse.Position.X, Mouse.Position.Y, 1),
                    Camera.Instance.Projection,
                    Camera.Instance.View,
                    Matrix.Identity
                );

                Vector3 direction = farPoint - nearPoint;
                direction.Normalize();

                MouseRay = new Ray(nearPoint, direction);
            }
        }

        protected override void Draw(GameTime gameTime)
        {
            FPSCounter.Instance.CalcFPS(gameTime);

            GraphicsDevice.SetRenderTarget(EffectRenderTarget);
            GraphicsDevice.Clear(Color.Black);
            GraphicsDevice.SetRenderTarget(null);

            GraphicsDevice.Clear(Color.Black);

            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.BlendState = BlendState.Opaque;

            if (ActiveScene?.Status == GameControlStatus.Ready)
                ActiveScene?.Draw(gameTime);

            if (ActiveScene?.Status == GameControlStatus.Ready)
                ActiveScene?.DrawAfter(gameTime);

            SpriteBatch.Begin();
            SpriteBatch.DrawString(Font, $"FPS: {(int)FPSCounter.Instance.FPS_AVG}, VMX: {Mouse.Position.X}, VMY: {Mouse.Position.Y}", new Vector2(10, 10), Color.White);

            if (ActiveScene.World != null && ActiveScene.World is WalkableWorldControl walkableWorld)
            {
                SpriteBatch.DrawString(Font, $"PX: {walkableWorld.Walker.Location.X}, PY: {walkableWorld.Walker.Location.Y}", new Vector2(10, 30), Color.White);
                SpriteBatch.DrawString(Font, $"TMX: {walkableWorld.MouseTileX}, TMY: {walkableWorld.MouseTileY}", new Vector2(10, 50), Color.White);
            }
            SpriteBatch.End();

            GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;

            base.Draw(gameTime);
        }
    }
}
