using Client.Main.Controllers;
using Client.Main.Core.Client;
using Client.Main.Models;
using Microsoft.Extensions.Logging;
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
        // Fields
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

        // Child Controls
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

        // State
        private InputMessageType _currentInputType = InputMessageType.Chat;
        private bool _isWhisperLocked = false; // Corresponds to m_bBlockWhisper
        private bool _isWhisperSendMode = true; // Corresponds to m_bWhisperSend (true = show ID box)
        private bool _suppressNextEnter;
        private ChatLogWindow _chatLogWindowRef; // Reference to the chat log

        // History
        private List<string> _chatHistory = new List<string>();
        private List<string> _whisperIdHistory = new List<string>();
        private int _currentChatHistoryIndex = 0;
        private int _currentWhisperHistoryIndex = 0;
        private MessageType finalType;

        // Cooldown for chat messages
        private const long ChatCooldownMs = 1000; // 1 Second
        private long _lastChatTime = 0;

        private readonly ILogger<ChatInputBoxControl> _logger;

        // Properties
        public InputMessageType CurrentInputType => _currentInputType;
        public bool IsWhisperLocked => _isWhisperLocked;

        // Constructors
        public ChatInputBoxControl(ChatLogWindow chatLogWindow, ILoggerFactory loggerFactory)
        {
            if (loggerFactory == null) throw new ArgumentNullException(nameof(loggerFactory));
            _logger = loggerFactory.CreateLogger<ChatInputBoxControl>();

            _chatLogWindowRef = chatLogWindow ?? throw new ArgumentNullException(nameof(chatLogWindow));
            AutoViewSize = false;
            ViewSize = new Point(CHATBOX_WIDTH, CHATBOX_HEIGHT);
            ControlSize = ViewSize;
            Visible = false; // Start hidden.
            Interactive = true; // Needs mouse interaction.
        }

        // Methods
        public override async Task Load()
        {
            // 1. Background
            _background = new TextureControl
            {
                TexturePath = "Interface/newui_chat_back.jpg",
                BlendState = BlendState.AlphaBlend, // Assuming JPG might need alpha blend if it has a transparency layer, otherwise Opaque.
                ViewSize = ViewSize,
                AutoViewSize = false
            };
            Controls.Add(_background);

            // 2. Text Input Fields
            _chatInput = new TextFieldControl
            {
                X = 72,
                Y = 30,
                ViewSize = new Point(176, 14), // Width adjusted slightly.
                FontSize = 10f, // Adjust as needed.
                BackgroundColor = Color.Black * 0.1f,
                TextColor = new Color(230, 210, 255)
            };
            _whisperIdInput = new TextFieldControl
            {
                X = 5,
                Y = 30,
                ViewSize = new Point(60, 14), // Width adjusted slightly.
                FontSize = 10f,
                BackgroundColor = Color.Black * 0.1f,
                TextColor = new Color(200, 200, 200, 255),
                Visible = false // Start hidden.
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
                    SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav"); // Play sound on click.
                };
                Controls.Add(_typeButtons[i]);
            }

            // Whisper-Lock
            _whisperToggleButton = CreateButton(BLOCK_WHISPER_START_X, 0,
                                                "Interface/newui_chat_whisper_on.jpg", "WhisperToggle");
            _whisperToggleButton.Click += (s, e) =>
            {
                ToggleWhisperLock();
                SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav"); // Play sound on click.
            };
            Controls.Add(_whisperToggleButton);

            // System-Messages ON/OFF
            _systemToggleButton = CreateButton(SYSTEM_ON_START_X, 0,
                                               "Interface/newui_chat_system_on.jpg", "SystemToggle");
            _systemToggleButton.Click += (s, e) =>
            {
                ToggleSystemMessages();
                SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav"); // Play sound on click.
            };
            Controls.Add(_systemToggleButton);

            // Chat-Log ON/OFF
            _chatLogToggleButton = CreateButton(CHATLOG_ON_START_X, 0,
                                                "Interface/newui_chat_chat_on.jpg", "ChatLogToggle");
            _chatLogToggleButton.Click += (s, e) =>
            {
                ToggleChatLogVisibility();
                SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav"); // Play sound on click.
            };
            Controls.Add(_chatLogToggleButton);

            // Show / hide frame (scrollbar, resize etc.)
            _frameToggleButton = CreateButton(FRAME_ON_START_X, 0,
                                              "Interface/newui_chat_frame_on.jpg", "FrameToggle");
            _frameToggleButton.Click += (s, e) =>
            {
                _chatLogWindowRef.ToggleFrame();
                SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav"); // Play sound on click.
            };
            Controls.Add(_frameToggleButton);

            // Size-cycle (F4)
            _sizeButton = CreateButton(FRAME_RESIZE_START_X, 0,
                                       "Interface/newui_chat_btn_size.jpg", "SizeButton");
            _sizeButton.Click += (s, e) =>
            {
                _chatLogWindowRef.CycleSize();
                SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav"); // Play sound on click.
            };
            Controls.Add(_sizeButton);

            // Transparency-cycle
            _transparencyButton = CreateButton(TRANSPARENCY_START_X, 0,
                                               "Interface/newui_chat_btn_alpha.jpg", "AlphaButton");
            _transparencyButton.Click += (s, e) =>
            {
                _chatLogWindowRef.CycleBackgroundAlpha();
                SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav"); // Play sound on click.
            };
            Controls.Add(_transparencyButton);

            // Subscribe to EnterKeyPressed event
            _chatInput.EnterKeyPressed += (s, e) =>
            {
                if (_suppressNextEnter)
                {
                    _suppressNextEnter = false; // consume the suppression flag first

                    // If after consuming suppression, the input field is truly empty (and whisper not active or empty)
                    // then this Enter press (which was originally to open the chat) should now close it.
                    if (string.IsNullOrEmpty(_chatInput.Value.Trim()) &&
                        (!_isWhisperSendMode || string.IsNullOrEmpty(_whisperIdInput.Value.Trim())))
                    {
                        Hide();
                        if (Scene != null) Scene.ConsumeKeyboardEnter(); // explicitly consume
                    }
                    // If text was entered after opening and before this Enter, do nothing; suppression worked.
                }
                else
                {
                    // Normal Enter press, not suppressed. Let ProcessEnterKey decide to send or hide.
                    ProcessEnterKey();
                }
            };

            // Load textures for all children.
            await base.Load(); // This initializes children, including loading their textures.

            // Initial visual state update.
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
                BlendState = BlendState.AlphaBlend, // Use AlphaBlend for JPG/TGA with potential transparency.
                Interactive = true,
                Name = name,
                Visible = false // Start hidden, shown based on parent's state.
            };
        }

        public void Show()
        {
            Visible = true;
            _chatInput.Visible = true;
            _whisperIdInput.Visible = _isWhisperSendMode;

            _suppressNextEnter = true;
            foreach (var btn in GetAllButtons()) btn.Visible = true;

            _chatInput.Value = string.Empty; // Clear text on show.
            _chatInput.Focus();
            Scene.FocusControl = _chatInput;
            _chatInput.MoveCursorToEnd();

            // Reset history navigation.
            _currentChatHistoryIndex = _chatHistory.Count;
            _currentWhisperHistoryIndex = _whisperIdHistory.Count;

            UpdateVisualStates();

            // Play sound on opening.
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
                Scene.FocusControl = null; // Remove focus.
            }

            // Play sound on closing.
            SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
        }

        public override void Update(GameTime gameTime)
        {
            if (!Visible) return;

            base.Update(gameTime); // Update children (buttons, text fields).
            HandleKeyboardInput();
            UpdateVisualStates(); // Keep visual state consistent.
        }

        private void HandleKeyboardInput()
        {
            if (!Visible) return;

            var keyboard = MuGame.Instance.Keyboard;
            var prevKeyboard = MuGame.Instance.PrevKeyboard;

            // Handle Enter key FIRST (with suppression of the first Enter after Show) ---
            // This allows Enter to close an empty chat even if the container has focus.
            if (keyboard.IsKeyDown(Keys.Enter) && prevKeyboard.IsKeyUp(Keys.Enter))
            {
                if (_suppressNextEnter)
                {
                    _suppressNextEnter = false;
                }
                else
                {
                    ProcessEnterKey();
                    // After Enter, usually focus is lost or window closes, so further key processing might not be needed.
                    // However, if ProcessEnterKey decided not to close, other keys might still be relevant.
                    // For safety, we can return here if Enter was the action.
                    return;
                }
            }

            // --- Handle Escape key to hide the chat box ---
            if (keyboard.IsKeyDown(Keys.Escape) && prevKeyboard.IsKeyUp(Keys.Escape))
            {
                Hide();
                return;
            }

            // proceed with other inputs only if input fields have focus.
            bool chatFocus = Scene.FocusControl == _chatInput;
            bool whisperFocus = Scene.FocusControl == _whisperIdInput && _whisperIdInput.Visible;

            if (!chatFocus && !whisperFocus) // only proceed if one of the text fields has focus
                return; // neither text field has focus, don't process Tab, Up, Down, F-keys related to chat functionality

            // --- Tab key to switch focus between input fields ---
            if (keyboard.IsKeyDown(Keys.Tab) && prevKeyboard.IsKeyUp(Keys.Tab))
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
            }
            // --- Navigate message history ---
            else if (keyboard.IsKeyDown(Keys.Up) && prevKeyboard.IsKeyUp(Keys.Up))
            {
                NavigateHistory(-1);
            }
            else if (keyboard.IsKeyDown(Keys.Down) && prevKeyboard.IsKeyUp(Keys.Down))
            {
                NavigateHistory(1);
            }
            // --- Toggle whisper mode ---
            else if (keyboard.IsKeyDown(Keys.F3) && prevKeyboard.IsKeyUp(Keys.F3))
            {
                ToggleWhisperSendMode();
                SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
            }
            // --- Cycle chat size ---
            else if (keyboard.IsKeyDown(Keys.F4) && prevKeyboard.IsKeyUp(Keys.F4))
            {
                if (_chatLogWindowRef.IsFrameVisible)
                {
                    _chatLogWindowRef.CycleSize();
                    SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
                }
            }
            // --- Toggle chat frame ---
            else if (keyboard.IsKeyDown(Keys.F5) && prevKeyboard.IsKeyUp(Keys.F5))
            {
                _chatLogWindowRef.ToggleFrame();
                SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
            }
        }

        private void ProcessEnterKey()
        {
            long currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (currentTime - _lastChatTime < ChatCooldownMs)
            {
                // Optional: Show a message "Please wait before sending another message."
                // For now, just prevent sending and potentially re-show the input if hidden.
                if (!Visible)
                {
                    Show();
                }
                _logger.LogDebug("Chat cooldown active, message blocked.");
                return;
            }

            string messageText = _chatInput.Value.Trim();
            string whisperTarget = _whisperIdInput.Value.Trim();

            // If both inputs are empty, just hide/show the box.
            if (string.IsNullOrEmpty(messageText) && (string.IsNullOrEmpty(whisperTarget) || !_isWhisperSendMode))
            {
                if (!Visible)
                {
                    Show();
                }
                else
                {
                    Hide();
                    if (Scene != null) Scene.ConsumeKeyboardEnter();
                }
                return;
            }

            // --- Get NetworkManager ---
            var networkManager = MuGame.Network;
            if (networkManager == null || !networkManager.IsConnected || networkManager.CurrentState < ClientConnectionState.InGame)
            {
                _logger.LogWarning("Cannot send chat message. NetworkManager unavailable or not in game.");
                _chatLogWindowRef.AddMessage("System", "Cannot send message: Not connected to game.", MessageType.Error);
                // Optionally hide the input box here too, or leave it open for the user to see the error.
                return;
            }
            // --- End Get NetworkManager ---

            string messageToSend = messageText; // Start with the trimmed message.

            // --- Determine Message Type and Send ---
            Task sendTask = Task.CompletedTask; // Task to await potential network send.

            if (_isWhisperSendMode && !string.IsNullOrEmpty(whisperTarget))
            {
                if (string.IsNullOrEmpty(messageText))
                {
                    _logger.LogWarning("Attempted to send whisper with empty message to {Target}.", whisperTarget);
                    _chatLogWindowRef.AddMessage("System", "Cannot send empty whisper message.", MessageType.Error);
                    return; // Don't send empty whispers.
                }

                finalType = MessageType.Whisper;
                AddWhisperIdHistory(whisperTarget);
                // Send whisper via NetworkManager.
                sendTask = networkManager.SendWhisperMessageAsync(whisperTarget, messageText);
            }
            else // Not whisper mode or no target specified.
            {
                if (string.IsNullOrEmpty(messageText))
                {
                    // Don't send empty public/group messages.
                    Hide(); // Just hide the box if message is empty.
                    if (Scene != null) Scene.ConsumeKeyboardEnter();
                    return;
                }

                // Check for explicit prefixes FIRST.
                if (messageText.StartsWith("~"))
                {
                    finalType = MessageType.Party;
                    // Keep prefix for sending.
                }
                else if (messageText.StartsWith("@"))
                {
                    finalType = MessageType.Guild;
                    // Keep prefix for sending.
                }
                else if (messageText.StartsWith("$")) // Assuming '$' for Gens.
                {
                    finalType = MessageType.Gens;
                    // Keep prefix for sending.
                }
                else // No explicit prefix, use button state.
                {
                    finalType = _currentInputType switch
                    {
                        InputMessageType.Party => MessageType.Party,
                        InputMessageType.Guild => MessageType.Guild,
                        InputMessageType.Gens => MessageType.Gens,
                        _ => MessageType.Chat, // Default to Chat.
                    };

                    // Add prefix based on button state IF NOT normal chat.
                    if (finalType == MessageType.Party) messageToSend = "~" + messageText;
                    else if (finalType == MessageType.Guild) messageToSend = "@" + messageText;
                    else if (finalType == MessageType.Gens) messageToSend = "$" + messageText;
                    // Normal chat (finalType == MessageType.Chat) sends without prefix.
                }
                // Send public/group chat via NetworkManager (with prefix if needed).
                sendTask = networkManager.SendPublicChatMessageAsync(messageToSend);
            }

            // Add to chat history (only the message text, not the prefix).
            AddChatHistory(messageText); // Add original text without prefix.

            // Clear input and hide AFTER ensuring the send task is initiated.
            _chatInput.Value = "";
            Hide();
            if (Scene != null) Scene.ConsumeKeyboardEnter();

            // Update cooldown timer AFTER successful send attempt initiation.
            _lastChatTime = currentTime;

            // Optional: Await the send task if you need confirmation, but usually UI shouldn't block.
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
            _chatHistory.Remove(text); // Remove duplicates.
            _chatHistory.Add(text);
            if (_chatHistory.Count > MAX_CHAT_HISTORY)
            {
                _chatHistory.RemoveAt(0);
            }
            _currentChatHistoryIndex = _chatHistory.Count; // Reset index to bottom.
        }

        private void AddWhisperIdHistory(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            _whisperIdHistory.Remove(id); // Remove duplicates.
            _whisperIdHistory.Add(id);
            if (_whisperIdHistory.Count > MAX_WHISPER_HISTORY)
            {
                _whisperIdHistory.RemoveAt(0);
            }
            _currentWhisperHistoryIndex = _whisperIdHistory.Count; // Reset index to bottom.
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
            // This state is managed by ChatLogWindow, but we keep the button visual update.
            bool newState = !_chatLogWindowRef.IsSysMsgVisible;
            _chatLogWindowRef.ShowSystemMessages(newState);
            UpdateVisualStates();
            Console.WriteLine($"[Chat] System Messages Visible: {newState}");
        }

        private void ToggleChatLogVisibility()
        {
            // This state is managed by ChatLogWindow.
            bool newState = !_chatLogWindowRef.IsChatLogVisible;
            _chatLogWindowRef.ShowChatLogMessages(newState);
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

            bool showFrameButtons = _chatLogWindowRef.IsFrameVisible;
            _sizeButton.Visible = showFrameButtons;
            _transparencyButton.Visible = showFrameButtons;

            _whisperIdInput.Visible = _isWhisperSendMode;
        }

        // Helper to get all buttons for visibility toggling.
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
            if (!Visible)
                return;

            base.Draw(gameTime);

            var sb = GraphicsManager.Instance.Sprite;

            for (int i = 0; i < _typeButtons.Length; i++)
            {
                if (i != (int)_currentInputType)
                    DrawDisabledOverlay(_typeButtons[i]);
            }

            if (!_isWhisperLocked) DrawDisabledOverlay(_whisperToggleButton);
            if (!_chatLogWindowRef.IsSysMsgVisible) DrawDisabledOverlay(_systemToggleButton);
            if (!_chatLogWindowRef.IsChatLogVisible) DrawDisabledOverlay(_chatLogToggleButton);
            if (!_chatLogWindowRef.IsFrameVisible) DrawDisabledOverlay(_frameToggleButton);

            if (!_isWhisperSendMode && _whisperIdInput != null)
            {
                sb.Draw(
                    GraphicsManager.Instance.Pixel,
                    _whisperIdInput.DisplayRectangle,
                    Color.Black * 0.5f);
            }
        }

        public override bool OnClick()
        {
            // If the main chat input box area is clicked (not a button within it),
            // and it's visible, set focus to the chat input field.
            if (Visible && _chatInput != null && _chatInput.Visible)
            {
                _chatInput.Focus(); // this will also set Scene.FocusControl
            }
            return base.OnClick(); // allow base to fire Click event if any subscribers
        }
    }
}