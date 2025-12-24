using Client.Main.Controllers;
using Client.Main.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Client.Main.Configuration;
using Client.Main.Networking;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Client.Main.Core.Client;
using Client.Main.Content;
using Client.Main.Graphics;
#if ANDROID
using Android.App;
#endif

namespace Client.Main
{
    public class MuGame : Game
    {
        private const string LocalSettingsFileName = "appsettings.local.json";
        // Static Fields
        private static Controllers.TaskScheduler _taskScheduler;
        private static readonly ConcurrentQueue<IMainThreadAction> _mainThreadActions = new ConcurrentQueue<IMainThreadAction>();

        private interface IMainThreadAction
        {
            void Invoke();
        }

        private sealed class QueuedAction : IMainThreadAction
        {
            private readonly Action _action;

            public QueuedAction(Action action) => _action = action ?? throw new ArgumentNullException(nameof(action));

            public void Invoke() => _action();
        }

        private sealed class QueuedAction<TState> : IMainThreadAction
        {
            private readonly Action<TState> _action;
            private readonly TState _state;

            public QueuedAction(Action<TState> action, TState state)
            {
                _action = action ?? throw new ArgumentNullException(nameof(action));
                _state = state;
            }

            public void Invoke() => _action(_state);
        }

        // Static Properties
        public static MuGame Instance { get; private set; }
        public static Random Random { get; } = new Random();
        public static IConfiguration AppConfiguration { get; private set; }
        public static ILoggerFactory AppLoggerFactory { get; private set; }
        public static MuOnlineSettings AppSettings { get; private set; }
        public static NetworkManager Network { get; private set; }
        public static string ConfigDirectory { get; private set; }
        public static string LocalSettingsPath => Path.Combine(ConfigDirectory ?? AppContext.BaseDirectory, LocalSettingsFileName);
        public static Controllers.TaskScheduler TaskScheduler => _taskScheduler;
        public static int FrameIndex { get; private set; }

        // Instance Fields
        private readonly GraphicsDeviceManager _graphics;
        private ILogger _logger = AppLoggerFactory?.CreateLogger<MuGame>();
        private bool _networkDisposed = false;
        private float _scaleFactor;
        private bool _effectCacheValid = false;
        private Point _lastEffectTargetSize;
        private Matrix _cachedEffectOrtho;
        private Vector2 _cachedEffectResolution;

        // Public Instance Properties
        public BaseScene ActiveScene { get; private set; }
        public int Width => _graphics.PreferredBackBufferWidth;
        public int Height => _graphics.PreferredBackBufferHeight;
        public GameWindow GameWindow => this.Window;
        public MouseState PrevMouseState { get; private set; }
        public MouseState Mouse { get; set; }
        public MouseState PrevUiMouseState { get; private set; }
        public MouseState UiMouseState { get; private set; }
        public Point UiMousePosition { get; private set; }
        public KeyboardState PrevKeyboard { get; private set; }
        public KeyboardState Keyboard { get; private set; }
        public TouchCollection PrevTouchState { get; private set; }
        public TouchCollection Touch { get; private set; }
        public Point UiTouchPosition { get; private set; }
        public Ray MouseRay { get; private set; }
        /// <summary>
        /// Mouse position converted to back buffer space (for 3D raycasting).
        /// In fullscreen borderless mode, window size may differ from back buffer size.
        /// </summary>
        public Vector2 MouseInBackBuffer { get; private set; }
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
                PreferredBackBufferHeight = 720,
                PreferMultiSampling = Constants.MSAA_ENABLED,
                HardwareModeSwitch = false // Required for dynamic resolution changes at runtime
            };

#if ANDROID
            _graphics.IsFullScreen = true;
            _graphics.SupportedOrientations = DisplayOrientation.LandscapeLeft | DisplayOrientation.LandscapeRight;
            _graphics.SynchronizeWithVerticalRetrace = true;
            // Screen size will be configured in Initialize() using GraphicsAdapter
            IsFixedTimeStep = false;
            TargetElapsedTime = TimeSpan.FromMilliseconds(16.67);
#elif IOS
            _graphics.IsFullScreen = true;
            _graphics.SynchronizeWithVerticalRetrace = true;
            IsFixedTimeStep = false;
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
            _graphics.PreferredBackBufferFormat = SurfaceFormat.Color;
            _graphics.ApplyChanges();

            // UiScaler configuration moved to Initialize() - Window.ClientBounds may be invalid in constructor

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
                _mainThreadActions.Enqueue(new QueuedAction(action));
            }
        }

        /// <summary>
        /// Schedules a stateful action to be executed on the main game thread without creating a closure.
        /// </summary>
        public static void ScheduleOnMainThread<TState>(Action<TState> action, TState state)
        {
            if (action != null)
            {
                _mainThreadActions.Enqueue(new QueuedAction<TState>(action, state));
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
            if (settings.Graphics == null)
            {
                logger.LogError("❌ Graphics settings missing from configuration.");
                isValid = false;
            }
            else
            {
                if (settings.Graphics.Width <= 0 || settings.Graphics.Height <= 0)
                {
                    logger.LogError("❌ Graphics resolution {W}x{H} invalid.", settings.Graphics.Width, settings.Graphics.Height);
                    isValid = false;
                }

                if (settings.Graphics.UiVirtualWidth <= 0 || settings.Graphics.UiVirtualHeight <= 0)
                {
                    logger.LogError("❌ UI virtual resolution {W}x{H} invalid.", settings.Graphics.UiVirtualWidth, settings.Graphics.UiVirtualHeight);
                    isValid = false;
                }
            }
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
        /// <summary>
        /// Gets the actual screen size using GraphicsAdapter for mobile platforms.
        /// </summary>
        private Point GetActualScreenSize()
        {
#if ANDROID || IOS
            // Use GraphicsAdapter to get actual display size
            var adapter = base.GraphicsDevice?.Adapter ?? GraphicsAdapter.DefaultAdapter;
            if (adapter != null)
            {
                var displayMode = adapter.CurrentDisplayMode;
                return new Point(displayMode.Width, displayMode.Height);
            }

            // Fallback to configured size
            return new Point(_graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight);
#else
            return new Point(_graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight);
#endif
        }

        protected override void Initialize()
        {
#if ANDROID
            Android.Util.Log.Info("MuGame", "=== Initialize() START ===");
#endif
            // --- Configuration Setup ---
#if ANDROID
            Android.Util.Log.Info("MuGame", "About to call EnsureAndroidConfig()");
            string cfgPath = EnsureAndroidConfig();
            Android.Util.Log.Info("MuGame", $"Config path: {cfgPath}");
            ConfigDirectory = Path.GetDirectoryName(cfgPath)!;
            AppConfiguration = new ConfigurationBuilder()
                .SetBasePath(ConfigDirectory)
                .AddJsonFile(Path.GetFileName(cfgPath), optional: false, reloadOnChange: true)
                .AddJsonFile(LocalSettingsFileName, optional: true, reloadOnChange: true)
                .Build();
            Android.Util.Log.Info("MuGame", "AppConfiguration created");
#else // Windows, Linux, etc.
            ConfigDirectory = AppContext.BaseDirectory;
            AppConfiguration = new ConfigurationBuilder()
                .SetBasePath(ConfigDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile(LocalSettingsFileName, optional: true, reloadOnChange: true)
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

#if ANDROID
                // Add file logger for Android - logs to Downloads folder
                builder.AddProvider(new Client.Main.Platform.Android.AndroidFileLoggerProvider());
#endif
            });

            _logger = AppLoggerFactory.CreateLogger<MuGame>();
            var bootLogger = AppLoggerFactory.CreateLogger("MuGame.Boot"); // Logger for startup

            // --- Load Settings ---
            AppSettings = AppConfiguration.GetSection("MuOnlineSettings").Get<MuOnlineSettings>();
            if (AppSettings == null || !ValidateSettings(AppSettings, bootLogger)) // Add validation
            {
                bootLogger.LogCritical("❌ Invalid application settings found in appsettings.json. Shutting down.");
#if !IOS
                Exit(); // Stop the game if settings are invalid
#endif
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

            // Initialize TaskScheduler
            _taskScheduler = new Controllers.TaskScheduler(AppLoggerFactory);
            bootLogger.LogInformation("✅ TaskScheduler initialized.");

            IsMouseVisible = false; // Keep this if you want a custom cursor

            ApplyGraphicsConfiguration(AppSettings.Graphics);
            GraphicsQualityManager.ApplyFromSettings(AppSettings.Graphics, GraphicsDevice?.Adapter ?? GraphicsAdapter.DefaultAdapter, _logger);
            ApplyGraphicsOptions();

            // Configure screen size for mobile platforms AFTER graphics device is ready
#if ANDROID
            var screenSize = GetActualScreenSize();
            _graphics.PreferredBackBufferWidth = screenSize.X;
            _graphics.PreferredBackBufferHeight = screenSize.Y;
            _graphics.ApplyChanges();

            UiScaler.Configure(
                screenSize.X,
                screenSize.Y,
                Constants.BASE_UI_WIDTH,
                Constants.BASE_UI_HEIGHT,
                ScaleMode.Stretch);

            bootLogger.LogInformation("✅ Android UiScaler configured: Screen={Width}x{Height}, Virtual={VWidth}x{VHeight}, ScaleX={ScaleX:F4}, ScaleY={ScaleY:F4}",
                screenSize.X, screenSize.Y,
                Constants.BASE_UI_WIDTH, Constants.BASE_UI_HEIGHT,
                UiScaler.ScaleX, UiScaler.ScaleY);
#elif IOS
            var screenSize = GetActualScreenSize();
            _graphics.PreferredBackBufferWidth = screenSize.X;
            _graphics.PreferredBackBufferHeight = screenSize.Y;
            _graphics.ApplyChanges();

            UiScaler.Configure(
                screenSize.X,
                screenSize.Y,
                Constants.BASE_UI_WIDTH,
                Constants.BASE_UI_HEIGHT,
                ScaleMode.Stretch);

            bootLogger.LogInformation("✅ iOS UiScaler configured: Screen={Width}x{Height}, Virtual={VWidth}x{VHeight}, ScaleX={ScaleX:F4}, ScaleY={ScaleY:F4}",
                screenSize.X, screenSize.Y,
                Constants.BASE_UI_WIDTH, Constants.BASE_UI_HEIGHT,
                UiScaler.ScaleX, UiScaler.ScaleY);
#else
            UiScaler.Configure(
                _graphics.PreferredBackBufferWidth,
                _graphics.PreferredBackBufferHeight,
                Constants.BASE_UI_WIDTH,
                Constants.BASE_UI_HEIGHT,
                ScaleMode.Uniform);
#endif

            _logger?.LogDebug("UI scale factor set to {Scale:F3} (virtual {VirtualWidth}x{VirtualHeight} -> actual {ActualWidth}x{ActualHeight}).",
                UiScaler.Scale,
                UiScaler.VirtualSize.X,
                UiScaler.VirtualSize.Y,
                UiScaler.ActualSize.X,
                UiScaler.ActualSize.Y);

            base.Initialize();
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
            // --- Process Main Thread Actions via TaskScheduler ---
            while (_mainThreadActions.TryDequeue(out var action))
            {
                _taskScheduler.QueueTask(action.Invoke, Controllers.TaskScheduler.Priority.Normal);
            }

            // Process prioritized tasks using the task scheduler
            _taskScheduler.ProcessFrame();

            try // outer try
            {
                GameTime = gameTime;
                FrameIndex++;
                UpdateInputInfo(gameTime);
                CheckShaderToggles();
                SunCycleManager.Update();

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

            // --- LOAD PLAYER IDLE POSE DATA ---
            // Load bone transformations from Player.bmd for inventory item rendering
            // Equipment items (armor, boots, etc.) use LinkParentAnimation and need player bone poses
            _ = PlayerIdlePoseProvider.EnsureLoadedAsync().ContinueWith(t =>
            {
                if (t.Exception != null)
                    _logger?.LogWarning(t.Exception, "Failed to load player idle pose data for inventory rendering");
                else
                    _logger?.LogDebug("Player idle pose data loaded successfully for inventory");
            });
            // --- END PLAYER IDLE POSE DATA ---

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
                // Initialize frame-based optimizations
                DynamicBufferPool.BeginFrame(FrameIndex);
                BMDLoader.Instance.BeginFrame();
                
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
                _logger.LogDebug("--- ChangeSceneInternal: Starting initialization for {SceneType}...", ActiveScene.GetType().Name);

                if (ActiveScene is BaseScene baseScene)
                {
                    await baseScene.InitializeWithProgressReporting(null);
                }
                else
                {
                    await ActiveScene.Initialize();
                }

                _logger.LogDebug("--- ChangeSceneInternal: Initialization completed for {SceneType}.", ActiveScene.GetType().Name);
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

        public void ApplyGraphicsOptions()
        {
#if !(ANDROID || IOS)
            _graphics.SynchronizeWithVerticalRetrace = !Constants.UNLIMITED_FPS && !Constants.DISABLE_VSYNC;
            IsFixedTimeStep = !Constants.UNLIMITED_FPS;
            TargetElapsedTime = Constants.UNLIMITED_FPS
                ? TimeSpan.FromMilliseconds(1)
                : TimeSpan.FromSeconds(1.0 / 60.0);
#endif
            _graphics.PreferMultiSampling = Constants.MSAA_ENABLED;
            _graphics.ApplyChanges();
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

            PrevMouseState = Mouse;
            PrevUiMouseState = UiMouseState;
            PrevKeyboard = Keyboard;
            PrevTouchState = Touch;

            // --- PHYSICAL DEVICES ---
            // Always update keyboard and touch state
            Keyboard = Microsoft.Xna.Framework.Input.Keyboard.GetState();
            Touch = touchState;

            // Get back buffer and window dimensions
            int backBufferWidth = GraphicsDevice.PresentationParameters.BackBufferWidth;
            int backBufferHeight = GraphicsDevice.PresentationParameters.BackBufferHeight;

            // In borderless fullscreen, Window.ClientBounds may return screen size while
            // back buffer is smaller. We need to check if mouse needs scaling.
            // Use Window.ClientBounds for the actual area where mouse events are reported.
            var clientBounds = Window.ClientBounds;
            int inputAreaWidth = clientBounds.Width > 0 ? clientBounds.Width : backBufferWidth;
            int inputAreaHeight = clientBounds.Height > 0 ? clientBounds.Height : backBufferHeight;

            // Calculate scale factors for converting input coords to back buffer coords
            float scaleX = (float)backBufferWidth / inputAreaWidth;
            float scaleY = (float)backBufferHeight / inputAreaHeight;

            // Convert mouse position from window space to back buffer space
            float mouseBackBufferX = mouseState.X * scaleX;
            float mouseBackBufferY = mouseState.Y * scaleY;
            MouseInBackBuffer = new Vector2(mouseBackBufferX, mouseBackBufferY);

            // Check if mouse is within back buffer bounds (using converted coordinates)
            var mouseInWindow = mouseBackBufferX >= 0 && mouseBackBufferX < backBufferWidth &&
                                mouseBackBufferY >= 0 && mouseBackBufferY < backBufferHeight;

            if (!IsActive || !mouseInWindow)
            {
                Mouse = PrevMouseState;
#if !ANDROID
                Keyboard = new KeyboardState(); // Clear keyboard on PC when inactive
#endif
            }
            else
            {
                Mouse = mouseState;
            }

            // --- VIRTUAL MOUSE (UI) ---

            if (Touch.Count > 0)
            {
                // CASE 1: Touch detected (Android/Touchscreen)
                var touch = Touch[0];
                var touchPos = touch.Position;

                // Convert touch position to virtual UI coordinates
                var virtualTouchPos = UiScaler.ToVirtual(new Microsoft.Xna.Framework.Point((int)touchPos.X, (int)touchPos.Y));

                UiTouchPosition = virtualTouchPos;
                UiMousePosition = virtualTouchPos; // Mouse follows finger

                // Touch as left mouse button
                var leftButtonState = (touch.State == TouchLocationState.Pressed || touch.State == TouchLocationState.Moved)
                    ? Microsoft.Xna.Framework.Input.ButtonState.Pressed
                    : Microsoft.Xna.Framework.Input.ButtonState.Released;

                UiMouseState = new Microsoft.Xna.Framework.Input.MouseState(
                    virtualTouchPos.X,
                    virtualTouchPos.Y,
                    0,
                    leftButtonState,
                    Microsoft.Xna.Framework.Input.ButtonState.Released, Microsoft.Xna.Framework.Input.ButtonState.Released, Microsoft.Xna.Framework.Input.ButtonState.Released, Microsoft.Xna.Framework.Input.ButtonState.Released);
            }
            else
            {
                // CASE 2: No touch
#if ANDROID
                // Keep the cursor at the last touch position when finger is lifted.
                // This prevents the UI system from thinking we clicked outside the control.
                // The cursor will stay at this position until next touch - we DON'T move it to (-1, -1).

                UiMousePosition = PrevUiMouseState.Position;
                UiTouchPosition = PrevUiMouseState.Position;

                UiMouseState = new Microsoft.Xna.Framework.Input.MouseState(
                    UiMousePosition.X, UiMousePosition.Y,
                    0,
                    Microsoft.Xna.Framework.Input.ButtonState.Released,
                    Microsoft.Xna.Framework.Input.ButtonState.Released, Microsoft.Xna.Framework.Input.ButtonState.Released, Microsoft.Xna.Framework.Input.ButtonState.Released, Microsoft.Xna.Framework.Input.ButtonState.Released);
#else
                // WINDOWS: Use standard mouse
                UiMousePosition = UiScaler.ToVirtual(Mouse.Position);
                UiTouchPosition = UiMousePosition;

                UiMouseState = new Microsoft.Xna.Framework.Input.MouseState(
                    UiMousePosition.X,
                    UiMousePosition.Y,
                    Mouse.ScrollWheelValue,
                    Mouse.LeftButton,
                    Mouse.MiddleButton,
                    Mouse.RightButton,
                    Mouse.XButton1,
                    Mouse.XButton2);
#endif
            }

            // Update MouseRay when mouse position changes OR when touch position changes
            bool shouldUpdateRay = false;

            if (PrevMouseState.Position != Mouse.Position)
                shouldUpdateRay = true;

            if (PrevTouchState.Count != Touch.Count)
                shouldUpdateRay = true;

            // Check if touch position changed (finger moved)
            if (Touch.Count > 0 && PrevTouchState.Count > 0)
            {
                if (Touch[0].Position != PrevTouchState[0].Position)
                    shouldUpdateRay = true;
            }

            if (shouldUpdateRay)
                UpdateMouseRay();
        }

        private void UpdateMouseRay()
        {
            // Use touch position if available, otherwise use mouse position in back buffer space
            Vector2 inputPosition;
            if (Touch.Count > 0)
            {
                // Touch position needs same scaling as mouse in fullscreen borderless mode
                var touchPos = Touch[0].Position;
                int backBufferWidth = GraphicsDevice.PresentationParameters.BackBufferWidth;
                int backBufferHeight = GraphicsDevice.PresentationParameters.BackBufferHeight;

                var clientBounds = Window.ClientBounds;
                int inputAreaWidth = clientBounds.Width > 0 ? clientBounds.Width : backBufferWidth;
                int inputAreaHeight = clientBounds.Height > 0 ? clientBounds.Height : backBufferHeight;

                float scaleX = (float)backBufferWidth / inputAreaWidth;
                float scaleY = (float)backBufferHeight / inputAreaHeight;
                inputPosition = new Vector2(touchPos.X * scaleX, touchPos.Y * scaleY);
            }
            else
            {
                // Use pre-calculated mouse position in back buffer space
                inputPosition = MouseInBackBuffer;
            }

            // Create viewport matching actual back buffer size for correct unprojection
            // (GraphicsDevice.Viewport may be set to render target size from previous frame)
            var backBufferViewport = new Viewport(
                0, 0,
                GraphicsDevice.PresentationParameters.BackBufferWidth,
                GraphicsDevice.PresentationParameters.BackBufferHeight);

            Vector3 farSource = new Vector3(inputPosition, 1f);
            Vector3 farPoint = backBufferViewport.Unproject(
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

            EnsureEffectCache(destination.Width, destination.Height);

            if (effect == GraphicsManager.Instance.FXAAEffect)
            {
                effect.Parameters["Resolution"]?.SetValue(_cachedEffectResolution);
            }

            if (effect == GraphicsManager.Instance.FXAAEffect || effect == GraphicsManager.Instance.AlphaRGBEffect)
            {
                effect.Parameters["WorldViewProjection"]?.SetValue(_cachedEffectOrtho);
            }

            GraphicsManager.Instance.Sprite.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, GraphicsManager.GetQualityLinearSamplerState(), DepthStencilState.None, RasterizerState.CullNone, effect);
            GraphicsManager.Instance.Sprite.Draw(source, GraphicsDevice.Viewport.Bounds, Color.White);
            GraphicsManager.Instance.Sprite.End();

            // Deactivate render target after each effect
            GraphicsDevice.SetRenderTarget(null);
        }

        private void DrawFinalImageToScreen(RenderTarget2D sourceTarget)
        {
            GraphicsDevice.Clear(Color.Black);

            Effect gammaEffect = Constants.MSAA_ENABLED ? GraphicsManager.Instance.GammaCorrectionEffect : null;

            GraphicsManager.Instance.Sprite.Begin(
                SpriteSortMode.Deferred,
                BlendState.Opaque,
                GraphicsManager.GetQualityLinearSamplerState(),
                DepthStencilState.None,
                RasterizerState.CullNone,
                gammaEffect);

            GraphicsManager.Instance.Sprite.Draw(sourceTarget, GraphicsDevice.Viewport.Bounds, Color.White);
            GraphicsManager.Instance.Sprite.End();
        }

        public void ApplyGraphicsConfiguration(GraphicsSettings graphics)
        {
            if (graphics == null)
            {
                return;
            }

#if ANDROID
            // On Android, use actual screen size from GraphicsAdapter
            var screenSize = GetActualScreenSize();
            _graphics.PreferredBackBufferWidth = screenSize.X;
            _graphics.PreferredBackBufferHeight = screenSize.Y;
            _graphics.ApplyChanges();

            UiScaler.Configure(
                screenSize.X,
                screenSize.Y,
                Math.Max(1, graphics.UiVirtualWidth),
                Math.Max(1, graphics.UiVirtualHeight),
                ScaleMode.Stretch);

            Camera.Instance.AspectRatio = (float)screenSize.X / screenSize.Y;

            _logger?.LogDebug("Android graphics configured: {Width}x{Height}, UiScaler: {ScaleX:F4}x{ScaleY:F4}",
                screenSize.X, screenSize.Y,
                UiScaler.ScaleX, UiScaler.ScaleY);
#elif IOS
            // On iOS, use actual screen size, not config values
            var screenSize = GetActualScreenSize();
            _graphics.PreferredBackBufferWidth = screenSize.X;
            _graphics.PreferredBackBufferHeight = screenSize.Y;
            _graphics.ApplyChanges();

            UiScaler.Configure(
                screenSize.X,
                screenSize.Y,
                Math.Max(1, graphics.UiVirtualWidth),
                Math.Max(1, graphics.UiVirtualHeight),
                ScaleMode.Stretch);

            Camera.Instance.AspectRatio = (float)screenSize.X / screenSize.Y;

            _logger?.LogDebug("iOS graphics configured: {Width}x{Height}, UiScaler: {ScaleX:F4}x{ScaleY:F4}",
                screenSize.X, screenSize.Y,
                UiScaler.ScaleX, UiScaler.ScaleY);
#else
            _graphics.PreferredBackBufferWidth = Math.Max(1, graphics.Width);
            _graphics.PreferredBackBufferHeight = Math.Max(1, graphics.Height);
            _graphics.IsFullScreen = graphics.IsFullScreen;

            // In fullscreen mode, we need HardwareModeSwitch = true to actually change resolution.
            // With HardwareModeSwitch = false (borderless), back buffer is always screen size.
            // For windowed mode, HardwareModeSwitch doesn't matter.
            if (graphics.IsFullScreen)
            {
                _graphics.HardwareModeSwitch = true;
            }

            _graphics.ApplyChanges();

            // Get actual window/backbuffer size after ApplyChanges (may differ from preferred)
            int actualWidth = GraphicsDevice.PresentationParameters.BackBufferWidth;
            int actualHeight = GraphicsDevice.PresentationParameters.BackBufferHeight;

            // Update viewport to match actual back buffer size (required for correct mouse ray casting)
            GraphicsDevice.Viewport = new Viewport(0, 0, actualWidth, actualHeight);

            UiScaler.Configure(
                actualWidth,
                actualHeight,
                Math.Max(1, graphics.UiVirtualWidth),
                Math.Max(1, graphics.UiVirtualHeight),
                ScaleMode.Uniform);

            // Update camera aspect ratio after resolution change
            Camera.Instance.AspectRatio = (float)actualWidth / actualHeight;
#endif

            _scaleFactor = UiScaler.Scale;
        }

        private void EnsureEffectCache(int width, int height)
        {
            var size = new Point(width, height);
            if (_effectCacheValid && size == _lastEffectTargetSize)
            {
                return;
            }

            _lastEffectTargetSize = size;
            _cachedEffectResolution = new Vector2(width, height);
            _cachedEffectOrtho = Matrix.CreateOrthographicOffCenter(
                0,
                width,
                height,
                0,
                0,
                1);
            _effectCacheValid = true;
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

        public static void PersistConnectSettings(string host, int port)
        {
            if (string.IsNullOrWhiteSpace(host) || port <= 0 || port > 65535)
            {
                return;
            }

            var logger = AppLoggerFactory?.CreateLogger<MuGame>();
            try
            {
                Directory.CreateDirectory(ConfigDirectory ?? AppContext.BaseDirectory);

                JsonObject root = LoadLocalSettings(logger);
                if (root["MuOnlineSettings"] is not JsonObject muSettings)
                {
                    muSettings = new JsonObject();
                    root["MuOnlineSettings"] = muSettings;
                }

                muSettings["ConnectServerHost"] = host;
                muSettings["ConnectServerPort"] = port;

                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(LocalSettingsPath, root.ToJsonString(options));
                logger?.LogInformation("Saved ConnectServer settings to {Path}", LocalSettingsPath);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to persist connect server settings to disk.");
            }
        }

        public static void PersistGraphicsPreset(GraphicsQualityPreset preset)
        {
            var logger = AppLoggerFactory?.CreateLogger<MuGame>();
            try
            {
                Directory.CreateDirectory(ConfigDirectory ?? AppContext.BaseDirectory);

                JsonObject root = LoadLocalSettings(logger);
                if (root["MuOnlineSettings"] is not JsonObject muSettings)
                {
                    muSettings = new JsonObject();
                    root["MuOnlineSettings"] = muSettings;
                }

                if (muSettings["Graphics"] is not JsonObject graphics)
                {
                    graphics = new JsonObject();
                    muSettings["Graphics"] = graphics;
                }

                graphics["QualityPreset"] = preset.ToString();

                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(LocalSettingsPath, root.ToJsonString(options));
                logger?.LogInformation("Saved graphics preset to {Path}", LocalSettingsPath);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to persist graphics preset to disk.");
            }
        }

        public static void PersistDisplaySettings(int width, int height, bool isFullScreen)
        {
            var logger = AppLoggerFactory?.CreateLogger<MuGame>();
            try
            {
                Directory.CreateDirectory(ConfigDirectory ?? AppContext.BaseDirectory);

                JsonObject root = LoadLocalSettings(logger);
                if (root["MuOnlineSettings"] is not JsonObject muSettings)
                {
                    muSettings = new JsonObject();
                    root["MuOnlineSettings"] = muSettings;
                }

                if (muSettings["Graphics"] is not JsonObject graphics)
                {
                    graphics = new JsonObject();
                    muSettings["Graphics"] = graphics;
                }

                graphics["Width"] = width;
                graphics["Height"] = height;
                graphics["IsFullScreen"] = isFullScreen;

                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(LocalSettingsPath, root.ToJsonString(options));
                logger?.LogInformation("Saved display settings to {Path}", LocalSettingsPath);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to persist display settings to disk.");
            }
        }

        private static JsonObject LoadLocalSettings(ILogger logger)
        {
            if (!File.Exists(LocalSettingsPath))
            {
                return new JsonObject();
            }

            try
            {
                var text = File.ReadAllText(LocalSettingsPath);
                return JsonNode.Parse(text) as JsonObject ?? new JsonObject();
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to read existing local settings; recreating file.");
                return new JsonObject();
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
