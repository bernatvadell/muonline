// GameScene.cs
using Client.Main.Controls;
using Client.Main.Controls.UI;
using Client.Main.Controls.UI.Game;
using Client.Main.Models;
using Client.Main.Objects.Player;
using Client.Main.Worlds;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using Client.Main.Objects;
using Client.Main.Core.Utilities;
using Client.Main.Networking.PacketHandling.Handlers; // For CharacterClassNumber
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Client.Main.Controls.UI.Game.Inventory;
using Client.Main.Controls.UI.Game.Map;
using Client.Main.Controls.UI.Game.Party;
using Client.Main.Controls.UI.Game.PauseMenu;
using Client.Main.Controls.UI.Game.Character;
using Client.Main.Controls.UI.Game.Trade;
using Microsoft.Xna.Framework.Graphics;
using Client.Main.Networking;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;
using Client.Main.Controls.UI.Game.Buffs;
using Client.Main.Controls.UI.Game.Hud;
using MUnique.OpenMU.Network.Packets;
using Client.Main.Controllers;
using Client.Main.Helpers;

namespace Client.Main.Scenes
{
    public class GameScene : BaseScene
    {
        // ──────────────────────────── Fields ────────────────────────────
        private readonly PlayerObject _hero;
        private readonly MainControl _main;
        private GameSceneMapController _mapController;
        private MapListControl _mapListControl;
        private ChatLogWindow _chatLog;
        private MoveCommandWindow _moveCommandWindow;
        private ChatInputBoxControl _chatInput;
        private InventoryControl _inventoryControl;
        private NotificationManager _notificationManager;
        private PartyPanelControl _partyPanel;
        private readonly (string Name, CharacterClassNumber Class, ushort Level, byte[] Appearance) _characterInfo;
        private CharacterInfoWindowControl _characterInfoWindow;
        private ILogger _logger = MuGame.AppLoggerFactory?.CreateLogger<GameScene>() ?? NullLogger<GameScene>.Instance;
        private LabelControl _pingLabel; // Displays current ping
        private double _pingTimer = 0;
        private int? _lastPingValue = null;
        private PauseMenuControl _pauseMenu; // ESC menu
        private Controls.UI.Game.Skills.SkillQuickSlot _skillQuickSlot; // Skill quick slot
        private Controls.UI.Game.Skills.SkillSelectionPanel _skillSelectionPanel; // Skill selection panel (independent)
        private ActiveBuffsPanel _activeBuffsPanel; // Active buffs display (top-left corner)
        private Texture2D _backgroundTexture;
        private ProgressBarControl _progressBar;
        private GameSceneSkillController _skillController;
        private GameSceneNotificationController _notificationController;
        private GameScenePlayerMenuController _playerMenuController;
        private GameSceneInputController _inputController;
        private GameSceneScopeImportController _scopeImportController;
        private GameSceneObjectEditorController _objectEditorController;
        private GameSceneDuelController _duelController;
        private GameSceneChatController _chatController;
        private GameSceneUiPreloadController _uiPreloadController;
        private GameSceneWindowCloseController _windowCloseController;

        // Performance optimization fields - track object IDs for O(1) lookups
        // ───────────────────────── Properties ─────────────────────────
        public PlayerObject Hero => _hero;
        public ChatLogWindow ChatLog => _chatLog;
        public InventoryControl InventoryControl => _inventoryControl;
        public TradeControl TradeControl => TradeControl.Instance;
        public PauseMenuControl PauseMenu => _pauseMenu;

        public static readonly IReadOnlyDictionary<byte, Type> MapWorldRegistry = DiscoverWorlds();

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "World registry uses reflection; trimming is not supported for scene discovery.")]
        private static IReadOnlyDictionary<byte, Type> DiscoverWorlds()
        {
            var registry = new Dictionary<byte, Type>();
            var worldTypes = Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(WalkableWorldControl).IsAssignableFrom(t));

            foreach (var type in worldTypes)
            {
                var attr = type.GetCustomAttribute<WorldInfoAttribute>();
                if (attr != null)
                {
                    if (!registry.TryAdd((byte)attr.MapId, type))
                    {
                        // Optionally log a warning about duplicate MapId
                    }
                }
            }
            return registry;
        }

        // ──────────────────────── Constructors ────────────────────────
        public GameScene((string Name, CharacterClassNumber Class, ushort Level, byte[] Appearance) characterInfo)
        {
            _characterInfo = characterInfo;
            _logger?.LogDebug($"GameScene constructor called for Character: {_characterInfo.Name} ({_characterInfo.Class})");

            // Create the hero with the appearance data from the character list
            _hero = new PlayerObject(new AppearanceData(characterInfo.Appearance));

            _main = new MainControl(MuGame.Network.GetCharacterState());
            Controls.Add(_main);
            Controls.Add(NpcShopControl.Instance);
            Controls.Add(VaultControl.Instance);
            Controls.Add(ChaosMixControl.Instance);
            Controls.Add(TradeControl.Instance);

            _mapListControl = new MapListControl { Visible = false };

            _chatLog = new ChatLogWindow
            {
                X = 5,
                Y = UiScaler.VirtualSize.Y - 160 - ChatInputBoxControl.CHATBOX_HEIGHT
            };
            Controls.Add(_chatLog);

            _chatInput = new ChatInputBoxControl(_chatLog, MuGame.AppLoggerFactory)
            {
                X = 5,
                Y = UiScaler.VirtualSize.Y - 65 - ChatInputBoxControl.CHATBOX_HEIGHT
            };
            Controls.Add(_chatInput);
            _duelController = new GameSceneDuelController(this, _chatLog, _logger);

            _notificationManager = new NotificationManager();
            Controls.Add(_notificationManager);
            _notificationManager.BringToFront();
            _notificationController = new GameSceneNotificationController(_notificationManager, _chatLog);
            _notificationController.AddPending(ChatMessageHandler.TakePendingServerMessages());
            _scopeImportController = new GameSceneScopeImportController(this, _logger);

            _inventoryControl = new InventoryControl(MuGame.Network, MuGame.AppLoggerFactory);
            Controls.Add(_inventoryControl);
            _inventoryControl.HookEvents();
            _windowCloseController = new GameSceneWindowCloseController(_inventoryControl, _logger);

            _moveCommandWindow = new MoveCommandWindow(MuGame.AppLoggerFactory, MuGame.Network);
            Controls.Add(_moveCommandWindow);
            _moveCommandWindow.MapWarpRequested += OnMapWarpRequested;

            _characterInfoWindow = new CharacterInfoWindowControl { X = 20, Y = 50, Visible = false };
            Controls.Add(_characterInfoWindow);

            _partyPanel = new PartyPanelControl();
            Controls.Add(_partyPanel);

            _pingLabel = new LabelControl
            {
                Text = "Ping: --",
                Align = ControlAlign.Bottom | ControlAlign.Right,
                Margin = new Margin { Bottom = 5, Right = 5 },
                FontSize = 10,
                TextColor = Color.White
            };
            Controls.Add(_pingLabel);
            _pingLabel.BringToFront();

            _chatInput.BringToFront();
            DebugPanel.BringToFront();
            Cursor.BringToFront();

            // Pause/ESC menu
            _pauseMenu = new PauseMenuControl();
            Controls.Add(_pauseMenu);
            _pauseMenu.BringToFront();

            // Skill selection panel (independent, not child of quick slot)
            _skillSelectionPanel = new Controls.UI.Game.Skills.SkillSelectionPanel();
            Controls.Add(_skillSelectionPanel);

            // Skill quick slot
            _skillQuickSlot = new Controls.UI.Game.Skills.SkillQuickSlot(MuGame.Network.GetCharacterState());
            _skillQuickSlot.SetSelectionPanel(_skillSelectionPanel); // Connect panel
            Controls.Add(_skillQuickSlot);
            _skillQuickSlot.BringToFront();
            _skillController = new GameSceneSkillController(this, _skillQuickSlot, _logger, _duelController.IsDuelAttackTarget);

            // Experience bar
            var experienceBar = new ExperienceBarControl(MuGame.Network.GetCharacterState());
            Controls.Add(experienceBar);

            // Active buffs panel (top-left corner, no border)
            _activeBuffsPanel = new ActiveBuffsPanel(MuGame.Network.GetCharacterState());
            Controls.Add(_activeBuffsPanel);
            _activeBuffsPanel.BringToFront();

            // Duel HUD scoreboard (top center, visible only during duel)
            var duelHud = new DuelHudControl(MuGame.Network.GetCharacterState());
            Controls.Add(duelHud);
            duelHud.BringToFront();
            _playerMenuController = new GameScenePlayerMenuController(this, StartWhisperToPlayer, _duelController.OnDuelRequestedFromContextMenu);
            _playerMenuController.Initialize();
            _inputController = new GameSceneInputController(
                this,
                _pauseMenu,
                _playerMenuController,
                _moveCommandWindow,
                _inventoryControl,
                _characterInfoWindow,
                _chatInput,
                _chatLog);
            _objectEditorController = new GameSceneObjectEditorController(this, _logger);
            _objectEditorController.Initialize();

            try
            {
                _backgroundTexture = MuGame.Instance.Content.Load<Texture2D>("Background");
            }
            catch (Exception ex)
            {
                _logger?.LogDebug($"[GameScene] Background load failed: {ex.Message}");
            }

            _progressBar = new ProgressBarControl();
            Controls.Add(_progressBar);
            _mapController = new GameSceneMapController(
                this,
                _main,
                _progressBar,
                _chatLog,
                _chatInput,
                _mapListControl,
                DebugPanel,
                Cursor,
                _scopeImportController,
                _logger);
            _mapController.EnsureLoadingScreen();
            _chatController = new GameSceneChatController(_mapController, _duelController, _chatLog, _logger);
            _chatInput.MessageSendRequested += _chatController.OnChatMessageSendRequested;
            _uiPreloadController = new GameSceneUiPreloadController(this, _logger);

            // Start pre-loading common UI assets in background to prevent freezes
            // This runs async and won't block scene initialization
            _ = _uiPreloadController.StartPreloadAsync();
        }

        public GameScene() : this(GetCharacterInfoFromState())
        {
        }

        public GameScene((string Name, CharacterClassNumber Class, ushort Level, byte[] Appearance) characterInfo, NetworkManager networkManager)
            : this(characterInfo)
        {
            // Optionally store networkManager if needed in the future
        }

        private static (string Name, CharacterClassNumber Class, ushort Level, byte[] Appearance) GetCharacterInfoFromState()
        {
            var state = MuGame.Network?.GetCharacterState();
            if (state != null)
            {
                return (state.Name ?? "Unknown", state.Class, state.Level, Array.Empty<byte>());
            }
            return ("Unknown", CharacterClassNumber.DarkKnight, 1, Array.Empty<byte>());
        }

        // ───────────────────── Content Loading (Progressive) ─────────────────────
        private void UpdateLoadProgress(string message, float progress)
        {
            MuGame.ScheduleOnMainThread(() => // Ensure UI updates are on the main thread
            {
                _mapController?.UpdateLoadProgress(message, progress);
            });
        }

        protected override async Task LoadSceneContentWithProgress(Action<string, float> progressCallback)
        {
            try
            {
                UpdateLoadProgress("Initializing Game Scene...", 0.0f);

                // 1. Hero Setup
                UpdateLoadProgress("Setting up hero info...", 0.05f);

                var charState = MuGame.Network?.GetCharacterState();
                if (charState == null)
                {
                    UpdateLoadProgress("Error: CharacterState is null.", 1.0f);
                    _logger?.LogDebug("CharacterState is null in GameScene.Load, cannot proceed.");
                    _main.Visible = false;
                    return;
                }

                _hero.CharacterClass = _characterInfo.Class;
                _hero.Name = _characterInfo.Name;

                charState.UpdateCoreCharacterInfo(
                    charState.Id,
                    _characterInfo.Name,
                    _characterInfo.Class,
                    _characterInfo.Level,
                    charState.PositionX,
                    charState.PositionY,
                    charState.MapId
                );

                _hero.NetworkId = charState.Id;
                _hero.Location = new Vector2(charState.PositionX, charState.PositionY);
                _logger?.LogDebug($"GameScene.LoadSceneContentWithProgress: _hero.NetworkId set to {charState.Id:X4}, Location set to ({charState.PositionX}, {charState.PositionY}).");

                UpdateLoadProgress("Hero info applied.", 0.1f);
                if (_windowCloseController != null)
                {
                    _hero.PlayerMoved += _windowCloseController.OnHeroMoved;
                    _hero.PlayerTookDamage += _windowCloseController.OnHeroTookDamage;
                }

                // 2. Determine Initial World (Quick)
                UpdateLoadProgress("Determining initial world...", 0.15f);
                Type initialWorldType = typeof(LorenciaWorld);
                if (charState != null && MapWorldRegistry.TryGetValue((byte)charState.MapId, out var mappedType))
                {
                    initialWorldType = mappedType;
                }
                else
                {
                    _logger?.LogDebug($"GameScene.Load: Unknown MapId: {charState?.MapId}. Defaulting to Lorencia.");
                }
                UpdateLoadProgress($"Initial world: {initialWorldType.Name}.", 0.2f);

                // 3. Instantiate and Initialize World
                UpdateLoadProgress($"Loading world: {initialWorldType.Name}...", 0.25f);

                if (World != null)
                {
                    Controls.Remove(World);
                    World.Dispose();
                    World = null;
                }

                var worldInstance = (WorldControl)Activator.CreateInstance(initialWorldType);
                if (worldInstance is WalkableWorldControl walkable)
                {
                    _logger?.LogDebug($"GameScene.LoadSceneContentWithProgress: About to assign _hero to walkable.Walker. _hero.NetworkId: {_hero.NetworkId:X4}");

                    walkable.Walker = _hero;
                    _logger?.LogDebug($"GameScene.LoadSceneContentWithProgress: Assigned _hero ({_hero.NetworkId:X4}) to walkableWorld.Walker.");

                    _scopeImportController?.EnsureWalkerNetworkId(walkable, charState.Id, "after assignment and verification");

                    _logger?.LogDebug($"GameScene.LoadSceneContentWithProgress: Walker.NetworkId after assignment and verification: {walkable.Walker?.NetworkId:X4}");
                }

                Controls.Add(worldInstance);
                World = worldInstance;

                World.Objects.Add(_hero);
                _logger?.LogDebug($"GameScene.LoadSceneContentWithProgress: Added _hero to World.Objects.");

                await worldInstance.Initialize();
                UpdateLoadProgress($"World {initialWorldType.Name} initialized.", 0.6f);

                if (worldInstance is WalkableWorldControl walkableAfterInit)
                {
                    _logger?.LogDebug($"GameScene.LoadSceneContentWithProgress: Walker.NetworkId after world initialization: {walkableAfterInit.Walker?.NetworkId:X4}");

                    _scopeImportController?.EnsureWalkerNetworkId(walkableAfterInit, charState.Id, "after world initialization");
                    _logger?.LogDebug($"GameScene.LoadSceneContentWithProgress: Walker.NetworkId after fix: {walkableAfterInit.Walker?.NetworkId:X4}");
                }

                // 4. Load Hero Assets
                UpdateLoadProgress("Loading hero assets...", 0.65f);
                if (_hero.Status == GameControlStatus.NonInitialized || _hero.Status == GameControlStatus.Initializing)
                {
                    ushort expectedNetworkId = charState.Id;
                    _logger?.LogDebug($"GameScene.LoadSceneContentWithProgress: _hero.NetworkId before Load(): {_hero.NetworkId:X4}");

                    await _hero.Load();

                    _logger?.LogDebug($"GameScene.LoadSceneContentWithProgress: _hero.NetworkId after Load(): {_hero.NetworkId:X4}");
                    _scopeImportController?.EnsureHeroNetworkId(expectedNetworkId, "after hero Load()");
                }
                UpdateLoadProgress("Hero assets loaded.", 0.80f);

                // 5. Import Pending Objects
                UpdateLoadProgress("Importing nearby entities...", 0.85f);
                if (World.Status == GameControlStatus.Ready)
                {
                    await (_scopeImportController?.ImportPendingRemotePlayersAsync() ?? Task.CompletedTask);
                    await (_scopeImportController?.ImportPendingNpcsMonstersAsync() ?? Task.CompletedTask);
                    await (_scopeImportController?.ImportPendingDroppedItemsAsync() ?? Task.CompletedTask);
                }
                else
                {
                    _logger?.LogDebug($"GameScene.Load: World not ready after Initialize (Status: {World.Status}). Pending objects may not import correctly.");
                }
                UpdateLoadProgress("Entities imported.", 0.95f);

                // Preload sounds for dropped items
                UpdateLoadProgress("Preloading sounds...", 0.96f);
                PreloadSounds();
                UpdateLoadProgress("Sounds preloaded.", 0.965f);

                // Preload NPC and monster textures
                UpdateLoadProgress("Preloading NPC textures...", 0.97f);
                // Skip preloading to avoid blocking
                UpdateLoadProgress("NPC textures preloaded.", 0.975f);

                // Preload UI textures so opening windows doesn't cause stalls
                UpdateLoadProgress("Preloading UI textures...", 0.98f);
                // Skip preloading to avoid blocking
                UpdateLoadProgress("UI textures preloaded.", 0.99f);

                if (World is WalkableWorldControl finalWalkable)
                {
                    _logger?.LogDebug($"GameScene.LoadSceneContentWithProgress: FINAL CHECK - Walker.NetworkId: {finalWalkable.Walker?.NetworkId:X4}, CharState.Id: {charState.Id:X4}");

                    // One last check and fix if needed
                    _scopeImportController?.EnsureWalkerNetworkId(finalWalkable, charState.Id, "final verification");
                    _logger?.LogDebug($"GameScene.LoadSceneContentWithProgress: After final fix - Walker.NetworkId: {finalWalkable.Walker?.NetworkId:X4}");
                }

                // Finalize
                _mapController?.UpdateLoadProgress("Game ready!", 1.0f);
                _main.Visible = true;
                _mapController?.UpdateMapName();
            }
            finally
            {
                _mapController?.DisposeLoadingScreen();
                if (_progressBar != null)
                {
                    _progressBar.Visible = false;
                }
            }
        }

        public override async Task Load()
        {
            // This method is called by BaseScene.Initialize() if LoadSceneContentWithProgress is not overridden,
            // OR if the overridden method calls base.Load().
            // For GameScene, we want the progressive loading, so we'll call it from here if this Load is hit.
            // However, with the new structure, InitializeWithProgressReporting should call LoadSceneContentWithProgress directly.
            // This is a fallback / ensures old paths might still work or for clarity.
            if (Status == GameControlStatus.Initializing) // Check if we are already in the new init flow
            {
                await LoadSceneContentWithProgress(UpdateLoadProgress);
            }
            else
            {
                // Fallback to old behavior or log a warning
                _logger?.LogDebug("GameScene.Load() called outside of InitializeWithProgressReporting flow. Consider refactoring.");
                await base.Load(); // Which is empty in BaseScene, then calls derived GameScene's old Load logic
            }
        }

        private async void OnMapWarpRequested(int mapIndex, string mapDisplayName)
        {
            _logger?.LogDebug($"Player requested warp to map index: {mapIndex}");
            var mapName = mapDisplayName;
            _chatLog.AddMessage("System", $"Warping to {mapName} (ID {mapIndex})...", MessageType.System);

            try
            {
                await MuGame.Network.SendWarpRequestAsync((ushort)mapIndex);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, $"Error sending warp request for map index {mapIndex}.");
                _chatLog.AddMessage("System", $"Error warping: {ex.Message}", MessageType.Error);
            }
        }

        // ─────────────────── Map Change Logic (Remains largely the same) ───────────────────
        public async Task ChangeMap([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type worldType)
        {
            if (_mapController != null)
            {
                await _mapController.ChangeMap(worldType);
            }
        }

        public async Task ChangeMap<T>() where T : WalkableWorldControl, new()
        {
            await ChangeMap(typeof(T));
        }

        // ─────────────────── Notification Handling ───────────────────
        public void ShowNotificationMessage(ServerMessage.MessageType messageType, string message)
        {
            _notificationController?.Enqueue(messageType, message);
        }

        // ─────────────────────────── Update Loop ───────────────────────────
        public override void Update(GameTime gameTime)
        {
            if (_mapController?.IsChangingWorld == true)
            {
                _mapController.UpdateLoading(gameTime);
                return;
            }

            var currentKeyboardState = MuGame.Instance.Keyboard;

            base.Update(gameTime);
            _inputController?.HandleGlobalInput(currentKeyboardState);

            _notificationManager?.Update(gameTime);
            _notificationController?.ProcessPending();

            if (World is WalkableWorldControl walkableWorld)
            {
                ScopeHandler.PumpNpcSpawnQueue(walkableWorld);
            }

            if (World == null || World.Status != GameControlStatus.Ready)
            {
                _playerMenuController?.ResetOnWorldUnavailable();
                _skillController?.ClearPending();
                _inputController?.UpdatePreviousKeyboardState(currentKeyboardState);
                return;
            }

            var uiMouse = MuGame.Instance.UiMouseState;
            var prevUiMouse = MuGame.Instance.PrevUiMouseState;
            _playerMenuController?.Update(gameTime, currentKeyboardState, uiMouse, prevUiMouse);
            _skillController?.Update();

            // Handle attack clicks on monsters with proper validation
            if (!IsMouseInputConsumedThisFrame &&
                MouseHoverObject is MonsterObject targetMonster &&
                MuGame.Instance.Mouse.LeftButton == ButtonState.Pressed &&
                MuGame.Instance.PrevMouseState.LeftButton == ButtonState.Released) // Fresh press
            {
                if (Hero != null &&
                    !Hero.IsDead && // Don't attack if player is dead
                    !targetMonster.IsDead &&
                    targetMonster.World == World && // Ensure same world
                    Vector2.Distance(Hero.Location, targetMonster.Location) <= Hero.GetAttackRangeTiles()) // Check range
                {
                    Hero.Attack(targetMonster);
                    SetMouseInputConsumed(); // Consume the click
                }
            }

            // Handle attack clicks on duel opponent players (treat as monster during duel)
            if (!IsMouseInputConsumedThisFrame &&
                MouseHoverObject is PlayerObject targetPlayer &&
                targetPlayer != _hero &&
                (_duelController?.IsDuelAttackTarget(targetPlayer) == true) &&
                MuGame.Instance.Mouse.LeftButton == ButtonState.Pressed &&
                MuGame.Instance.PrevMouseState.LeftButton == ButtonState.Released) // Fresh press
            {
                if (Hero != null &&
                    !Hero.IsDead &&
                    !targetPlayer.IsDead &&
                    targetPlayer.World == World)
                {
                    Hero.Attack(targetPlayer);
                    SetMouseInputConsumed();
                }
            }

            // Handle skill usage with right-click
            _skillController?.HandleRightClickSkillUsage();

            // Handle blending editor activation with left mouse click + "/" key
            if (!IsMouseInputConsumedThisFrame &&
                currentKeyboardState.IsKeyDown(Keys.OemQuestion) && // "/" key
                MuGame.Instance.Mouse.LeftButton == ButtonState.Pressed &&
                MuGame.Instance.PrevMouseState.LeftButton == ButtonState.Released)
            {
                _objectEditorController?.HandleBlendingEditorActivation();
                SetMouseInputConsumed();
            }

            // Handle object deletion with left mouse click + DEL key
            if (!IsMouseInputConsumedThisFrame &&
                currentKeyboardState.IsKeyDown(Keys.Delete) && // DEL key
                MuGame.Instance.Mouse.LeftButton == ButtonState.Pressed &&
                MuGame.Instance.PrevMouseState.LeftButton == ButtonState.Released)
            {
                _objectEditorController?.HandleObjectDeletion();
                SetMouseInputConsumed();
            }

            _inputController?.HandleChatLogInput(currentKeyboardState);

            // Update ping every 5 seconds to reduce network overhead
            _pingTimer += gameTime.ElapsedGameTime.TotalSeconds;
            if (_pingTimer >= 5.0)
            {
                _pingTimer = 0;
                _ = UpdatePingAsync();
            }
        }

        // ─────────────────────────── Draw Loop ───────────────────────────
        public override void Draw(GameTime gameTime)
        {
            if (_mapController?.IsChangingWorld == true || World == null || World.Status != GameControlStatus.Ready)
            {
                GraphicsDevice.Clear(new Color(12, 12, 20));
                DrawBackground();
                var loading = _mapController?.LoadingScreen;
                _progressBar.Progress = loading?.Progress ?? 0f;
                _progressBar.StatusText = loading?.Message ?? "Loading...";
                _progressBar.Visible = true;
                _progressBar.Draw(gameTime);
                return;
            }

            using (new SpriteBatchScope(
                       GraphicsManager.Instance.Sprite,
                       SpriteSortMode.Deferred,
                       BlendState.AlphaBlend,
                       SamplerState.PointClamp,
                       DepthStencilState.None,
                       transform: UiScaler.SpriteTransform))
            {
                for (int i = 0; i < Controls.Count; i++)
                {
                    var ctrl = Controls[i];
                    if (ctrl == null || ctrl == World || !ctrl.Visible)
                    {
                        continue;
                    }

                    ctrl.Draw(gameTime);
                }

            }

            base.Draw(gameTime);

            // Final top-most pass: draw dragged item previews above all UI windows
            using (new SpriteBatchScope(
                       GraphicsManager.Instance.Sprite,
                       SpriteSortMode.Deferred,
                       BlendState.AlphaBlend,
                       SamplerState.PointClamp,
                       DepthStencilState.None,
                       transform: UiScaler.SpriteTransform))
            {
                var sprite = GraphicsManager.Instance.Sprite;
                _inventoryControl?._pickedItemRenderer?.Draw(sprite, gameTime);
                VaultControl.Instance?.DrawPickedPreview(sprite, gameTime);
                ChaosMixControl.Instance?.DrawPickedPreview(sprite, gameTime);
                TradeControl.Instance?.DrawPickedPreview(sprite, gameTime);
            }
            _characterInfoWindow?.BringToFront();
        }

        private new void DrawBackground()
        {
            if (_backgroundTexture == null) return;

            using var scope = new SpriteBatchScope(
                GraphicsManager.Instance.Sprite, SpriteSortMode.Deferred,
                BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone,
                null, UiScaler.SpriteTransform);

            GraphicsManager.Instance.Sprite.Draw(_backgroundTexture,
                new Rectangle(0, 0, UiScaler.VirtualSize.X, UiScaler.VirtualSize.Y), Color.White);
        }


        private void PreloadSounds()
        {
            SoundController.Instance.PreloadSound("Sound/pDropItem.wav");
            SoundController.Instance.PreloadSound("Sound/pDropMoney.wav");
            SoundController.Instance.PreloadSound("Sound/mGem.wav");
            SoundController.Instance.PreloadSound("Sound/pGetItem.wav");
            SoundController.Instance.PreloadSound("Sound/pWalk(Grass).wav");
            SoundController.Instance.PreloadSound("Sound/pWalk(Snow).wav");
            SoundController.Instance.PreloadSound("Sound/pWalk(Soil).wav");
            SoundController.Instance.PreloadSound("Sound/pSwim.wav");
        }

        private async Task UpdatePingAsync()
        {
            if (MuGame.Network == null)
                return;

            int? ping = await MuGame.Network.PingServerAsync();
            MuGame.ScheduleOnMainThread(() =>
            {
                if (_pingLabel == null)
                    return;

                if (ping == _lastPingValue)
                    return;

                _lastPingValue = ping;
                _pingLabel.Text = ping.HasValue ? $"Ping: {ping.Value} ms" : "Ping: --";
            });
        }

        private void StartWhisperToPlayer(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName) || _chatInput == null)
            {
                return;
            }

            _chatInput.StartWhisperTo(playerName);
        }

        internal void SetWorldInternal(WorldControl world)
        {
            World = world;
        }

        public override void Dispose()
        {
            if (_hero != null)
            {
                if (_windowCloseController != null)
                {
                    _hero.PlayerMoved -= _windowCloseController.OnHeroMoved;
                    _hero.PlayerTookDamage -= _windowCloseController.OnHeroTookDamage;
                }
            }
            base.Dispose();
        }
    }
}
