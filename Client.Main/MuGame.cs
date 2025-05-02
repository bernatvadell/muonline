using Client.Main.Controllers;
using Client.Main.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

// **** ADDED USINGS ****
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Client.Main.Configuration; // Namespace for MuOnlineSettings
using Client.Main.Networking;
using System.Collections.Concurrent;    // Namespace for NetworkManager (will be created next)
// **** END ADDED USINGS ****

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
        public TouchCollection PrevTouchState { get; private set; }
        public TouchCollection Touch { get; private set; }

        // **** ADDED PROPERTIES ****
        public static IConfiguration AppConfiguration { get; private set; }
        public static ILoggerFactory AppLoggerFactory { get; private set; }
        public static MuOnlineSettings AppSettings { get; private set; }
        public static NetworkManager Network { get; private set; } // The network manager instance
        // **** END ADDED PROPERTIES ****

        // **** ADDED QUEUE AND SCHEDULER ****
        private static readonly ConcurrentQueue<Action> _mainThreadActions = new ConcurrentQueue<Action>();

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
        // **** END ADDED QUEUE AND SCHEDULER ****

        public Ray MouseRay { get; private set; }

        public GameTime GameTime { get; private set; }

        // DepthStencilState to disable depth mask
        public DepthStencilState DisableDepthMask { get; } = new DepthStencilState
        {
            DepthBufferEnable = true,
            DepthBufferWriteEnable = false
        };

        private float _scaleFactor;

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
        }

        // **** ADDED VALIDATION METHOD ****
        private static bool ValidateSettings(MuOnlineSettings? settings, ILogger logger)
        {
            // Reuse the validation logic from MuOnlineConsole's App.axaml.cs
            // (Ensure necessary using for TargetProtocolVersion is present)
            if (settings == null) { logger.LogError("❌ Failed to load config from 'MuOnlineSettings'."); return false; }
            bool isValid = true;
            if (string.IsNullOrWhiteSpace(settings.ConnectServerHost) || settings.ConnectServerPort == 0) { logger.LogError("❌ Connect Server host/port invalid."); isValid = false; }
            if (string.IsNullOrWhiteSpace(settings.Username) || string.IsNullOrWhiteSpace(settings.Password)) { logger.LogError("❌ Username/password invalid."); isValid = false; }
            if (string.IsNullOrWhiteSpace(settings.ProtocolVersion) || !Enum.TryParse<Client.TargetProtocolVersion>(settings.ProtocolVersion, true, out _)) // Case-insensitive parse
            { logger.LogError("❌ ProtocolVersion '{V}' invalid. Valid: {Vs}", settings.ProtocolVersion, string.Join(", ", Enum.GetNames<Client.TargetProtocolVersion>())); isValid = false; }
            if (string.IsNullOrWhiteSpace(settings.ClientVersion)) { logger.LogWarning("⚠️ ClientVersion not set."); }
            if (string.IsNullOrWhiteSpace(settings.ClientSerial)) { logger.LogWarning("⚠️ ClientSerial not set."); }
            return isValid;
        }
        // **** END ADDED VALIDATION METHOD ****

        // Istniejąca metoda generyczna
        public void ChangeScene<T>() where T : BaseScene, new()
        {
            if (typeof(T) == typeof(GameScene))
            {
                // Ten kod nie powinien być już nigdy wywołany dla GameScene
                // Możesz rzucić wyjątek lub zalogować ostrzeżenie
                throw new InvalidOperationException("GameScene requires parameters. Use ChangeScene(BaseScene newScene) instead.");
            }
            _logger.LogInformation(">>> ChangeScene<{SceneType}>() called (generic)", typeof(T).Name);
            BaseScene newScene = new T(); // Tworzenie instancji za pomocą konstruktora bez parametrów
            ChangeSceneInternal(newScene); // Wywołanie metody pomocniczej
        }

        // NOWA metoda akceptująca instancję sceny
        public void ChangeScene(BaseScene newScene)
        {
            if (newScene == null)
            {
                _logger.LogError("Attempted to change scene to a null instance.");
                throw new ArgumentNullException(nameof(newScene));
            }
            _logger.LogInformation(">>> ChangeScene(BaseScene newScene) called with scene type: {SceneType}", newScene.GetType().Name);
            ChangeSceneInternal(newScene); // Wywołanie metody pomocniczej
        }

        // Prywatna metoda pomocnicza do zmiany sceny
        private async void ChangeSceneInternal(BaseScene newScene)
        {
            _logger.LogInformation("--- ChangeSceneInternal: Starting scene change to {SceneType}...", newScene.GetType().Name);

            // Opcjonalnie: Pokaż ekran ładowania przed usunięciem starej sceny
            // ShowLoadingScreen();

            // Usuń starą scenę
            if (ActiveScene != null)
            {
                _logger.LogDebug("--- ChangeSceneInternal: Disposing previous scene ({SceneType})...", ActiveScene.GetType().Name);
                ActiveScene.Dispose();
                _logger.LogDebug("--- ChangeSceneInternal: Previous scene disposed.");
            }
            ActiveScene = null; // Zapewnij, że nie ma odniesienia podczas ładowania nowej

            // Ustaw nową scenę
            ActiveScene = newScene;
            _logger.LogDebug("--- ChangeSceneInternal: ActiveScene set to {SceneType}.", ActiveScene.GetType().Name);


            // Zainicjalizuj/Załaduj nową scenę (zakładając, że Initialize/Load jest asynchroniczne)
            try
            {
                _logger.LogDebug("--- ChangeSceneInternal: Calling Initialize() for {SceneType}...", ActiveScene.GetType().Name);
                // Upewnij się, że metoda Initialize istnieje i jest odpowiednia,
                // lub użyj await ActiveScene.Load(), jeśli tak działa Twój system.
                await ActiveScene.Initialize();
                _logger.LogDebug("--- ChangeSceneInternal: Initialize() completed for {SceneType}.", ActiveScene.GetType().Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "!!! ChangeSceneInternal: Exception during Initialize() for {SceneType}.", ActiveScene.GetType().Name);
                // Obsłuż błąd - może powrót do LoginScene?
                // ActiveScene = new LoginScene(); // Awaryjny powrót
                // await ActiveScene.Initialize();
                return; // Zakończ zmianę sceny po błędzie
            }


            // Opcjonalnie: Ukryj ekran ładowania po załadowaniu nowej sceny
            // HideLoadingScreen();

            _logger.LogInformation("<<< ChangeSceneInternal: Scene change to {SceneType} complete.", ActiveScene.GetType().Name);
        }

        protected override void Initialize()
        {
            // --- Configuration Setup ---
            AppConfiguration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

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

            var bootLogger = AppLoggerFactory.CreateLogger("MuGame"); // Logger for startup

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
            var characterState = new Client.CharacterState(AppLoggerFactory);
            var scopeManager = new Client.ScopeManager(AppLoggerFactory, characterState);
            Network = new NetworkManager(AppLoggerFactory, AppSettings, characterState, scopeManager);
            bootLogger.LogInformation("✅ Network Manager initialized.");


            IsMouseVisible = false; // Keep this if you want a custom cursor
            base.Initialize();
            float baseAspectRatio = 16f / 9f; // Relación de aspecto base (ejemplo: 16:9)
            float currentAspectRatio = (float)_graphics.PreferredBackBufferWidth / _graphics.PreferredBackBufferHeight;

            _scaleFactor = currentAspectRatio / baseAspectRatio;

            Console.WriteLine($"Scale Factor: {_scaleFactor}");
        }

        protected override void UnloadContent()
        {
            base.UnloadContent();
            GraphicsManager.Instance.Dispose();

            // --- DISPOSE NETWORK MANAGER ---
            // Use ?. operator for safety
            Network?.DisposeAsync().AsTask().Wait(); // Dispose network resources cleanly
            AppLoggerFactory?.Dispose(); // Dispose logger factory
            // --- END DISPOSE ---
        }

        protected override void Update(GameTime gameTime)
        {
            // --- Process Main Thread Actions ---
            // **** DODAJ LOGOWANIE ****
            int actionCount = 0;
            var queueLogger = AppLoggerFactory?.CreateLogger("MuGame.MainThreadQueue"); // Logger dla kolejki
                                                                                        // **** KONIEC DODAWANIA LOGOWANIA ****

            while (_mainThreadActions.TryDequeue(out Action? action))
            {
                // **** DODAJ LOGOWANIE ****
                actionCount++;
                queueLogger?.LogTrace("Dequeued action #{Count}. Attempting execution...", actionCount);
                // **** KONIEC DODAWANIA LOGOWANIA ****
                try
                {
                    action?.Invoke();
                    // **** DODAJ LOGOWANIE ****
                    queueLogger?.LogTrace("Action #{Count} executed successfully.", actionCount);
                    // **** KONIEC DODAWANIA LOGOWANIA ****
                }
                catch (Exception ex)
                {
                    queueLogger?.LogError(ex, "Error executing action #{Count} scheduled on main thread.", actionCount);
                }
            }
            // **** DODAJ LOGOWANIE ****
            // if (actionCount > 0) queueLogger?.LogDebug("Processed {Count} actions from queue this frame.", actionCount);
            // **** KONIEC DODAWANIA LOGOWANIA ****
            // --- End Process Main Thread Actions ---


            // --- Existing Update Logic ---
            try // Dodaj zewnętrzny try
            {
                GameTime = gameTime;
                UpdateInputInfo(gameTime);
                CheckShaderToggles();

                try // Dodaj wewnętrzny try dla ActiveScene.Update
                {
                    ActiveScene?.Update(gameTime);
                }
                catch (Exception sceneEx)
                {
                    _logger?.LogCritical(sceneEx, "Unhandled exception in ActiveScene.Update ({SceneType})!", ActiveScene?.GetType().Name ?? "null");
                    // Rozważ zatrzymanie gry lub powrót do bezpiecznej sceny
                    // Exit();
                }

                try // Dodaj wewnętrzny try dla base.Update
                {
                    base.Update(gameTime);
                }
                catch (Exception baseEx)
                {
                    _logger?.LogCritical(baseEx, "Unhandled exception in base.Update!");
                    // Rozważ zatrzymanie gry
                    // Exit();
                }
            }
            catch (Exception e) // Złap inne nieoczekiwane błędy w Update
            {
                Debug.WriteLine(e);
                _logger?.LogCritical(e, "Unhandled exception in MuGame.Update loop (outside scene/base update)!");
                // Exit();
            }
            // --- End Existing Update Logic ---
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
                AppLoggerFactory?.CreateLogger<MuGame>()?.LogCritical("Network Manager is null during LoadContent. Cannot connect.");
                // Potentially exit or handle error
            }
            // --- END NETWORK CONNECTION ---


            // Load the initial scene (e.g., LoginScene) AFTER network connection starts
            ChangeSceneAsync(Constants.ENTRY_SCENE).ContinueWith(t =>
            {
                if (t.Exception != null)
                    Debug.WriteLine("Error changing scene: " + t.Exception);
            });

        }

        private async Task ChangeSceneAsync(Type sceneType)
        {
            ActiveScene?.Dispose();
            ActiveScene = (BaseScene)Activator.CreateInstance(sceneType);
            await ActiveScene.Initialize();
        }

        // *** POCZĄTEK NOWEJ METODY ***
        /// <summary>
        /// Changes the active scene to the provided scene instance.
        /// Handles disposal of the old scene and initialization of the new one.
        /// </summary>
        /// <param name="newScene">The pre-initialized scene instance to switch to.</param>
        // Add a logger field for use in instance methods
        private ILogger? _logger => AppLoggerFactory?.CreateLogger<MuGame>();

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
            finally
            {
                // Ensure that no render target is active to avoid the Present error
                GraphicsDevice.SetRenderTarget(null);
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

        public static void DisposeInstance()
        {
            if (Instance != null)
            {
                Instance.Dispose();
                Instance = null;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            { 
                // Dispose managed resources like NetworkManager if not already done in UnloadContent
                // Use ?. operator for safety
                Network?.DisposeAsync().AsTask().Wait(); // Ensure disposal if UnloadContent wasn't called
                AppLoggerFactory?.Dispose();
            }
            base.Dispose(disposing);
            Instance = null; // Clear singleton instance
        }
    }
}