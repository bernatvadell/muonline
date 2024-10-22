using Client.Main.Content;
using Client.Main.Controls;
using Client.Main.Models;
using Client.Main.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Diagnostics;

namespace Client.Main
{
    public class MuGame : Game
    {
        // Singleton instance of the game
        public static MuGame Instance { get; private set; }

        // Global random instance
        public static Random Random { get; } = new Random();

        private GraphicsDeviceManager _graphics;
        private bool _isFXAAEnabled = false;
        private bool _isAlphaRGBEnabled = true;
        private KeyboardState _previousKeyboardState;

        private RenderTarget2D _mainRenderTarget;
        private RenderTarget2D _tempTarget1;
        private RenderTarget2D _tempTarget2;

        // Public properties and objects used globally
        public BaseScene ActiveScene { get; private set; }
        public SpriteBatch SpriteBatch { get; private set; }
        public SpriteFont Font { get; private set; }
        public RenderTarget2D EffectRenderTarget { get; private set; }
        public Texture2D Pixel { get; private set; }
        public BlendState InverseDestinationBlend { get; private set; }
        public AlphaTestEffect AlphaTestEffect { get; private set; }
        public Effect AlphaRGBEffect { get; set; }
        public Effect FXAAEffect { get; private set; }
        public int Width => _graphics.PreferredBackBufferWidth;
        public int Height => _graphics.PreferredBackBufferHeight;

        // Mouse and keyboard states
        public MouseState Mouse { get; private set; }
        public KeyboardState Keyboard { get; private set; }
        public Ray MouseRay { get; private set; }

        // DepthStencilState to disable depth mask
        public DepthStencilState DisableDepthMask { get; } = new DepthStencilState
        {
            DepthBufferEnable = true,
            DepthBufferWriteEnable = false
        };

        public MuGame()
        {
            Instance = this;

            // Graphics settings
            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = 1280,
                PreferredBackBufferHeight = 720
            };

            InverseDestinationBlend = new BlendState
            {
                ColorSourceBlend = Blend.InverseDestinationColor,
                ColorDestinationBlend = Blend.One,
                AlphaSourceBlend = Blend.One,
                AlphaDestinationBlend = Blend.One,
                BlendFactor = Color.White
            };

            // Frame rate settings
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
            // Initialize resources needed for the game
            BMDLoader.Instance.SetGraphicsDevice(GraphicsDevice);
            TextureLoader.Instance.SetGraphicsDevice(GraphicsDevice);

            Pixel = new Texture2D(GraphicsDevice, 1, 1);
            Pixel.SetData(new[] { Color.White });

            InitializeRenderTargets();

            AlphaRGBEffect = LoadEffect("AlphaRGB");
            FXAAEffect = LoadEffect("FXAA");
            InitializeFXAAEffect();

            AlphaTestEffect = new AlphaTestEffect(GraphicsDevice)
            {
                Projection = Matrix.CreateOrthographicOffCenter(0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height, 0, 0, 1),
                View = Matrix.Identity,
                World = Matrix.Identity,
                ReferenceAlpha = (int)(255 * 0.25f)
            };

            SpriteBatch = new SpriteBatch(GraphicsDevice);
            Font = Content.Load<SpriteFont>("Arial");
            ChangeScene<GameScene>();
        }

        private Effect LoadEffect(string effectName)
        {
            try
            {
                return Content.Load<Effect>(effectName);
            }
            catch (Exception)
            {
                Console.WriteLine($"{effectName} could not be loaded!");
                return null;
            }
        }

        private void InitializeFXAAEffect()
        {
            if (FXAAEffect != null)
            {
                FXAAEffect.Parameters["Resolution"]?.SetValue(new Vector2(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height));
            }
        }

        private void InitializeRenderTargets()
        {
            PresentationParameters pp = GraphicsDevice.PresentationParameters;

            _mainRenderTarget = new RenderTarget2D(GraphicsDevice, pp.BackBufferWidth, pp.BackBufferHeight, false, pp.BackBufferFormat, DepthFormat.Depth24);
            _tempTarget1 = new RenderTarget2D(GraphicsDevice, pp.BackBufferWidth, pp.BackBufferHeight);
            _tempTarget2 = new RenderTarget2D(GraphicsDevice, pp.BackBufferWidth, pp.BackBufferHeight);

            EffectRenderTarget = new RenderTarget2D(GraphicsDevice, pp.BackBufferWidth, pp.BackBufferHeight, false, pp.BackBufferFormat, pp.DepthStencilFormat);
        }

        protected override void UnloadContent()
        {
            base.UnloadContent();
            _mainRenderTarget?.Dispose();
            _tempTarget1?.Dispose();
            _tempTarget2?.Dispose();
        }

        protected override void Update(GameTime gameTime)
        {
            try
            {
                UpdateInputInfo(gameTime);
                CheckShaderToggles();
                ActiveScene?.Update(gameTime);
                base.Update(gameTime);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
        }

        private void CheckShaderToggles()
        {
            KeyboardState currentKeyboardState = Microsoft.Xna.Framework.Input.Keyboard.GetState();

            if (currentKeyboardState.IsKeyDown(Keys.LeftShift))
            {
                ToggleEffect(ref _isAlphaRGBEnabled, Keys.F1, "AlphaRGB", currentKeyboardState);
                ToggleEffect(ref _isFXAAEnabled, Keys.F2, "FXAA", currentKeyboardState);
            }

            _previousKeyboardState = currentKeyboardState;
        }

        private void ToggleEffect(ref bool isEnabled, Keys key, string effectName, KeyboardState currentKeyboardState)
        {
            if (currentKeyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key))
            {
                isEnabled = !isEnabled;
                Console.WriteLine($"{effectName} {(isEnabled ? "Enabled" : "Disabled")}");
            }
        }

        private void UpdateInputInfo(GameTime gameTime)
        {
            var mouseState = Microsoft.Xna.Framework.Input.Mouse.GetState();
            var windowBounds = Window.ClientBounds;

            // Check if the mouse is within the window bounds
            var absoluteMousePosition = new Point(mouseState.X + windowBounds.X, mouseState.Y + windowBounds.Y);
            if (IsActive && windowBounds.Contains(absoluteMousePosition))
            {
                Mouse = mouseState;
                Keyboard = Microsoft.Xna.Framework.Input.Keyboard.GetState();
            }
            else
            {
                // Reset mouse and keyboard states if the window is not active
                Mouse = new MouseState(mouseState.X, mouseState.Y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, 0);
                Keyboard = new KeyboardState();
            }

            // Update mouse ray when the mouse moves
            if (Camera.Instance.Position != Vector3.Zero && Camera.Instance.Target != Vector3.Zero)
            {
                UpdateMouseRay();
            }
        }

        private void UpdateMouseRay()
        {
            var nearPoint = GraphicsDevice.Viewport.Unproject(new Vector3(Mouse.Position.X, Mouse.Position.Y, 0), Camera.Instance.Projection, Camera.Instance.View, Matrix.Identity);
            var farPoint = GraphicsDevice.Viewport.Unproject(new Vector3(Mouse.Position.X, Mouse.Position.Y, 1), Camera.Instance.Projection, Camera.Instance.View, Matrix.Identity);

            Vector3 direction = farPoint - nearPoint;
            direction.Normalize();

            MouseRay = new Ray(nearPoint, direction);
        }

        protected override void Draw(GameTime gameTime)
        {
            try
            {
                FPSCounter.Instance.CalcFPS(gameTime);

                DrawSceneToMainRenderTarget(gameTime);
                ApplyPostProcessingEffects();

                base.Draw(gameTime);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
        }

        private void DrawSceneToMainRenderTarget(GameTime gameTime)
        {
            GraphicsDevice.SetRenderTarget(_mainRenderTarget);
            GraphicsDevice.Clear(Color.Black);

            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.BlendState = BlendState.AlphaBlend;

            ActiveScene?.Draw(gameTime);
            ActiveScene?.DrawAfter(gameTime);
            GraphicsDevice.SetRenderTarget(null);
        }

        private void ApplyPostProcessingEffects()
        {
            RenderTarget2D sourceTarget = _mainRenderTarget;
            RenderTarget2D destTarget = _tempTarget1;

            if (_isAlphaRGBEnabled)
            {
                ApplyEffect(AlphaRGBEffect, sourceTarget, destTarget);
                SwapTargets(ref sourceTarget, ref destTarget);
            }

            if (_isFXAAEnabled && FXAAEffect != null)
            {
                ApplyEffect(FXAAEffect, sourceTarget, destTarget);
                SwapTargets(ref sourceTarget, ref destTarget);
            }

            DrawFinalImageToScreen(sourceTarget);
        }

        private void ApplyEffect(Effect effect, RenderTarget2D source, RenderTarget2D destination)
        {
            GraphicsDevice.SetRenderTarget(destination);
            GraphicsDevice.Clear(Color.Transparent);

            if (effect == FXAAEffect)
            {
                effect.Parameters["Resolution"]?.SetValue(new Vector2(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height));
            }
            else if (effect == AlphaRGBEffect)
            {
                Matrix worldViewProjection = Matrix.CreateOrthographicOffCenter(0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height, 0, 0, 1);
                effect.Parameters["WorldViewProjection"]?.SetValue(worldViewProjection);
            }

            SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, effect);
            SpriteBatch.Draw(source, GraphicsDevice.Viewport.Bounds, Color.White);
            SpriteBatch.End();

            // Deactivate render target after each effect
            GraphicsDevice.SetRenderTarget(null);
        }

        private void DrawFinalImageToScreen(RenderTarget2D sourceTarget)
        {
            GraphicsDevice.Clear(Color.Black);
            SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            SpriteBatch.Draw(sourceTarget, GraphicsDevice.Viewport.Bounds, Color.White);
            SpriteBatch.End();

            // Draw shader status text
            SpriteBatch.Begin();
            string statusText = $"FXAA: {(_isFXAAEnabled ? "ON" : "OFF")} | AlphaRGB: {(_isAlphaRGBEnabled ? "ON" : "OFF")}";
            Vector2 textSize = Font.MeasureString(statusText);
            Vector2 position = new Vector2(GraphicsDevice.Viewport.Width - textSize.X - 10, 10);
            SpriteBatch.DrawString(Font, statusText, position, Color.Yellow);
            SpriteBatch.End();
        }

        private void SwapTargets(ref RenderTarget2D source, ref RenderTarget2D destination)
        {
            source = destination;
            destination = (destination == _tempTarget1) ? _tempTarget2 : _tempTarget1;
        }
    }
}
