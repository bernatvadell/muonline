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
using System.Threading.Tasks;
using Client.Main.Client;
using System.Diagnostics;
using MUnique.OpenMU.Network.Packets;
using System.Collections.Generic; // For CharacterClassNumber

namespace Client.Main.Scenes
{
    public class GameScene : BaseScene
    {
        // *** REMOVE _characterState - no longer needed here ***
        // private readonly CharacterState _characterState;

        private readonly PlayerObject _hero = new();
        private readonly MainControl _main;
        private WorldControl _nextWorld;
        private LoadingScreenControl _loadingScreen;
        private MapListControl _mapListControl;
        private bool _isChangingWorld = false;
        private ChatLogWindow _chatLog;
        private ChatInputBoxControl _chatInput;
        // private MiniMapControl _miniMap;
        private KeyboardState _previousKeyboardState;

        // *** ADD FIELD FOR PASSED CHARACTER INFO ***
        private readonly (string Name, CharacterClassNumber Class, ushort Level) _characterInfo;

        public PlayerObject Hero => _hero;

        private static readonly Dictionary<byte, Type> MapWorldRegistry = new()
        {
            { 0, typeof(LorenciaWorld) },
            { 1, typeof(DungeonWorld) },
            { 2, typeof(DeviasWorld) },
            { 3, typeof(NoriaWorld) },
            // Dodawaj kolejne mapy według mapId
        };

        // *** MODIFY CONSTRUCTOR TO ACCEPT CHARACTER INFO ***
        public GameScene((string Name, CharacterClassNumber Class, ushort Level) characterInfo)
        {
            _characterInfo = characterInfo; // Store the passed info
            // _characterState = MuGame.Network?.GetCharacterState() ?? throw new InvalidOperationException("CharacterState not available."); // No longer needed here

            Debug.WriteLine($"GameScene constructor called for Character: {_characterInfo.Name} ({_characterInfo.Class})");

            _main = new MainControl(MuGame.Network.GetCharacterState());
            Controls.Add(_main);
            Controls.Add(NpcShopControl.Instance);
            _mapListControl = new MapListControl { Visible = false };

            // --- CHAT ---
            _chatLog = new ChatLogWindow();
            _chatLog.X = 5;
            _chatLog.Y = MuGame.Instance.Height - 160 - ChatInputBoxControl.CHATBOX_HEIGHT;
            Controls.Add(_chatLog);

            _chatInput = new ChatInputBoxControl(_chatLog);
            _chatInput.X = 5;
            _chatInput.Y = MuGame.Instance.Height - 65 - ChatInputBoxControl.CHATBOX_HEIGHT;
            Controls.Add(_chatInput);
            // --- CHAT END ---

            _loadingScreen = new LoadingScreenControl { Visible = true };
            Controls.Add(_loadingScreen);

            // Ensure UI elements are drawn in the correct order
            _chatInput.BringToFront();
            DebugPanel.BringToFront();
            Cursor.BringToFront();
        }

        /* ───────────────────────── ADD THIS ───────────────────────── */
        /// <summary>
        /// Required by MuGame.ChangeScene&lt;T&gt;().  
        /// Pulls the selected-character data from the CharacterState that
        /// NetworkManager keeps up-to-date.
        /// </summary>
        public GameScene()
            : this(GetCharacterInfoFromState()) { }

        private static (string, CharacterClassNumber, ushort) GetCharacterInfoFromState()
        {
            var state = MuGame.Network?.GetCharacterState();     // adjust to your actual API
            if (state != null)
            {
                return (state.Name ?? "Unknown",
                        state.Class,
                        state.Level);
            }

            // Fallback – the scene will still load, you can correct it later.
            return ("Unknown", CharacterClassNumber.DarkKnight, 1);
        }
        /* ──────────────────────────────────────────────────────────── */
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
            for (int i = 0; i < 10; i++) // Add more messages to test scrolling
            {
                _chatLog.AddMessage("Spammer", $"Test message number {i + 1} to fill the chat window and check how scrolling works.", MessageType.Chat);
            }
            _chatLog.AddMessage(string.Empty, "An unexpected error has occurred.", MessageType.Error);
        }

        public override async Task Load()
        {
            await base.Load();

            /* 1) dane postaci */
            _hero.CharacterClass = _characterInfo.Class;
            _hero.Name = _characterInfo.Name;

            /* 2) pozycja z serwera */
            var state = MuGame.Network?.GetCharacterState();
            if (state != null)
            {
                _hero.Location = new Vector2(state.PositionX, state.PositionY);
                Debug.WriteLine($"GameScene.Load: Spawn pos from server -> ({state.PositionX},{state.PositionY})");
            }

            if (state != null && MapWorldRegistry.TryGetValue((byte)state.MapId, out var worldType))
            {
                await ChangeMap(worldType);
            }
            else
            {
                Debug.WriteLine($"Unknown MapId: {state?.MapId}. Defaulting to Lorencia.");
                await ChangeMap(typeof(LorenciaWorld));
            }

            /* 4) upewnij się, że model gracza jest wczytany */
            if (_hero.Status is GameControlStatus.NonInitialized or GameControlStatus.Initializing)
                await _hero.Load();

            AddChatTestData();
        }

        private async Task ChangeMap(Type worldType)
        {
            _isChangingWorld = true;

            if (_loadingScreen == null)
            {
                _loadingScreen = new LoadingScreenControl
                {
                    Visible = true          // pokaż od razu
                };
                Controls.Add(_loadingScreen); // doda => wywoła Initialize/Load
            }
            else
            {
                _loadingScreen.Visible = true;
            }

            _loadingScreen.Message = $"Loading {worldType.Name}…";

            _main.Visible = false;

            // Tworzymy instancję nowego świata dynamicznie
            // POPRAWNA wersja – bez parametrów konstruktora
            _nextWorld = (WorldControl?)Activator.CreateInstance(worldType)
                         ?? throw new InvalidOperationException($"Cannot create world: {worldType}");

            if (_nextWorld is WalkableWorldControl walkable)
                walkable.Walker = _hero;   // przypisujemy tuż po stworzeniu
            _nextWorld.Objects.Add(_hero);

            Debug.WriteLine($"ChangeMap<{worldType.Name}>: Added _hero to world objects.");
            await _nextWorld.Initialize();

            World?.Dispose();
            World = _nextWorld;
            _nextWorld = null;
            Controls.Insert(0, World);

            Controls.Remove(_loadingScreen);
            _loadingScreen = null;

            _main.Visible = true;
            _isChangingWorld = false;

            if (World.Name != null)
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
        }

        // ChangeMap remains the same, as _hero configuration happens before it now
        public async Task ChangeMap<T>() where T : WalkableWorldControl, new()
        {
            _isChangingWorld = true;

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
            // _miniMap?.Hide(); // Hide minimap during load

            // --- _hero already has the correct class set in GameScene.Load ---
            _nextWorld = new T();
            if (_nextWorld is WalkableWorldControl walkable)
                walkable.Walker = _hero;
            _nextWorld.Objects.Add(_hero); // Add _hero BEFORE Initialize
            Debug.WriteLine($"ChangeMap<{typeof(T).Name}>: Added _hero (Class: {_hero.CharacterClass}) to world objects.");
            await _nextWorld.Initialize(); // Initialize should call Load on _hero
            Debug.WriteLine($"ChangeMap<{typeof(T).Name}>: World initialized. _hero status: {_hero.Status}");

            World?.Dispose();
            World = _nextWorld;
            _nextWorld = null;
            Controls.Insert(0, World);
            // _hero.Reset(); // Reset movement state if needed

            // Load minimap content for the new world
            // if (_miniMap != null)
            // {
            //     await _miniMap.LoadContentForWorld(World.WorldIndex);
            // }

            Controls.Remove(_loadingScreen);
            _loadingScreen = null;

            _main.Visible = true;
            _isChangingWorld = false;

            if (World.Name != null)
            {
                var mapNameControl = new MapNameControl();
                mapNameControl.LabelText = World.Name;
                Controls.Add(mapNameControl);
                mapNameControl.BringToFront(); // Bring map name potentially over other UI
                // Ensure essential UI is still on top
                _chatLog.BringToFront();
                _chatInput.BringToFront();
                // _miniMap?.BringToFront();
                _mapListControl?.BringToFront();
                DebugPanel.BringToFront();
                Cursor.BringToFront();
            }
        }

        // Update and Draw methods remain the same
        public override void Update(GameTime gameTime)
        {
            if (_isChangingWorld)
            {
                _loadingScreen?.Update(gameTime);
                return;
            }

            KeyboardState currentKeyboardState = Keyboard.GetState();

            bool uiHasFocus = FocusControl != null && FocusControl != World; // Check if any UI element *other than the world* has focus
                                                                             // --- Chat Input Toggle ---
            if (!uiHasFocus && !_chatInput.Visible && currentKeyboardState.IsKeyDown(Keys.Enter) && !_previousKeyboardState.IsKeyDown(Keys.Enter))
            {
                _chatInput.Show();
            }
            // --- Map List Toggle ---
            else if (currentKeyboardState.IsKeyDown(Keys.M) && !_previousKeyboardState.IsKeyDown(Keys.M))
            {
                bool newVisibility = !_mapListControl.Visible;
                _mapListControl.Visible = newVisibility;
                if (newVisibility)
                {
                    if (!Controls.Contains(_mapListControl))
                    {
                        Controls.Add(_mapListControl);
                        _mapListControl.BringToFront(); // Ensure it's visible
                                                        // Also bring other top elements over it if needed
                                                        // _miniMap?.BringToFront();
                        DebugPanel.BringToFront();
                        Cursor.BringToFront();
                    }
                }
                else
                {
                    Controls.Remove(_mapListControl);
                }
            }
            // --- Minimap Toggle --- //
            // else if (currentKeyboardState.IsKeyDown(Keys.Tab) && !_previousKeyboardState.IsKeyDown(Keys.Tab))
            // {
            //     if (_miniMap != null)
            //     {
            //         if (_miniMap.Visible)
            //             _miniMap.Hide();
            //         else
            //             _miniMap.Show();
            //     }
            // }

            if (World == null || World.Status != GameControlStatus.Ready)
                return;

            base.Update(gameTime);

            KeyboardState kbd = Keyboard.GetState();
            KeyboardState prevKbd = MuGame.Instance.PrevKeyboard;

            // Przełączanie ramki (F5)
            if (kbd.IsKeyDown(Keys.F5) && prevKbd.IsKeyUp(Keys.F5))
            {
                _chatLog?.ToggleFrame();
            }
            // Zmiana rozmiaru (F4)
            if (kbd.IsKeyDown(Keys.F4) && prevKbd.IsKeyUp(Keys.F4))
            {
                _chatLog?.CycleSize();
            }
            // Zmiana alpha (np. F6 - nie ma tego w C++)
            if (kbd.IsKeyDown(Keys.F6) && prevKbd.IsKeyUp(Keys.F6))
            {
                _chatLog?.CycleBackgroundAlpha();
            }
            // Przełączanie typu widoku (np. F2)
            if (kbd.IsKeyDown(Keys.F2) && prevKbd.IsKeyUp(Keys.F2))
            {
                if (_chatLog != null)
                {
                    MessageType nextType = _chatLog.CurrentViewType + 1;
                    if (!System.Enum.IsDefined(typeof(MessageType), nextType) || nextType == MessageType.Unknown)
                    {
                        nextType = MessageType.All; // Wróć do początku
                    }
                    if (nextType == MessageType.Info) nextType++; // Pomiń Info jeśli nie chcesz go w cyklu
                    if (nextType == MessageType.Error) nextType++; // Pomiń Error
                    _chatLog.ChangeViewType(nextType);
                    Console.WriteLine($"[ChatLog] Zmieniono widok na: {nextType}");
                }
            }
            // Przewijanie (PageUp/PageDown)
            if (_chatLog != null && _chatLog.IsFrameVisible)
            {
                int scrollDelta = 0;
                // PageUp -> chcemy zwiększyć offset (przewinąć w górę) -> delta dodatnia
                if (kbd.IsKeyDown(Keys.PageUp) && prevKbd.IsKeyUp(Keys.PageUp))
                    scrollDelta = _chatLog.NumberOfShowingLines;
                // PageDown -> chcemy zmniejszyć offset (przewinąć w dół) -> delta ujemna
                if (kbd.IsKeyDown(Keys.PageDown) && prevKbd.IsKeyUp(Keys.PageDown))
                    scrollDelta = -_chatLog.NumberOfShowingLines;

                if (scrollDelta != 0)
                {
                    _chatLog.ScrollLines(scrollDelta); // Przekaż poprawną deltę
                }
            }

            _previousKeyboardState = currentKeyboardState;
        }

        public override void Draw(GameTime gameTime)
        {
            if (_isChangingWorld)
            {
                _loadingScreen?.Draw(gameTime);
                return;
            }

            if (World == null || World.Status != GameControlStatus.Ready)
            {
                _loadingScreen?.Draw(gameTime);
                return;
            }

            base.Draw(gameTime);
        }
    }
}