using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Models;
using Client.Main.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Client.Main
{
    public class MuGame : Game
    {
        // Singleton instance of the game
        public static MuGame Instance { get; private set; }

        // Global random instance
        public static Random Random { get; } = new Random();

        private readonly GraphicsDeviceManager _graphics;

        // Public properties and objects used globally
        public BaseScene ActiveScene { get; private set; }

        public int Width => _graphics.PreferredBackBufferWidth;
        public int Height => _graphics.PreferredBackBufferHeight;

        // Mouse and keyboard states
        public MouseState PrevMouseState { get; private set; }
        public MouseState Mouse { get; private set; }
        public KeyboardState PrevKeyboard { get; private set; }
        public KeyboardState Keyboard { get; private set; }

        public Ray MouseRay { get; private set; }

        public GameTime GameTime { get; private set; }

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
            ChangeScene(typeof(T));
        }

        public void ChangeScene(Type sceneType)
        {
            ActiveScene?.Dispose();
            ActiveScene = (BaseScene)Activator.CreateInstance(sceneType);
            Task.Run(() => ActiveScene.Initialize()).ConfigureAwait(false);
        }

        protected override void Initialize()
        {
            IsMouseVisible = false;
            base.Initialize();
        }

        protected override void LoadContent()
        {
            GraphicsManager.Instance.Init(GraphicsDevice, Content);
            ChangeScene(Constants.ENTRY_SCENE);
        }

        protected override void UnloadContent()
        {
            base.UnloadContent();
            GraphicsManager.Instance.Dispose();
        }

        protected override void Update(GameTime gameTime)
        {
            try
            {
                GameTime = gameTime;
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
            if (Keyboard.IsKeyDown(Keys.LeftShift))
            {
                if (PrevKeyboard.IsKeyDown(Keys.F1) && Keyboard.IsKeyUp(Keys.F1))
                    GraphicsManager.Instance.IsAlphaRGBEnabled = !GraphicsManager.Instance.IsAlphaRGBEnabled;
                else if (PrevKeyboard.IsKeyDown(Keys.F2) && Keyboard.IsKeyUp(Keys.F2))
                    GraphicsManager.Instance.IsFXAAEnabled = !GraphicsManager.Instance.IsFXAAEnabled;
            }
        }

        private void UpdateInputInfo(GameTime gameTime)
        {
            var mouseState = Microsoft.Xna.Framework.Input.Mouse.GetState();
            var windowBounds = Window.ClientBounds;

            PrevMouseState = Mouse;
            PrevKeyboard = Keyboard;

            // Check if the mouse is within the window bounds
            var absoluteMousePosition = new Point(mouseState.X + windowBounds.X, mouseState.Y + windowBounds.Y);
            if (IsActive && windowBounds.Contains(absoluteMousePosition))
            {
                Mouse = mouseState;
                Keyboard = Microsoft.Xna.Framework.Input.Keyboard.GetState();
            }
            else
            {
                Mouse = PrevMouseState.X != 0 || PrevMouseState.Y != 0
                    ? new MouseState(PrevMouseState.X, PrevMouseState.Y, PrevMouseState.ScrollWheelValue, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, PrevMouseState.HorizontalScrollWheelValue)
                    : new MouseState(0, 0, mouseState.ScrollWheelValue, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, mouseState.HorizontalScrollWheelValue);
                Keyboard = new KeyboardState();
            }

            if (PrevMouseState.Position != Mouse.Position)
                UpdateMouseRay();
        }

        private void UpdateMouseRay()
        {
            Vector2 mousePosition = Mouse.Position.ToVector2();
            Vector3 farSource = new Vector3(mousePosition, 1f);
            Vector3 farPoint = GraphicsDevice.Viewport.Unproject(
                farSource,
                Camera.Instance.Projection,
                Camera.Instance.View,
                Matrix.Identity);

            Vector3 nearPoint = Camera.Instance.Position;
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
            GraphicsDevice.SetRenderTarget(GraphicsManager.Instance.MainRenderTarget);
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
            RenderTarget2D sourceTarget = GraphicsManager.Instance.MainRenderTarget;
            RenderTarget2D destTarget = GraphicsManager.Instance.TempTarget1;

            if (GraphicsManager.Instance.IsAlphaRGBEnabled && GraphicsManager.Instance.AlphaRGBEffect != null)
            {
                ApplyEffect(GraphicsManager.Instance.AlphaRGBEffect, sourceTarget, destTarget);
                GraphicsManager.Instance.SwapTargets(ref sourceTarget, ref destTarget);
            }

            if (GraphicsManager.Instance.IsFXAAEnabled && GraphicsManager.Instance.FXAAEffect != null)
            {
                ApplyEffect(GraphicsManager.Instance.FXAAEffect, sourceTarget, destTarget);
                GraphicsManager.Instance.SwapTargets(ref sourceTarget, ref destTarget);
            }

            DrawFinalImageToScreen(sourceTarget);
        }

        private void ApplyEffect(Effect effect, RenderTarget2D source, RenderTarget2D destination)
        {
            GraphicsDevice.SetRenderTarget(destination);
            GraphicsDevice.Clear(Color.Transparent);

            if (effect == GraphicsManager.Instance.FXAAEffect)
            {
                effect.Parameters["Resolution"]?.SetValue(new Vector2(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height));
            }
            else if (effect == GraphicsManager.Instance.AlphaRGBEffect)
            {
                Matrix worldViewProjection = Matrix.CreateOrthographicOffCenter(0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height, 0, 0, 1);
                effect.Parameters["WorldViewProjection"]?.SetValue(worldViewProjection);
            }

            GraphicsManager.Instance.Sprite.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, effect);
            GraphicsManager.Instance.Sprite.Draw(source, GraphicsDevice.Viewport.Bounds, Color.White);
            GraphicsManager.Instance.Sprite.End();

            // Deactivate render target after each effect
            GraphicsDevice.SetRenderTarget(null);
        }

        private void DrawFinalImageToScreen(RenderTarget2D sourceTarget)
        {
            GraphicsDevice.Clear(Color.Black);
            GraphicsManager.Instance.Sprite.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            GraphicsManager.Instance.Sprite.Draw(sourceTarget, GraphicsDevice.Viewport.Bounds, Color.White);
            GraphicsManager.Instance.Sprite.End();
        }
    }
}
