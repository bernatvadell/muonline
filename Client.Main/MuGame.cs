using Client.Main.Controllers;
using Client.Main.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Client.Main.Configuration;
using Client.Main.Networking;
using System.Collections.Concurrent;
using Client.Main.Core.Client;
#if ANDROID
using Android.App;
using System.IO;
#endif

namespace Client.Main
{
    public class MuGame : Game
    {
        // Static Fields
        private static readonly ConcurrentQueue<Action> _mainThreadActions = new ConcurrentQueue<Action>();

        // Static Properties
        public static MuGame Instance { get; private set; }
        public static Random Random { get; } = new Random();
        public static IConfiguration AppConfiguration { get; private set; }
        public static ILoggerFactory AppLoggerFactory { get; private set; }
        public static MuOnlineSettings AppSettings { get; private set; }
        public static NetworkManager Network { get; private set; }

        // Instance Fields
        private readonly GraphicsDeviceManager _graphics;
        private ILogger _logger = AppLoggerFactory?.CreateLogger<MuGame>();
        private bool _networkDisposed = false;
        private float _scaleFactor;

        // Public Instance Properties
        public BaseScene ActiveScene { get; private set; }
        public int Width => _graphics.PreferredBackBufferWidth;
        public int Height => _graphics.PreferredBackBufferHeight;
        public MouseState PrevMouseState { get; private set; }
        public MouseState Mouse { get; private set; }
        public KeyboardState PrevKeyboard { get; private set; }
        public KeyboardState Keyboard { get; private set; }
        public TouchCollection PrevTouchState { get; private set; }
        public TouchCollection Touch { get; private set; }
        public Ray MouseRay { get; private set; }
        public GameTime GameTime { get; private set; }
        public DepthStencilState DisableDepthMask { get; } = new DepthStencilState
        {
            DepthBufferEnable = true,
            DepthBufferWriteEnable = false
        };

        // Constructors
        public MuGame()
        {
            Instance = this;

            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = 1280,
                PreferredBackBufferHeight = 720
            };

#if ANDROID || IOS
            _graphics.IsFullScreen = true;
            _graphics.SynchronizeWithVerticalRetrace = true;
            IsFixedTimeStep = true;
            TargetElapsedTime = TimeSpan.FromMilliseconds(16.67);
#else
            if (Constants.UNLIMITED_FPS)
            {
                _graphics.SynchronizeWithVerticalRetrace = false;
                IsFixedTimeStep = false;
                TargetElapsedTime = TimeSpan.FromMilliseconds(1);
            }
#endif
            _graphics.GraphicsProfile = GraphicsProfile.HiDef;
            _graphics.ApplyChanges();
            Content.RootDirectory = "Content";

            // Register handler for game exit (X button, Alt+F4, etc.)
            // This ensures proper cleanup and process termination.
            this.Exiting += OnGameExiting;
        }

        /// <summary>
        /// Schedules an action to be executed on the main game thread during the next Update cycle.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        public static void ScheduleOnMainThread(Action action)
        {
            if (action != null)
            {
                _mainThreadActions.Enqueue(action);
            }
        }

#if ANDROID
        private static string EnsureAndroidConfig()
        {
            var ctx = Application.Context!;
            var dst = Path.Combine(ctx.FilesDir!.AbsolutePath, "appsettings.json");

            if (!File.Exists(dst))
            {
                try
                {
                    using var src = ctx.Assets!.Open("appsettings.json");
                    using var trg = File.Create(dst);
                    src.CopyTo(trg);
                }
                catch (Exception copyEx)
                {
                    Android.Util.Log.Error("MuGame", "Cannot copy appsettings.json: " + copyEx);
                }
            }
            return dst;
        }
#endif

        private static bool ValidateSettings(MuOnlineSettings settings, ILogger logger)
        {
            // Reuse the validation logic from MuOnlineConsole's App.axaml.cs
            // (Ensure necessary using for TargetProtocolVersion is present)
            if (settings == null) { logger.LogError("❌ Failed to load config from 'MuOnlineSettings'."); return false; }
            bool isValid = true;
            if (string.IsNullOrWhiteSpace(settings.ConnectServerHost) || settings.ConnectServerPort == 0) { logger.LogError("❌ Connect Server host/port invalid."); isValid = false; }
            if (string.IsNullOrWhiteSpace(settings.ProtocolVersion) || !Enum.TryParse<TargetProtocolVersion>(settings.ProtocolVersion, true, out _)) // Case-insensitive parse
            { logger.LogError("❌ ProtocolVersion '{V}' invalid. Valid: {Vs}", settings.ProtocolVersion, string.Join(", ", Enum.GetNames<TargetProtocolVersion>())); isValid = false; }
            if (string.IsNullOrWhiteSpace(settings.ClientVersion)) { logger.LogWarning("⚠️ ClientVersion not set."); }
            if (string.IsNullOrWhiteSpace(settings.ClientSerial)) { logger.LogWarning("⚠️ ClientSerial not set."); }
            return isValid;
        }

        public static void DisposeInstance()
        {
            if (Instance != null)
            {
                Instance.Dispose();
                Instance = null;
            }
        }

        // Public Instance Methods
        public void ChangeScene<T>() where T : BaseScene, new()
        {
            if (typeof(T) == typeof(GameScene))
            {
                // This code should never be called for GameScene anymore
                // You can throw an exception or log a warning
                throw new InvalidOperationException("GameScene requires parameters. Use ChangeScene(BaseScene newScene) instead.");
            }
            _logger.LogInformation(">>> ChangeScene<{SceneType}>() called (generic)", typeof(T).Name);
            BaseScene newScene = new T(); // Creating an instance using the parameterless constructor
            ChangeSceneInternal(newScene); // Calling the helper method
        }

        // NEW method accepting a scene instance
        public void ChangeScene(BaseScene newScene)
        {
            if (newScene == null)
            {
                _logger.LogError("Attempted to change scene to a null instance.");
                throw new ArgumentNullException(nameof(newScene));
            }
            _logger.LogInformation(">>> ChangeScene(BaseScene newScene) called with scene type: {SceneType}", newScene.GetType().Name);
            ChangeSceneInternal(newScene); // Calling the helper method
        }

        // Protected Instance Methods
        protected override void Initialize()
        {
            // --- Configuration Setup ---
#if ANDROID
            string cfgPath = EnsureAndroidConfig();
            AppConfiguration = new ConfigurationBuilder()
                .SetBasePath(Path.GetDirectoryName(cfgPath)!)
                .AddJsonFile(Path.GetFileName(cfgPath), optional: false, reloadOnChange: false)
                .Build();
#else // Windows, Linux, etc.
            AppConfiguration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();
#endif

            // --- Logging Setup ---
            AppLoggerFactory = LoggerFactory.Create(builder =>
            {
                builder.ClearProviders();
                // Configure logging based on appsettings.json
                builder.AddConfiguration(AppConfiguration.GetSection("Logging"));
                // Add Console logger (can add others like Debug, File)
                builder.AddSimpleConsole(options =>
                {
                    AppConfiguration.GetSection("Logging:SimpleConsole").Bind(options);
                    options.IncludeScopes = true; // Optional: Include scopes if you use them
                });
            });

            _logger = AppLoggerFactory.CreateLogger<MuGame>();
            var bootLogger = AppLoggerFactory.CreateLogger("MuGame.Boot"); // Logger for startup

            // --- Load Settings ---
            AppSettings = AppConfiguration.GetSection("MuOnlineSettings").Get<MuOnlineSettings>();
            if (AppSettings == null || !ValidateSettings(AppSettings, bootLogger)) // Add validation
            {
                bootLogger.LogCritical("❌ Invalid application settings found in appsettings.json. Shutting down.");
                Exit(); // Stop the game if settings are invalid
                return;
            }
            bootLogger.LogInformation("✅ Configuration loaded.");

            // --- Initialize Network Manager ---
            // Needs CharacterState and ScopeManager - create basic instances for now
            // You'll likely manage these more centrally later
            var characterState = new CharacterState(AppLoggerFactory);
            var scopeManager = new ScopeManager(AppLoggerFactory, characterState);
            Network = new NetworkManager(AppLoggerFactory, AppSettings, characterState, scopeManager);
            bootLogger.LogInformation("✅ Network Manager initialized.");

            IsMouseVisible = false; // Keep this if you want a custom cursor
            base.Initialize();

            // Base aspect ratio (e.g., 16:9)
            float baseAspectRatio = 16f / 9f;
            float currentAspectRatio = (float)_graphics.PreferredBackBufferWidth / _graphics.PreferredBackBufferHeight;
            _scaleFactor = currentAspectRatio / baseAspectRatio;

            _logger?.LogDebug($"Scale Factor: {_scaleFactor}");
        }

        protected override void UnloadContent()
        {
            base.UnloadContent();
            GraphicsManager.Instance.Dispose();
            DisposeNetworkSafely();   // ← only place called in UnloadContent
            AppLoggerFactory?.Dispose(); // AppLoggerFactory can be null if Initialize failed early
        }

        protected override void Update(GameTime gameTime)
        {
            // --- Process Main Thread Actions ---
            int actionCount = 0;
            var queueLogger = AppLoggerFactory?.CreateLogger("MuGame.MainThreadQueue"); // logger for the queue

            while (_mainThreadActions.TryDequeue(out Action action))
            {
                actionCount++;
                //queueLogger?.LogTrace("Dequeued action #{Count}. Attempting execution...", actionCount);
                try
                {
                    action.Invoke();
                    //queueLogger?.LogTrace("Action #{Count} executed successfully.", actionCount);
                }
                catch (Exception ex)
                {
                    //queueLogger?.LogError(ex, "Error executing action #{Count} scheduled on main thread.", actionCount);
                }
            }
            // **** ADD LOGGING ****
            // if (actionCount > 0) queueLogger?.LogDebug("Processed {Count} actions from queue this frame.", actionCount);
            // **** END ADD LOGGING ****

            try // outer try
            {
                GameTime = gameTime;
                UpdateInputInfo(gameTime);
                CheckShaderToggles();

                try // inner try for ActiveScene.Update
                {
                    ActiveScene?.Update(gameTime);
                }
                catch (Exception sceneEx)
                {
                    _logger?.LogCritical(sceneEx, "Unhandled exception in ActiveScene.Update ({SceneType})!", ActiveScene?.GetType().Name ?? "null");
                    // Consider stopping the game or returning to a safe scene
                    // Exit();
                }

                try // inner try for base.Update
                {
                    base.Update(gameTime);
                }
                catch (Exception baseEx)
                {
                    _logger?.LogCritical(baseEx, "Unhandled exception in base.Update!");
                    // Consider stopping the game
                    // Exit();
                }
            }
            catch (Exception e) // Catch other unexpected errors in Update
            {
                _logger?.LogCritical(e, "Unhandled exception in MuGame.Update loop (outside scene/base update)!");
                // Exit();
            }
        }

        protected override void LoadContent()
        {
            GraphicsManager.Instance.Init(GraphicsDevice, Content);

            // --- START NETWORK CONNECTION ---
            // Start connecting to the Connect Server when the game loads
            // We do this *after* GraphicsManager is init because some UI might depend on it
            if (Network != null) // Ensure network was initialized
            {
                // Start connection without blocking LoadContent
                _ = Network.ConnectToConnectServerAsync();
            }
            else
            {
                _logger?.LogCritical("Network Manager is null during LoadContent. Cannot connect.");
                // Potentially exit or handle error
            }
            // --- END NETWORK CONNECTION ---

            // Load the initial scene (e.g., LoginScene) AFTER network connection starts
            ChangeSceneAsync(Constants.ENTRY_SCENE).ContinueWith(t =>
            {
                if (t.Exception != null)
                    _logger?.LogDebug($"Error changing scene: {t.Exception}");
            });
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
                _logger?.LogDebug(e, "Exception in MuGame");
            }
            finally
            {
                // Ensure that no render target is active to avoid the Present error
                GraphicsDevice.SetRenderTarget(null);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeNetworkSafely();   // ← won't be called a second time
                AppLoggerFactory?.Dispose();
            }
            base.Dispose(disposing);
            Instance = null;
        }

        // Private Instance Methods
        // Private helper method for changing scenes
        private async void ChangeSceneInternal(BaseScene newScene)
        {
            _logger.LogInformation("--- ChangeSceneInternal: Starting scene change to {SceneType}...", newScene.GetType().Name);

            // Optional: Show loading screen before disposing the old scene
            // ShowLoadingScreen();

            // Dispose the old scene
            if (ActiveScene != null)
            {
                _logger.LogDebug("--- ChangeSceneInternal: Disposing previous scene ({SceneType})...", ActiveScene.GetType().Name);
                ActiveScene.Dispose();
                _logger.LogDebug("--- ChangeSceneInternal: Previous scene disposed.");
            }
            ActiveScene = null; // Ensure there's no reference while loading the new one

            // Set the new scene
            ActiveScene = newScene;
            _logger.LogDebug("--- ChangeSceneInternal: ActiveScene set to {SceneType}.", ActiveScene.GetType().Name);

            // Initialize/Load the new scene (assuming Initialize/Load is asynchronous)
            try
            {
                _logger.LogDebug("--- ChangeSceneInternal: Calling Initialize() for {SceneType}...", ActiveScene.GetType().Name);
                // Ensure the Initialize method exists and is appropriate,
                // or use await ActiveScene.Load() if that's how your system works.
                await ActiveScene.Initialize();
                _logger.LogDebug("--- ChangeSceneInternal: Initialize() completed for {SceneType}.", ActiveScene.GetType().Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "!!! ChangeSceneInternal: Exception during Initialize() for {SceneType}.", ActiveScene.GetType().Name);
                // Handle error - maybe return to LoginScene?
                // ActiveScene = new LoginScene(); // Emergency return
                // await ActiveScene.Initialize();
                return; // End scene change after error
            }

            // Optional: Hide loading screen after the new scene is loaded
            // HideLoadingScreen();
            _logger.LogInformation("<<< ChangeSceneInternal: Scene change to {SceneType} complete.", ActiveScene.GetType().Name);
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

            if (PrevKeyboard.IsKeyDown(Keys.F8) && Keyboard.IsKeyUp(Keys.F8))
                Constants.DRAW_BOUNDING_BOXES = !Constants.DRAW_BOUNDING_BOXES;
            else if (PrevKeyboard.IsKeyDown(Keys.F9) && Keyboard.IsKeyUp(Keys.F9))
                Constants.DRAW_BOUNDING_BOXES_INTERACTIVES = !Constants.DRAW_BOUNDING_BOXES_INTERACTIVES;
        }

        private async Task ChangeSceneAsync(Type sceneType)
        {
            ActiveScene?.Dispose();
            ActiveScene = (BaseScene)Activator.CreateInstance(sceneType);
            await ActiveScene.Initialize();
        }

        private void UpdateInputInfo(GameTime gameTime)
        {
            var mouseState = Microsoft.Xna.Framework.Input.Mouse.GetState();
            var touchState = TouchPanel.GetState();
            var windowBounds = Window.ClientBounds;

            PrevMouseState = Mouse;
            PrevKeyboard = Keyboard;
            PrevTouchState = Touch;

            var absoluteMousePosition = new Point(mouseState.X + windowBounds.X, mouseState.Y + windowBounds.Y);
            if (!IsActive || !windowBounds.Contains(absoluteMousePosition))
            {
                Mouse = PrevMouseState;
                Keyboard = new KeyboardState();
                Touch = PrevTouchState;
            }
            else
            {
                Mouse = mouseState;
                Keyboard = Microsoft.Xna.Framework.Input.Keyboard.GetState();
                Touch = touchState;
            }

            if (PrevMouseState.Position != Mouse.Position)
                UpdateMouseRay();

            if (PrevTouchState.Count != Touch.Count)
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

        private void DrawSceneToMainRenderTarget(GameTime gameTime)
        {
            GraphicsDevice.SetRenderTarget(GraphicsManager.Instance.MainRenderTarget);
            GraphicsDevice.Clear(Color.Black);

            // Ensure correct culling for 3D models
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
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

        /// <summary>
        /// Synchronously disposes <see cref="Network"/> exactly once,
        /// swallowing <see cref="ObjectDisposedException"/> which occurs,
        /// if the CancellationTokenSource inside NetworkManager was already disposed.
        /// </summary>
        private void DisposeNetworkSafely()
        {
            if (_networkDisposed || Network == null)
                return;

            _networkDisposed = true;

            try
            {
                // Fire-and-forget async dispose to avoid deadlock on shutdown.
                // Blocking here (e.g., .GetAwaiter().GetResult()) can cause a hang if async code needs the main thread.
                _ = Network.DisposeAsync();
            }
            catch (ObjectDisposedException)
            {
                // Ignore – NetworkManager already cleaned up earlier
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unexpected error while disposing NetworkManager.");
            }
        }

        /// <summary>
        /// Handles the Game.Exiting event to ensure proper cleanup and process termination.
        /// </summary>
        private void OnGameExiting(object sender, EventArgs e)
        {
            // Dispose the game instance and all resources
            DisposeInstance();
            // Force process exit to avoid lingering background process
            Environment.Exit(0);
        }
    }
}