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
        private WorldControl _nextWorld; // Used for map changes
        private LoadingScreenControl _loadingScreen; // For initial load and map changes
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
        public GameScene((string Name, CharacterClassNumber Class, ushort Level) characterInfo)
        {
            _characterInfo = characterInfo;
            Debug.WriteLine($"GameScene constructor called for Character: {_characterInfo.Name} ({_characterInfo.Class})");

            _main = new MainControl(MuGame.Network.GetCharacterState());
            Controls.Add(_main);
            Controls.Add(NpcShopControl.Instance);

            _mapListControl = new MapListControl { Visible = false };

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
            _notificationManager.BringToFront();

            // Initialize LoadingScreenControl here to show progress
            _loadingScreen = new LoadingScreenControl { Visible = true, Message = "Loading Game..." };
            Controls.Add(_loadingScreen);
            _loadingScreen.BringToFront(); // Ensure it's on top

            _chatInput.BringToFront();
            DebugPanel.BringToFront();
            Cursor.BringToFront();
        }

        public GameScene() : this(GetCharacterInfoFromState()) { }

        private static (string Name, CharacterClassNumber Class, ushort Level) GetCharacterInfoFromState()
        {
            var state = MuGame.Network?.GetCharacterState();
            if (state != null)
            {
                return (state.Name ?? "Unknown", state.Class, state.Level);
            }
            return ("Unknown", CharacterClassNumber.DarkKnight, 1);
        }

        // ───────────────────── Content Loading (Progressive) ─────────────────────
        private void UpdateLoadProgress(string message, float progress)
        {
            MuGame.ScheduleOnMainThread(() => // Ensure UI updates are on the main thread
            {
                if (_loadingScreen != null)
                {
                    _loadingScreen.Message = message;
                    _loadingScreen.Progress = progress;
                }
            });
        }

        // This method is now part of the new progress reporting system
        protected override async Task LoadSceneContentWithProgress(Action<string, float> progressCallback)
        {
            // We'll use the GameScene's own UpdateLoadProgress which updates its _loadingScreen.
            // The progressCallback from BaseScene can be ignored or used for higher-level reporting if needed.

            UpdateLoadProgress("Initializing Game Scene...", 0.0f);

            // 1. Hero Setup (Quick)
            UpdateLoadProgress("Setting up hero...", 0.1f);
            _hero.CharacterClass = _characterInfo.Class;
            _hero.Name = _characterInfo.Name;

            var charState = MuGame.Network?.GetCharacterState();
            if (charState != null)
            {
                _hero.NetworkId = charState.Id;
                _hero.Location = new Vector2(charState.PositionX, charState.PositionY);
                Debug.WriteLine($"GameScene.Load: Spawn position from server -> ({charState.PositionX}, {charState.PositionY})");
            }
            UpdateLoadProgress("Hero setup complete.", 0.2f);

            // 2. Determine Initial World (Quick)
            UpdateLoadProgress("Determining initial world...", 0.25f);
            Type initialWorldType = typeof(LorenciaWorld); // Default
            if (charState != null && MapWorldRegistry.TryGetValue((byte)charState.MapId, out var mappedType))
            {
                initialWorldType = mappedType;
            }
            else
            {
                Debug.WriteLine($"GameScene.Load: Unknown MapId: {charState?.MapId}. Defaulting to Lorencia.");
            }
            UpdateLoadProgress($"Initial world: {initialWorldType.Name}.", 0.3f);

            // 3. Instantiate and Initialize World (Potentially long)
            UpdateLoadProgress($"Loading world: {initialWorldType.Name}...", 0.35f);

            // Dispose previous world if any (important for ChangeMap, less so for initial load but good practice)
            if (World != null)
            {
                Controls.Remove(World);
                World.Dispose();
                World = null;
            }

            var worldInstance = (WorldControl)Activator.CreateInstance(initialWorldType);
            if (worldInstance is WalkableWorldControl walkable)
                walkable.Walker = _hero;

            Controls.Add(worldInstance);
            World = worldInstance;
            World.Objects.Add(_hero);

            await worldInstance.Initialize();
            UpdateLoadProgress($"World {initialWorldType.Name} initialized.", 0.7f);

            // 4. Load Hero Assets (Potentially long)
            UpdateLoadProgress("Loading hero model and equipment...", 0.75f);
            if (_hero.Status == GameControlStatus.NonInitialized || _hero.Status == GameControlStatus.Initializing)
            {
                await _hero.Load();
            }
            UpdateLoadProgress("Hero loaded.", 0.85f);

            // 5. Import Pending Objects (Quick, but depends on network state)
            UpdateLoadProgress("Importing nearby players and monsters...", 0.90f);
            if (World.Status == GameControlStatus.Ready)
            {
                await ImportPendingRemotePlayers();
                await ImportPendingNpcsMonsters();
            }
            else
            {
                Debug.WriteLine($"GameScene.Load: World not ready after Initialize (Status: {World.Status}). Pending objects may not import correctly.");
            }
            UpdateLoadProgress("Objects imported.", 0.95f);

            // Finalize: Hide loading screen, show main UI
            if (_loadingScreen != null)
            {
                Controls.Remove(_loadingScreen);
                _loadingScreen.Dispose();
                _loadingScreen = null;
            }
            _main.Visible = true;
            UpdateLoadProgress("Game ready!", 1.0f); // Signals completion
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
                Debug.WriteLine("GameScene.Load() called outside of InitializeWithProgressReporting flow. Consider refactoring.");
                await base.Load(); // Which is empty in BaseScene, then calls derived GameScene's old Load logic
            }
        }


        // ─────────────────── Map Change Logic (Remains largely the same) ───────────────────
        public async Task ChangeMap(Type worldType)
        {
            _isChangingWorld = true;

            if (_loadingScreen == null) // Recreate if disposed, or handle if just hidden
            {
                _loadingScreen = new LoadingScreenControl { Visible = true };
                Controls.Add(_loadingScreen);
                _loadingScreen.BringToFront();
            }
            else
            {
                _loadingScreen.Visible = true;
            }
            _loadingScreen.Message = $"Loading {worldType.Name}…";
            _loadingScreen.Progress = 0f; // Reset progress for map change
            _main.Visible = false;

            var previousWorld = World;
            _nextWorld = (WorldControl)Activator.CreateInstance(worldType)
                ?? throw new InvalidOperationException($"Cannot create world: {worldType}");

            if (_nextWorld is WalkableWorldControl walkable)
                walkable.Walker = _hero;

            // Add to controls and set World property BEFORE Initialize.
            Controls.Add(_nextWorld);
            World = _nextWorld; // Temporarily set to new world for its Initialize
            World.Objects.Add(_hero); // Add hero to new world

            _loadingScreen.Progress = 0.1f;
            Debug.WriteLine($"GameScene.ChangeMap<{worldType.Name}>: Initializing new world...");
            await _nextWorld.Initialize();
            Debug.WriteLine($"GameScene.ChangeMap<{worldType.Name}>: New world initialized. Status: {_nextWorld.Status}");
            _loadingScreen.Progress = 0.7f;

            if (previousWorld != null)
            {
                Controls.Remove(previousWorld);
                previousWorld.Dispose();
                Debug.WriteLine("GameScene.ChangeMap: Disposed previous world.");
            }

            _nextWorld = null;

            if (World.Status == GameControlStatus.Ready)
            {
                _loadingScreen.Progress = 0.8f;
                Debug.WriteLine("GameScene.ChangeMap: World is Ready. Importing pending objects...");
                await ImportPendingRemotePlayers();
                await ImportPendingNpcsMonsters();
            }
            else
            {
                Debug.WriteLine($"GameScene.ChangeMap: World not ready after Initialize (Status: {World.Status}). Pending objects may not import correctly.");
            }
            _loadingScreen.Progress = 0.95f;

            Controls.Remove(_loadingScreen);
            _loadingScreen.Dispose();
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
            Debug.WriteLine($"GameScene.ChangeMap<{worldType.Name}>: ChangeMap completed.");
        }

        public async Task ChangeMap<T>() where T : WalkableWorldControl, new()
        {
            await ChangeMap(typeof(T));
        }

        // ──────────────────── Debug & Test Methods ────────────────────
        private void AddChatTestData()
        {
            if (_chatLog == null) return;
            _chatLog.AddMessage(string.Empty, "Welcome to the world of Mu!", MessageType.System);
            _chatLog.AddMessage("Player1", "Hello everyone!", MessageType.Chat);
            _chatLog.AddMessage("System", "Server will restart in 5 minutes.", MessageType.Info);
            _chatLog.AddMessage("GM_Tester", "Please report for an interview.", MessageType.GM);
            _chatLog.AddMessage("AllyMember", "Shall we go for the boss?", MessageType.Guild);
            _chatLog.AddMessage("PartyDude", "I've got a spot!", MessageType.Party);
            _chatLog.AddMessage("Whisperer", "Shall we meet in Lorencia?", MessageType.Whisper);
            for (int i = 0; i < 10; i++) _chatLog.AddMessage("Spammer", $"Test message number {i + 1}.", MessageType.Chat);
            _chatLog.AddMessage(string.Empty, "An unexpected error has occurred.", MessageType.Error);
        }

        // ─────────────────── Import NPCs & Monsters ───────────────────
        private async Task ImportPendingNpcsMonsters()
        {
            if (World is not WalkableWorldControl w) return;
            var list = ScopeHandler.TakePendingNpcsMonsters();
            if (!list.Any()) return;
            foreach (var s in list)
            {
                if (w.Objects.OfType<WalkerObject>().Any(p => p.NetworkId == s.Id)) continue;
                if (!NpcDatabase.TryGetNpcType(s.TypeNumber, out Type objectType)) continue;
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
            if (World is not WalkableWorldControl w) return;
            var list = ScopeHandler.TakePendingPlayers();
            foreach (var s in list)
            {
                if (s.Id == MuGame.Network.GetCharacterState().Id) continue;
                if (w.Objects.OfType<PlayerObject>().Any(p => p.NetworkId == s.Id)) continue;
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
        public void ShowNotificationMessage(ServerMessage.MessageType messageType, string message)
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                lock (_pendingNotifications) { _pendingNotifications.Add((messageType, message)); }
            });
        }

        private void ProcessPendingNotifications()
        {
            if (_notificationManager == null) return;
            List<(ServerMessage.MessageType Type, string Message)> currentBatch;
            lock (_pendingNotifications)
            {
                if (!_pendingNotifications.Any()) return;
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
                    Controls.OfType<ChatLogWindow>().FirstOrDefault()?.AddMessage(string.Empty, pending.Message, Models.MessageType.System);
                }
                if (pending.Type == ServerMessage.MessageType.BlueNormal)
                {
                    Controls.OfType<ChatLogWindow>().FirstOrDefault()?.AddMessage(string.Empty, pending.Message, Models.MessageType.System);
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

            if (!uiHasFocus && !_chatInput.Visible && currentKeyboardState.IsKeyDown(Keys.Enter) && !_previousKeyboardState.IsKeyDown(Keys.Enter))
            {
                _chatInput.Show();
            }
            else if (currentKeyboardState.IsKeyDown(Keys.M) && !_previousKeyboardState.IsKeyDown(Keys.M))
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

            if (currentKeyboardState.IsKeyDown(Keys.F5) && _previousKeyboardState.IsKeyUp(Keys.F5)) _chatLog?.ToggleFrame();
            if (currentKeyboardState.IsKeyDown(Keys.F4) && _previousKeyboardState.IsKeyUp(Keys.F4)) _chatLog?.CycleSize();
            if (currentKeyboardState.IsKeyDown(Keys.F6) && _previousKeyboardState.IsKeyUp(Keys.F6)) _chatLog?.CycleBackgroundAlpha();
            if (currentKeyboardState.IsKeyDown(Keys.F2) && _previousKeyboardState.IsKeyUp(Keys.F2))
            {
                if (_chatLog != null)
                {
                    var nextType = _chatLog.CurrentViewType + 1;
                    if (!System.Enum.IsDefined(typeof(Models.MessageType), nextType) || nextType == Models.MessageType.Unknown) nextType = Models.MessageType.All;
                    if (nextType == Models.MessageType.Info || nextType == Models.MessageType.Error) nextType++;
                    if (!System.Enum.IsDefined(typeof(Models.MessageType), nextType) || nextType == Models.MessageType.Unknown) nextType = Models.MessageType.All;
                    _chatLog.ChangeViewType(nextType);
                    Console.WriteLine($"[ChatLog] Changed view to: {nextType}");
                }
            }
            if (_chatLog != null && _chatLog.IsFrameVisible)
            {
                int scrollDelta = 0;
                if (currentKeyboardState.IsKeyDown(Keys.PageUp) && _previousKeyboardState.IsKeyUp(Keys.PageUp)) scrollDelta = _chatLog.NumberOfShowingLines;
                if (currentKeyboardState.IsKeyDown(Keys.PageDown) && _previousKeyboardState.IsKeyUp(Keys.PageDown)) scrollDelta = -_chatLog.NumberOfShowingLines;
                if (scrollDelta != 0) _chatLog.ScrollLines(scrollDelta);
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