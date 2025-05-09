// GameScene.cs
using Client.Main.Controls;
using Client.Main.Controls.UI;
using Client.Main.Controls.UI.Game;
using Client.Main.Models;
using Client.Main.Objects.Player;
using Client.Main.Worlds;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using MUnique.OpenMU.Network.Packets;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using Client.Main.Objects;
using Client.Main.Core.Utilities;
using Client.Main.Networking.PacketHandling.Handlers; // For CharacterClassNumber

namespace Client.Main.Scenes
{
    public class GameScene : BaseScene
    {
        // ──────────────────────────── Fields ────────────────────────────
        private readonly PlayerObject _hero = new();
        private readonly MainControl _main;
        private WorldControl _nextWorld;
        private LoadingScreenControl _loadingScreen;
        private MapListControl _mapListControl;
        private ChatLogWindow _chatLog;
        private ChatInputBoxControl _chatInput;
        private NotificationManager _notificationManager;
        private readonly (string Name, CharacterClassNumber Class, ushort Level) _characterInfo;
        private KeyboardState _previousKeyboardState;
        private bool _isChangingWorld = false;
        private readonly List<(ServerMessage.MessageType Type, string Message)> _pendingNotifications = new();

        // ───────────────────────── Properties ─────────────────────────
        public PlayerObject Hero => _hero;

        public static readonly IReadOnlyDictionary<byte, Type> MapWorldRegistry = new Dictionary<byte, Type>
        {
            { 0, typeof(LorenciaWorld) },
            { 1, typeof(DungeonWorld) },
            { 2, typeof(DeviasWorld) },
            { 3, typeof(NoriaWorld) },
            // Add additional maps according to mapId
        };

        private static readonly Dictionary<ServerMessage.MessageType, Color> NotificationColors = new Dictionary<ServerMessage.MessageType, Color>
        {
            { ServerMessage.MessageType.GoldenCenter, Color.Goldenrod },
            { ServerMessage.MessageType.BlueNormal,   new Color(100, 150, 255) },
            { ServerMessage.MessageType.GuildNotice,  new Color(144, 238, 144) },
        };

        // ──────────────────────── Constructors ────────────────────────
        /// <summary>
        /// Primary constructor accepting character info.
        /// </summary>
        public GameScene((string Name, CharacterClassNumber Class, ushort Level) characterInfo)
        {
            _characterInfo = characterInfo;
            Debug.WriteLine($"GameScene constructor called for Character: {_characterInfo.Name} ({_characterInfo.Class})");

            _main = new MainControl(MuGame.Network.GetCharacterState());
            Controls.Add(_main);
            Controls.Add(NpcShopControl.Instance);

            _mapListControl = new MapListControl { Visible = false };

            // Chat setup
            _chatLog = new ChatLogWindow
            {
                X = 5,
                Y = MuGame.Instance.Height - 160 - ChatInputBoxControl.CHATBOX_HEIGHT
            };
            Controls.Add(_chatLog);

            _chatInput = new ChatInputBoxControl(_chatLog, MuGame.AppLoggerFactory)
            {
                X = 5,
                Y = MuGame.Instance.Height - 65 - ChatInputBoxControl.CHATBOX_HEIGHT
            };
            Controls.Add(_chatInput);

            _pendingNotifications.AddRange(ChatMessageHandler.TakePendingServerMessages());
            _notificationManager = new NotificationManager();
            Controls.Add(_notificationManager);
            _notificationManager.BringToFront(); // Ensure notifications draw above other UI

            _loadingScreen = new LoadingScreenControl { Visible = true };
            Controls.Add(_loadingScreen);

            // Ensure core UI is on top
            _chatInput.BringToFront();
            DebugPanel.BringToFront();
            Cursor.BringToFront();
        }

        /// <summary>
        /// Overload for ChangeScene: retrieves character data from the network state.
        /// </summary>
        public GameScene()
            : this(GetCharacterInfoFromState())
        {
        }

        private static (string Name, CharacterClassNumber Class, ushort Level) GetCharacterInfoFromState()
        {
            var state = MuGame.Network?.GetCharacterState();
            if (state != null)
            {
                return (state.Name ?? "Unknown", state.Class, state.Level);
            }

            // Fallback if CharacterState is unavailable
            return ("Unknown", CharacterClassNumber.DarkKnight, 1);
        }

        // ───────────────────── Content Loading ─────────────────────
 
        /// <summary>
        /// Loads the scene: sets up the hero, chooses the correct world, initializes it,
        /// and imports pending objects (players, NPCs, monsters).
        /// </summary>
        public override async Task Load()
        {
            await base.Load();

            // Assign character info to hero
            _hero.CharacterClass = _characterInfo.Class;
            _hero.Name = _characterInfo.Name;

            // Retrieve last known state from server
            var state = MuGame.Network?.GetCharacterState();
            if (state != null)
            {
                _hero.NetworkId = state.Id;
                _hero.Location = new Vector2(state.PositionX, state.PositionY);
                Debug.WriteLine($"GameScene.Load: Spawn position from server -> ({state.PositionX}, {state.PositionY})");
            }

            // Determine initial world type
            Type initialWorldType = typeof(LorenciaWorld);
            if (state != null && MapWorldRegistry.TryGetValue((byte)state.MapId, out var mappedType))
            {
                initialWorldType = mappedType;
            }
            else
            {
                Debug.WriteLine($"GameScene.Load: Unknown MapId: {state?.MapId}. Defaulting to Lorencia.");
            }

            // Instantiate and initialize the world
            var worldInstance = (WorldControl)Activator.CreateInstance(initialWorldType);
            if (worldInstance is WalkableWorldControl walkable)
                walkable.Walker = _hero;
            worldInstance.Objects.Add(_hero);

            Controls.Add(worldInstance);
            await worldInstance.Initialize();
            World = worldInstance;

            // Import pending remote players and monsters after world is ready
            if (World.Status == GameControlStatus.Ready)
            {
                await ImportPendingRemotePlayers();
                await ImportPendingNpcsMonsters();
            }
            else
            {
                Debug.WriteLine($"GameScene.Load: World not ready after Initialize (Status: {World.Status}). Pending objects may not import correctly.");
            }

            // Ensure hero is loaded
            if (_hero.Status == GameControlStatus.NonInitialized ||
                _hero.Status == GameControlStatus.Initializing)
            {
                await _hero.Load();
            }

            // Remove loading screen and show main UI
            if (_loadingScreen != null)
            {
                Controls.Remove(_loadingScreen);
                _loadingScreen.Dispose();
                _loadingScreen = null;
            }
            _main.Visible = true;
        }

        // ─────────────────── Map Change Logic ───────────────────

        /// <summary>
        /// Switches the active world to the specified type, showing a loading screen,
        /// disposing the old world, and importing pending objects.
        /// </summary>
        public async Task ChangeMap(Type worldType)
        {
            _isChangingWorld = true;

            // Show or create loading screen
            if (_loadingScreen == null)
            {
                _loadingScreen = new LoadingScreenControl { Visible = true };
                Controls.Add(_loadingScreen);
            }
            else
            {
                _loadingScreen.Visible = true;
            }
            _loadingScreen.Message = $"Loading {worldType.Name}…";
            _main.Visible = false;

            // Prepare new world instance
            var previousWorld = World;
            _nextWorld = (WorldControl)Activator.CreateInstance(worldType)
                ?? throw new InvalidOperationException($"Cannot create world: {worldType}");

            if (_nextWorld is WalkableWorldControl walkable)
                walkable.Walker = _hero;
            _nextWorld.Objects.Add(_hero);

            Debug.WriteLine($"GameScene.ChangeMap<{worldType.Name}>: Initializing new world...");
            await _nextWorld.Initialize();
            Debug.WriteLine($"GameScene.ChangeMap<{worldType.Name}>: New world initialized. Status: {_nextWorld.Status}");

            // Dispose of previous world
            if (previousWorld != null)
            {
                Controls.Remove(previousWorld);
                previousWorld.Dispose();
                Debug.WriteLine("GameScene.ChangeMap: Disposed previous world.");
            }

            // Activate new world
            World = _nextWorld;
            Controls.Insert(0, World);
            _nextWorld = null;

            // Import pending objects if ready
            if (World.Status == GameControlStatus.Ready)
            {
                Debug.WriteLine("GameScene.ChangeMap: World is Ready. Importing pending objects...");
                await ImportPendingRemotePlayers();
                await ImportPendingNpcsMonsters();
            }
            else
            {
                Debug.WriteLine($"GameScene.ChangeMap: World not ready after Initialize (Status: {World.Status}). Pending objects may not import correctly.");
            }

            // Remove loading screen and restore UI
            Controls.Remove(_loadingScreen);
            _loadingScreen = null;
            _main.Visible = true;
            _isChangingWorld = false;

            // Show map name overlay
            if (!string.IsNullOrEmpty(World.Name))
            {
                var mapNameControl = new MapNameControl { LabelText = World.Name };
                Controls.Add(mapNameControl);
                mapNameControl.BringToFront();
                _chatLog.BringToFront();
                _chatInput.BringToFront();
                _mapListControl?.BringToFront();
                DebugPanel.BringToFront();
                Cursor.BringToFront();
            }
            Debug.WriteLine($"GameScene.ChangeMap<{worldType.Name}>: ChangeMap completed.");
        }

        /// <summary>
        /// Generic version: switches to a new WalkableWorldControl of type T.
        /// </summary>
        public async Task ChangeMap<T>() where T : WalkableWorldControl, new()
        {
            _isChangingWorld = true;

            // Show or update loading screen
            if (_loadingScreen == null)
            {
                _loadingScreen = new LoadingScreenControl { Message = "Loading...", Visible = true };
                Controls.Add(_loadingScreen);
            }
            else
            {
                _loadingScreen.Message = "Loading...";
                _loadingScreen.Visible = true;
            }
            _main.Visible = false;

            var previousWorld = World;
            var newWorldInstance = new T();
            newWorldInstance.Walker = _hero;
            newWorldInstance.Objects.Add(_hero);

            Debug.WriteLine($"GameScene.ChangeMap<{typeof(T).Name}>: Initializing new world...");
            await newWorldInstance.Initialize();
            Debug.WriteLine($"GameScene.ChangeMap<{typeof(T).Name}>: New world initialized. Status: {newWorldInstance.Status}");

            if (previousWorld != null)
            {
                Controls.Remove(previousWorld);
                previousWorld.Dispose();
            }

            World = newWorldInstance;
            Controls.Insert(0, World);

            if (World.Status == GameControlStatus.Ready)
            {
                Debug.WriteLine("GameScene.ChangeMap: World is Ready. Importing pending objects...");
                await ImportPendingRemotePlayers();
                await ImportPendingNpcsMonsters();
            }
            else
            {
                Debug.WriteLine($"GameScene.ChangeMap: World not ready after Initialize (Status: {World.Status}). Pending objects may not import correctly.");
            }

            Controls.Remove(_loadingScreen);
            _loadingScreen = null;
            _main.Visible = true;
            _isChangingWorld = false;

            if (!string.IsNullOrEmpty(World.Name))
            {
                var mapNameControl = new MapNameControl { LabelText = World.Name };
                Controls.Add(mapNameControl);
                mapNameControl.BringToFront();
                _chatLog.BringToFront();
                _chatInput.BringToFront();
                _mapListControl?.BringToFront();
                DebugPanel.BringToFront();
                Cursor.BringToFront();
            }
            Debug.WriteLine($"GameScene.ChangeMap<{typeof(T).Name}>: ChangeMap completed.");
        }

        // ──────────────────── Debug & Test Methods ────────────────────
 
        /// <summary>
        /// Adds sample messages to the chat log for testing scrolling.
        /// </summary>
        private void AddChatTestData()
        {
            if (_chatLog == null)
                return;

            _chatLog.AddMessage(string.Empty, "Welcome to the world of Mu!", MessageType.System);
            _chatLog.AddMessage("Player1", "Hello everyone!", MessageType.Chat);
            _chatLog.AddMessage("System", "Server will restart in 5 minutes.", MessageType.Info);
            _chatLog.AddMessage("GM_Tester", "Please report for an interview.", MessageType.GM);
            _chatLog.AddMessage("AllyMember", "Shall we go for the boss?", MessageType.Guild);
            _chatLog.AddMessage("PartyDude", "I've got a spot!", MessageType.Party);
            _chatLog.AddMessage("Whisperer", "Shall we meet in Lorencia?", MessageType.Whisper);

            for (int i = 0; i < 10; i++)
            {
                _chatLog.AddMessage("Spammer", $"Test message number {i + 1}.", MessageType.Chat);
            }

            _chatLog.AddMessage(string.Empty, "An unexpected error has occurred.", MessageType.Error);
        }

        // ─────────────────── Import NPCs & Monsters ───────────────────
        private async Task ImportPendingNpcsMonsters()
        {
            if (World is not WalkableWorldControl w)
                return;

            var list = ScopeHandler.TakePendingNpcsMonsters();
            if (!list.Any())
                return;

            foreach (var s in list)
            {
                if (w.Objects.OfType<WalkerObject>().Any(p => p.NetworkId == s.Id))
                    continue;

                if (!NpcDatabase.TryGetNpcType(s.TypeNumber, out Type objectType))
                    continue;

                if (Activator.CreateInstance(objectType) is WalkerObject npcMonster)
                {
                    npcMonster.NetworkId = s.Id;
                    npcMonster.Location = new Vector2(s.PositionX, s.PositionY);
                    w.Objects.Add(npcMonster);
                    await npcMonster.Load();
                }
            }
        }

        // ─────────────────── Import Remote Players ───────────────────
        private async Task ImportPendingRemotePlayers()
        {
            if (World is not WalkableWorldControl w)
                return;

            var list = ScopeHandler.TakePendingPlayers();
            foreach (var s in list)
            {
                if (s.Id == MuGame.Network.GetCharacterState().Id)
                    continue;

                if (w.Objects.OfType<PlayerObject>().Any(p => p.NetworkId == s.Id))
                    continue;

                var remote = new PlayerObject
                {
                    NetworkId = s.Id,
                    Name = s.Name,
                    CharacterClass = s.Class,
                    Location = new Vector2(s.PositionX, s.PositionY)
                };

                w.Objects.Add(remote);
                await remote.Load();
            }
        }

        // ─────────────────── Notification Handling ───────────────────

        /// <summary>
        /// Queues server messages for UI-thread processing.
        /// </summary>
        public void ShowNotificationMessage(ServerMessage.MessageType messageType, string message)
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                lock (_pendingNotifications)
                {
                    _pendingNotifications.Add((messageType, message));
                }
            });
        }

        /// <summary>
        /// Processes queued notifications and displays them.
        /// </summary>
        private void ProcessPendingNotifications()
        {
            if (_notificationManager == null)
                return;

            List<(ServerMessage.MessageType Type, string Message)> currentBatch;
            lock (_pendingNotifications)
            {
                if (!_pendingNotifications.Any())
                    return;

                currentBatch = _pendingNotifications.ToList();
                _pendingNotifications.Clear();
            }

            foreach (var pending in currentBatch)
            {
                if (NotificationColors.TryGetValue(pending.Type, out Color notificationColor))
                {
                    if (pending.Type != ServerMessage.MessageType.BlueNormal)
                    {
                        _notificationManager.AddNotification(pending.Message, notificationColor);
                    }
                }
                else
                {
                    var chatLog = Controls.OfType<ChatLogWindow>().FirstOrDefault();
                    chatLog?.AddMessage(string.Empty, pending.Message, Models.MessageType.System);
                }

                if (pending.Type == ServerMessage.MessageType.BlueNormal)
                {
                    var chatLog = Controls.OfType<ChatLogWindow>().FirstOrDefault();
                    chatLog?.AddMessage(string.Empty, pending.Message, Models.MessageType.System);
                }
            }
        }

        // ─────────────────────────── Update Loop ───────────────────────────
        public override void Update(GameTime gameTime)
        {
            if (_isChangingWorld)
            {
                _loadingScreen?.Update(gameTime);
                return;
            }

            var currentKeyboardState = Keyboard.GetState();
            bool uiHasFocus = FocusControl != null && FocusControl != World;

            // Toggle chat input
            if (!uiHasFocus && !_chatInput.Visible
                && currentKeyboardState.IsKeyDown(Keys.Enter)
                && !_previousKeyboardState.IsKeyDown(Keys.Enter))
            {
                _chatInput.Show();
            }
            // Toggle map list
            else if (currentKeyboardState.IsKeyDown(Keys.M)
                     && !_previousKeyboardState.IsKeyDown(Keys.M))
            {
                bool newVisibility = !_mapListControl.Visible;
                _mapListControl.Visible = newVisibility;
                if (newVisibility)
                {
                    if (!Controls.Contains(_mapListControl))
                    {
                        Controls.Add(_mapListControl);
                        _mapListControl.BringToFront();
                        DebugPanel.BringToFront();
                        Cursor.BringToFront();
                    }
                }
                else
                {
                    Controls.Remove(_mapListControl);
                }
            }

            _notificationManager?.Update(gameTime);
            ProcessPendingNotifications();

            if (World == null || World.Status != GameControlStatus.Ready)
                return;

            base.Update(gameTime);

            // F5: toggle chat frame
            if (currentKeyboardState.IsKeyDown(Keys.F5) && _previousKeyboardState.IsKeyUp(Keys.F5))
                _chatLog?.ToggleFrame();

            // F4: cycle chat size
            if (currentKeyboardState.IsKeyDown(Keys.F4) && _previousKeyboardState.IsKeyUp(Keys.F4))
                _chatLog?.CycleSize();

            // F6: cycle chat background alpha
            if (currentKeyboardState.IsKeyDown(Keys.F6) && _previousKeyboardState.IsKeyUp(Keys.F6))
                _chatLog?.CycleBackgroundAlpha();

            // F2: cycle chat view type
            if (currentKeyboardState.IsKeyDown(Keys.F2) && _previousKeyboardState.IsKeyUp(Keys.F2))
            {
                if (_chatLog != null)
                {
                    var nextType = _chatLog.CurrentViewType + 1;
                    if (!System.Enum.IsDefined(typeof(Models.MessageType), nextType) || nextType == Models.MessageType.Unknown)
                        nextType = Models.MessageType.All;
                    if (nextType == Models.MessageType.Info || nextType == Models.MessageType.Error)
                        nextType++;
                    if (!System.Enum.IsDefined(typeof(Models.MessageType), nextType) || nextType == Models.MessageType.Unknown)
                        nextType = Models.MessageType.All;

                    _chatLog.ChangeViewType(nextType);
                    Console.WriteLine($"[ChatLog] Changed view to: {nextType}");
                }
            }

            // PageUp/PageDown: scroll chat frame
            if (_chatLog != null && _chatLog.IsFrameVisible)
            {
                int scrollDelta = 0;
                if (currentKeyboardState.IsKeyDown(Keys.PageUp) && _previousKeyboardState.IsKeyUp(Keys.PageUp))
                    scrollDelta = _chatLog.NumberOfShowingLines;
                if (currentKeyboardState.IsKeyDown(Keys.PageDown) && _previousKeyboardState.IsKeyUp(Keys.PageDown))
                    scrollDelta = -_chatLog.NumberOfShowingLines;

                if (scrollDelta != 0)
                    _chatLog.ScrollLines(scrollDelta);
            }

            _previousKeyboardState = currentKeyboardState;
        }

        // ─────────────────────────── Draw Loop ───────────────────────────
        public override void Draw(GameTime gameTime)
        {
            if (_isChangingWorld || World == null || World.Status != GameControlStatus.Ready)
            {
                _loadingScreen?.Draw(gameTime);
                return;
            }

            base.Draw(gameTime);
        }
    }
}
