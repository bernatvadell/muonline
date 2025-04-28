using Client.Main.Controllers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Client.Main.Controls.UI
{
    public class ChatInputBoxControl : UIControl
    {
        private const int CHATBOX_WIDTH = 281;
        public const int CHATBOX_HEIGHT = 47;
        private const int BUTTON_WIDTH = 27;
        private const int BUTTON_HEIGHT = 26;
        private const int GROUP_SEPARATING_WIDTH = 6;
        private const int INPUT_MESSAGE_TYPE_COUNT = 4;

        // Button X positions
        private const int INPUT_TYPE_START_X = 0;
        private const int BLOCK_WHISPER_START_X = INPUT_MESSAGE_TYPE_COUNT * BUTTON_WIDTH + GROUP_SEPARATING_WIDTH; // 4 * 27 + 6 = 114
        private const int SYSTEM_ON_START_X = BLOCK_WHISPER_START_X + BUTTON_WIDTH; // 114 + 27 = 141
        private const int CHATLOG_ON_START_X = SYSTEM_ON_START_X + BUTTON_WIDTH; // 141 + 27 = 168
        private const int FRAME_ON_START_X = CHATLOG_ON_START_X + BUTTON_WIDTH + GROUP_SEPARATING_WIDTH; // 168 + 27 + 6 = 201
        private const int FRAME_RESIZE_START_X = FRAME_ON_START_X + BUTTON_WIDTH; // 201 + 27 = 228
        private const int TRANSPARENCY_START_X = FRAME_RESIZE_START_X + BUTTON_WIDTH; // 228 + 27 = 255

        private const int MAX_CHAT_HISTORY = 12;
        private const int MAX_WHISPER_HISTORY = 5;

        // --- Child Controls ---
        private TextureControl _background;
        private TextFieldControl _chatInput;
        private TextFieldControl _whisperIdInput;
        private SpriteControl[] _typeButtons = new SpriteControl[4]; // Normal, Party, Guild, Gens
        private SpriteControl _whisperToggleButton;
        private SpriteControl _systemToggleButton;
        private SpriteControl _chatLogToggleButton;
        private SpriteControl _frameToggleButton;
        private SpriteControl _sizeButton;
        private SpriteControl _transparencyButton;

        // --- State ---
        private InputMessageType _currentInputType = InputMessageType.Chat;
        private bool _isWhisperLocked = false; // Corresponds to m_bBlockWhisper
        private bool _isWhisperSendMode = false; // Corresponds to m_bWhisperSend (true = show ID box)
        private ChatLogWindow _chatLogWindowRef; // Reference to the chat log

        // --- History ---
        private List<string> _chatHistory = new List<string>();
        private List<string> _whisperIdHistory = new List<string>();
        private int _currentChatHistoryIndex = 0;
        private int _currentWhisperHistoryIndex = 0;

        // --- Properties ---
        public InputMessageType CurrentInputType => _currentInputType;
        public bool IsWhisperLocked => _isWhisperLocked;

        // Added cooldown for chat messages based on C++
        private const long ChatCooldownMs = 1000; // 1 Second
        private long _lastChatTime = 0;


        public ChatInputBoxControl(ChatLogWindow chatLogWindow)
        {
            _chatLogWindowRef = chatLogWindow ?? throw new ArgumentNullException(nameof(chatLogWindow));
            AutoViewSize = false;
            ViewSize = new Point(CHATBOX_WIDTH, CHATBOX_HEIGHT);
            ControlSize = ViewSize;
            Visible = false; // Start hidden
            Interactive = true; // Needs mouse interaction
        }

        public override async Task Load()
        {
            // 1. Background
            _background = new TextureControl
            {
                TexturePath = "Interface/newui_chat_back.jpg",
                BlendState = BlendState.AlphaBlend, // Assuming JPG might need alpha blend if it has transparency layer, otherwise Opaque
                ViewSize = ViewSize,
                AutoViewSize = false
            };
            Controls.Add(_background);

            // 2. Text Input Fields
            _chatInput = new TextFieldControl
            {
                X = 72,
                Y = 30,
                ViewSize = new Point(176, 14), // Width adjusted slightly based on C++
                FontSize = 10f, // Adjust as needed
                BackgroundColor = Color.Black * 0.1f,
                TextColor = new Color(230, 210, 255)
            };
            _whisperIdInput = new TextFieldControl
            {
                X = 5,
                Y = 30,
                ViewSize = new Point(60, 14), // Width adjusted slightly
                FontSize = 10f,
                BackgroundColor = Color.Black * 0.1f,
                TextColor = new Color(200, 200, 200, 255),
                Visible = false // Start hidden
            };
            Controls.Add(_chatInput);
            Controls.Add(_whisperIdInput);

            // --- Buttons --------------------------------------------------------------

            // Type Buttons (Normal, Party, Guild, Gens)
            string[] typeTexPaths =
            {
                "Interface/newui_chat_normal_on.jpg",
                "Interface/newui_chat_party_on.jpg",
                "Interface/newui_chat_guild_on.jpg",
                "Interface/newui_chat_gens_on.jpg"
            };
            for (int i = 0; i < _typeButtons.Length; i++)
            {
                _typeButtons[i] = CreateButton(INPUT_TYPE_START_X + i * BUTTON_WIDTH, 0,
                                               typeTexPaths[i], $"TypeBtn_{i}");
                int typeIdx = i;
                _typeButtons[i].Click += (s, e) =>
                {
                    SetInputType((InputMessageType)typeIdx);
                    SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav"); // Play sound on click
                };
                Controls.Add(_typeButtons[i]);
            }

            // Whisper-Lock
            _whisperToggleButton = CreateButton(BLOCK_WHISPER_START_X, 0,
                                                "Interface/newui_chat_whisper_on.jpg", "WhisperToggle");
            _whisperToggleButton.Click += (s, e) =>
            {
                ToggleWhisperLock();
                SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav"); // Play sound on click
            };
            Controls.Add(_whisperToggleButton);

            // System-Messages ON/OFF
            _systemToggleButton = CreateButton(SYSTEM_ON_START_X, 0,
                                               "Interface/newui_chat_system_on.jpg", "SystemToggle");
            _systemToggleButton.Click += (s, e) =>
            {
                ToggleSystemMessages();
                SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav"); // Play sound on click
            };
            Controls.Add(_systemToggleButton);

            // Chat-Log ON/OFF
            _chatLogToggleButton = CreateButton(CHATLOG_ON_START_X, 0,
                                                "Interface/newui_chat_chat_on.jpg", "ChatLogToggle");
            _chatLogToggleButton.Click += (s, e) =>
            {
                ToggleChatLogVisibility();
                SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav"); // Play sound on click
            };
            Controls.Add(_chatLogToggleButton);

            // Show / hide frame (scrollbar, resize etc.)
            _frameToggleButton = CreateButton(FRAME_ON_START_X, 0,
                                              "Interface/newui_chat_frame_on.jpg", "FrameToggle");
            _frameToggleButton.Click += (s, e) =>
            {
                _chatLogWindowRef?.ToggleFrame();
                SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav"); // Play sound on click
            };
            Controls.Add(_frameToggleButton);

            // Size-cycle (F4)
            _sizeButton = CreateButton(FRAME_RESIZE_START_X, 0,
                                       "Interface/newui_chat_btn_size.jpg", "SizeButton");
            _sizeButton.Click += (s, e) =>
            {
                _chatLogWindowRef?.CycleSize();
                SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav"); // Play sound on click
            };
            Controls.Add(_sizeButton);

            // Transparency-cycle
            _transparencyButton = CreateButton(TRANSPARENCY_START_X, 0,
                                               "Interface/newui_chat_btn_alpha.jpg", "AlphaButton");
            _transparencyButton.Click += (s, e) =>
            {
                _chatLogWindowRef?.CycleBackgroundAlpha();
                SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav"); // Play sound on click
            };
            Controls.Add(_transparencyButton);

            // Load textures for all children
            await base.Load(); // This initializes children, including loading their textures

            // Initial visual state update
            UpdateVisualStates();
        }

        private SpriteControl CreateButton(int x, int y, string texturePath, string name)
        {
            return new SpriteControl
            {
                X = x,
                Y = y,
                TexturePath = texturePath,
                TileWidth = BUTTON_WIDTH,
                TileHeight = BUTTON_HEIGHT,
                ViewSize = new Point(BUTTON_WIDTH, BUTTON_HEIGHT),
                BlendState = BlendState.AlphaBlend, // Use AlphaBlend for JPG/TGA with potential transparency
                Interactive = true,
                Name = name,
                Visible = false // Start hidden, shown based on parent state
            };
        }

        public void Show()
        {
            Visible = true;
            _chatInput.Visible = true;
            _whisperIdInput.Visible = _isWhisperSendMode;
            foreach (var btn in GetAllButtons()) btn.Visible = true;

            _chatInput.Value = string.Empty; // Clear text on show

            _chatInput.Focus();
            _chatInput.MoveCursorToEnd();

            // Reset history navigation
            _currentChatHistoryIndex = _chatHistory.Count;
            _currentWhisperHistoryIndex = _whisperIdHistory.Count;

            UpdateVisualStates();

            // Play sound on opening
            SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
        }

        public void Hide()
        {
            Visible = false;
            _chatInput.Visible = false;
            _whisperIdInput.Visible = false;
            foreach (var btn in GetAllButtons()) btn.Visible = false;

            if (Scene.FocusControl == _chatInput || Scene.FocusControl == _whisperIdInput)
            {
                Scene.FocusControl = null; // Remove focus
            }

            // Play sound on closing
            SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
        }

        public override void Update(GameTime gameTime)
        {
            if (!Visible) return;

            base.Update(gameTime); // Update children (buttons, text fields)

            HandleKeyboardInput();
            UpdateVisualStates(); // Keep visual state consistent
        }

        private void HandleKeyboardInput()
        {
            bool chatFocus = Scene.FocusControl == _chatInput;
            bool whisperFocus = Scene.FocusControl == _whisperIdInput && _whisperIdInput.Visible;

            if (!chatFocus && !whisperFocus) return; // Only process if one of our inputs has focus

            var keyboard = MuGame.Instance.Keyboard;
            var prevKeyboard = MuGame.Instance.PrevKeyboard;

            // --- Enter Key ---
            if (keyboard.IsKeyDown(Keys.Enter) && prevKeyboard.IsKeyUp(Keys.Enter))
            {
                // Check cooldown before processing chat
                long currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (currentTime - _lastChatTime >= ChatCooldownMs)
                {
                    _lastChatTime = currentTime;
                    ProcessEnterKey();
                }
                // Don't consume the event, let the base scene handle focus changes if needed
            }
            // --- Escape Key ---
            else if (keyboard.IsKeyDown(Keys.Escape) && prevKeyboard.IsKeyUp(Keys.Escape))
            {
                Hide();
                // Don't consume, might be used elsewhere
            }
            // --- Tab Key ---
            else if (keyboard.IsKeyDown(Keys.Tab) && prevKeyboard.IsKeyUp(Keys.Tab))
            {
                if (_isWhisperSendMode)
                {
                    if (chatFocus)
                    {
                        _chatInput.Blur();
                        _whisperIdInput.Focus();
                        Scene.FocusControl = _whisperIdInput;
                        _whisperIdInput.MoveCursorToEnd();
                    }
                    else if (whisperFocus)
                    {
                        _whisperIdInput.Blur();
                        _chatInput.Focus();
                        Scene.FocusControl = _chatInput;
                        _chatInput.MoveCursorToEnd();
                    }
                }
                // Consume Tab
            }
            // --- Up/Down Arrows (History) ---
            else if (keyboard.IsKeyDown(Keys.Up) && prevKeyboard.IsKeyUp(Keys.Up))
            {
                NavigateHistory(-1);
                // Consume Up arrow
            }
            else if (keyboard.IsKeyDown(Keys.Down) && prevKeyboard.IsKeyUp(Keys.Down))
            {
                NavigateHistory(1);
                // Consume Down arrow
            }
            // --- F-Keys (like C++) ---
            else if (keyboard.IsKeyDown(Keys.F3) && prevKeyboard.IsKeyUp(Keys.F3))
            {
                ToggleWhisperSendMode();
                SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav"); // Play sound on F3
            }
            else if (keyboard.IsKeyDown(Keys.F4) && prevKeyboard.IsKeyUp(Keys.F4))
            {
                // C++ plays sound only if frame is visible
                if (_chatLogWindowRef?.IsFrameVisible ?? false)
                {
                    _chatLogWindowRef?.CycleSize();
                    SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav"); // Play sound on F4
                }
            }
            else if (keyboard.IsKeyDown(Keys.F5) && prevKeyboard.IsKeyUp(Keys.F5))
            {
                _chatLogWindowRef?.ToggleFrame();
                SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav"); // Play sound on F5
            }
            // F2 is handled by GameScene interacting with ChatLogWindow directly
        }

        private void ProcessEnterKey()
        {
            string messageText = _chatInput.Value.Trim();
            string whisperTarget = _whisperIdInput.Value.Trim();

            if (string.IsNullOrEmpty(messageText))
            {
                Hide(); // Hide if no message entered
                return;
            }

            // --- Determine Message Type and Prefix ---
            MessageType finalType = MessageType.Chat; // Default
            string prefix = "";
            string sender = "You"; // Placeholder for local display

            // TODO: Implement command checking like C++ CheckCommand(szChatText)
            // If it's a command, process it and return without sending a message
            // e.g., if (CheckCommand(messageText)) { Hide(); return; }

            if (_isWhisperSendMode && !string.IsNullOrEmpty(whisperTarget))
            {
                finalType = MessageType.Whisper;
                // Simulate sending whisper
                _chatLogWindowRef?.AddMessage(sender, $"->[{whisperTarget}]: {messageText}", finalType);
                AddWhisperIdHistory(whisperTarget);
                // TODO: Add actual network send call here
                Console.WriteLine($"[Chat] SEND WHISPER To '{whisperTarget}': {messageText}");
            }
            else
            {
                // Check for explicit prefixes like C++
                if (messageText.StartsWith("~"))
                {
                    finalType = MessageType.Party;
                    messageText = messageText.Substring(1).TrimStart();
                }
                else if (messageText.StartsWith("@"))
                {
                    finalType = MessageType.Guild;
                    messageText = messageText.Substring(1).TrimStart();
                }
                else if (messageText.StartsWith("$"))
                {
                    finalType = MessageType.Gens;
                    messageText = messageText.Substring(1).TrimStart();
                }
                else
                {
                    // Default type based on button state
                    finalType = _currentInputType switch
                    {
                        InputMessageType.Party => MessageType.Party,
                        InputMessageType.Guild => MessageType.Guild,
                        InputMessageType.Gens => MessageType.Gens,
                        _ => MessageType.Chat, // Default to Chat if button is Normal
                    };
                }


                // Simulate sending public/group chat
                _chatLogWindowRef?.AddMessage(sender, messageText, finalType);
                // TODO: Add actual network send call here (with prefix if needed)
                Console.WriteLine($"[Chat] SEND {finalType}: {messageText}"); // Log without prefix for clarity
            }

            // Add to chat history (only the message text, not the prefix)
            AddChatHistory(messageText);

            // Clear input and hide
            _chatInput.Value = "";
            Hide();
        }


        private void NavigateHistory(int direction)
        {
            bool chatFocus = Scene.FocusControl == _chatInput;
            bool whisperFocus = Scene.FocusControl == _whisperIdInput && _whisperIdInput.Visible;

            if (chatFocus && _chatHistory.Count > 0)
            {
                _currentChatHistoryIndex = Math.Clamp(_currentChatHistoryIndex + direction, 0, _chatHistory.Count);
                _chatInput.Value = (_currentChatHistoryIndex < _chatHistory.Count) ? _chatHistory[_currentChatHistoryIndex] : "";
                _chatInput.MoveCursorToEnd();
            }
            else if (whisperFocus && _whisperIdHistory.Count > 0)
            {
                _currentWhisperHistoryIndex = Math.Clamp(_currentWhisperHistoryIndex + direction, 0, _whisperIdHistory.Count);
                _whisperIdInput.Value = (_currentWhisperHistoryIndex < _whisperIdHistory.Count) ? _whisperIdHistory[_currentWhisperHistoryIndex] : "";
                _whisperIdInput.MoveCursorToEnd();
            }
        }

        private void AddChatHistory(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            _chatHistory.Remove(text); // Remove duplicates
            _chatHistory.Add(text);
            if (_chatHistory.Count > MAX_CHAT_HISTORY)
            {
                _chatHistory.RemoveAt(0);
            }
            _currentChatHistoryIndex = _chatHistory.Count; // Reset index to bottom
        }

        private void AddWhisperIdHistory(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            _whisperIdHistory.Remove(id); // Remove duplicates
            _whisperIdHistory.Add(id);
            if (_whisperIdHistory.Count > MAX_WHISPER_HISTORY)
            {
                _whisperIdHistory.RemoveAt(0);
            }
            _currentWhisperHistoryIndex = _whisperIdHistory.Count; // Reset index to bottom
        }

        private void SetInputType(InputMessageType type)
        {
            if (_currentInputType != type)
            {
                _currentInputType = type;
                UpdateVisualStates();
                Console.WriteLine($"[Chat] Input type set to: {type}");
            }
        }

        private void ToggleWhisperLock()
        {
            _isWhisperLocked = !_isWhisperLocked;
            UpdateVisualStates();
            Console.WriteLine($"[Chat] Whisper Lock: {_isWhisperLocked}");
        }

        private void ToggleWhisperSendMode()
        {
            _isWhisperSendMode = !_isWhisperSendMode;
            _whisperIdInput.Visible = _isWhisperSendMode;

            if (_isWhisperSendMode && Visible)
            {
                _chatInput.Blur();

                _whisperIdInput.Focus();
                Scene.FocusControl = _whisperIdInput;

                _whisperIdInput.MoveCursorToEnd();
            }
            else if (!_isWhisperSendMode && Visible)
            {
                _whisperIdInput.Blur();

                _chatInput.Focus();
                Scene.FocusControl = _chatInput;

                _chatInput.MoveCursorToEnd();
            }

            Console.WriteLine($"[Chat] Whisper Send Mode: {_isWhisperSendMode}");
        }

        private void ToggleSystemMessages()
        {
            // This state is managed by ChatLogWindow now, but we keep the button visual update
            bool newState = !(_chatLogWindowRef?.IsSysMsgVisible ?? true); // Example toggle logic
            _chatLogWindowRef?.ShowSystemMessages(newState); // Hypothetical method
            UpdateVisualStates();
            Console.WriteLine($"[Chat] System Messages Visible: {newState}");
        }

        private void ToggleChatLogVisibility()
        {
            // This state is managed by ChatLogWindow
            bool newState = !(_chatLogWindowRef?.IsChatLogVisible ?? true); // Example toggle logic
            _chatLogWindowRef?.ShowChatLogMessages(newState); // Hypothetical method
            UpdateVisualStates();
            Console.WriteLine($"[Chat] Chat Log Visible: {newState}");
        }

        private void UpdateVisualStates()
        {
            if (!Visible) return;

            for (int i = 0; i < _typeButtons.Length; i++)
                _typeButtons[i].Visible = true;

            _whisperToggleButton.Visible = true;
            _systemToggleButton.Visible = true;
            _chatLogToggleButton.Visible = true;
            _frameToggleButton.Visible = true;

            bool showFrameButtons = _chatLogWindowRef?.IsFrameVisible ?? false;
            _sizeButton.Visible = showFrameButtons;
            _transparencyButton.Visible = showFrameButtons;

            _whisperIdInput.Visible = _isWhisperSendMode;
        }

        // Helper to get all buttons for visibility toggling
        private IEnumerable<SpriteControl> GetAllButtons()
        {
            foreach (var btn in _typeButtons) yield return btn;
            yield return _whisperToggleButton;
            yield return _systemToggleButton;
            yield return _chatLogToggleButton;
            yield return _frameToggleButton;
            yield return _sizeButton;
            yield return _transparencyButton;
        }

        private static void DrawDisabledOverlay(SpriteControl btn)
        {
            var sb = GraphicsManager.Instance.Sprite;
            sb.Draw(GraphicsManager.Instance.Pixel, btn.DisplayRectangle, Color.Black * 0.55f);
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible) return;

            base.Draw(gameTime);

            var sb = GraphicsManager.Instance.Sprite;
            sb.Begin();

            for (int i = 0; i < _typeButtons.Length; i++)
                if (i != (int)_currentInputType)
                    DrawDisabledOverlay(_typeButtons[i]);

            if (!_isWhisperLocked) DrawDisabledOverlay(_whisperToggleButton);
            if (!(_chatLogWindowRef?.IsSysMsgVisible ?? true)) DrawDisabledOverlay(_systemToggleButton);
            if (!(_chatLogWindowRef?.IsChatLogVisible ?? true)) DrawDisabledOverlay(_chatLogToggleButton);
            if (!(_chatLogWindowRef?.IsFrameVisible ?? false)) DrawDisabledOverlay(_frameToggleButton);

            sb.End();

            if (!_isWhisperSendMode && _whisperIdInput != null)
            {
                sb = GraphicsManager.Instance.Sprite;
                sb.Begin();
                sb.Draw(GraphicsManager.Instance.Pixel, _whisperIdInput.DisplayRectangle, Color.Black * 0.5f);
                sb.End();
            }
        }
    }
}