using Client.Main.Controls.UI;
using Client.Main.Controls.UI.Game;
using Client.Main.Controllers;
using Client.Main.Models;
using Microsoft.Xna.Framework.Input;
using Client.Main.Controls.UI.Game.PauseMenu;
using Client.Main.Controls.UI.Game.Inventory;
using Client.Main.Controls.UI.Game.Character;

namespace Client.Main.Scenes
{
    internal sealed class GameSceneInputController
    {
        private static readonly Keys[] MoveCommandKeys = { Keys.Up, Keys.Down, Keys.Left, Keys.Right, Keys.Enter, Keys.Escape };

        private readonly GameScene _scene;
        private readonly PauseMenuControl _pauseMenu;
        private readonly GameScenePlayerMenuController _playerMenuController;
        private readonly MoveCommandWindow _moveCommandWindow;
        private readonly InventoryControl _inventoryControl;
        private readonly CharacterInfoWindowControl _characterInfoWindow;
        private readonly ChatInputBoxControl _chatInput;
        private readonly ChatLogWindow _chatLog;

        private KeyboardState _previousKeyboardState;

        public GameSceneInputController(
            GameScene scene,
            PauseMenuControl pauseMenu,
            GameScenePlayerMenuController playerMenuController,
            MoveCommandWindow moveCommandWindow,
            InventoryControl inventoryControl,
            CharacterInfoWindowControl characterInfoWindow,
            ChatInputBoxControl chatInput,
            ChatLogWindow chatLog)
        {
            _scene = scene;
            _pauseMenu = pauseMenu;
            _playerMenuController = playerMenuController;
            _moveCommandWindow = moveCommandWindow;
            _inventoryControl = inventoryControl;
            _characterInfoWindow = characterInfoWindow;
            _chatInput = chatInput;
            _chatLog = chatLog;
        }

        public void HandleGlobalInput(KeyboardState currentKeyboardState)
        {
            bool escapePressed = currentKeyboardState.IsKeyDown(Keys.Escape) && _previousKeyboardState.IsKeyUp(Keys.Escape);

            // Toggle pause menu on ESC (edge-triggered), unless a UI control consumed Escape.
            if (escapePressed && !_scene.IsKeyboardEscapeConsumedThisFrame)
            {
                if (_playerMenuController?.IsMenuVisible == true)
                {
                    _playerMenuController.HideMenu();
                }
                else if (_pauseMenu != null)
                {
                    _pauseMenu.Visible = !_pauseMenu.Visible;
                    if (_pauseMenu.Visible)
                        _pauseMenu.BringToFront();
                }
            }

            if (_scene.FocusControl == _moveCommandWindow && _moveCommandWindow.Visible)
            {
                // Only check relevant keys for move command window instead of all keys
                foreach (Keys key in MoveCommandKeys)
                {
                    if (currentKeyboardState.IsKeyDown(key) && _previousKeyboardState.IsKeyUp(key))
                    {
                        _moveCommandWindow.ProcessKeyInput(key, false);
                    }
                }
            }

            // Determine if any UI element that captures typing has focus.
            bool isUiInputActive =
                (_scene.FocusControl is TextFieldControl)
                || (_scene.FocusControl == _moveCommandWindow && _moveCommandWindow.Visible)
                || (_pauseMenu != null && _pauseMenu.Visible);

            // Process global hotkeys ONLY if a UI input element is NOT active.
            if (!isUiInputActive)
            {
                if (currentKeyboardState.IsKeyDown(Keys.I) && !_previousKeyboardState.IsKeyDown(Keys.I))
                {
                    bool wasVisible = _inventoryControl.Visible;
                    if (wasVisible)
                        _inventoryControl.Hide();
                    else
                        _inventoryControl.Show();

                    // Play window open sound only when opening (not closing)
                    if (!wasVisible)
                        SoundController.Instance.PlayBuffer("Sound/iCreateWindow.wav");
                }
                if (currentKeyboardState.IsKeyDown(Keys.C) && !_previousKeyboardState.IsKeyDown(Keys.C))
                {
                    if (_characterInfoWindow != null)
                    {
                        bool wasVisible = _characterInfoWindow.Visible;
                        if (wasVisible)
                            _characterInfoWindow.HideWindow();
                        else
                            _characterInfoWindow.ShowWindow();

                        // Play window open sound only when opening (not closing)
                        if (!wasVisible)
                            SoundController.Instance.PlayBuffer("Sound/iCreateWindow.wav");
                    }
                }
                if (currentKeyboardState.IsKeyDown(Keys.M) && !_previousKeyboardState.IsKeyDown(Keys.M))
                {
                    if (!NpcShopControl.Instance.Visible)
                    {
                        _moveCommandWindow.ToggleVisibility();
                    }
                }

                // Handle opening the chat window if it's not focused and Enter is pressed.
                if (!_scene.IsKeyboardEnterConsumedThisFrame && !_chatInput.Visible &&
                    currentKeyboardState.IsKeyDown(Keys.Enter) && !_previousKeyboardState.IsKeyDown(Keys.Enter))
                {
                    _chatInput.Show();
                }
            }
        }

        public void HandleChatLogInput(KeyboardState currentKeyboardState)
        {
            if (currentKeyboardState.IsKeyDown(Keys.F5) && _previousKeyboardState.IsKeyUp(Keys.F5))
                _chatLog?.ToggleFrame();
            if (currentKeyboardState.IsKeyDown(Keys.F4) && _previousKeyboardState.IsKeyUp(Keys.F4))
                _chatLog?.CycleSize();
            if (currentKeyboardState.IsKeyDown(Keys.F6) && _previousKeyboardState.IsKeyUp(Keys.F6))
                _chatLog?.CycleBackgroundAlpha();
            if (currentKeyboardState.IsKeyDown(Keys.F2) && _previousKeyboardState.IsKeyUp(Keys.F2))
            {
                if (_chatLog != null)
                {
                    var nextType = _chatLog.CurrentViewType + 1;
                    if (!System.Enum.IsDefined(typeof(MessageType), nextType) || nextType == MessageType.Unknown) nextType = MessageType.All;
                    if (nextType == MessageType.Info || nextType == MessageType.Error) nextType++;
                    if (!System.Enum.IsDefined(typeof(MessageType), nextType) || nextType == MessageType.Unknown) nextType = MessageType.All;
                    _chatLog.ChangeViewType(nextType);
                    System.Console.WriteLine($"[ChatLog] Changed view to: {nextType}");
                }
            }
            if (_chatLog != null && _chatLog.IsFrameVisible)
            {
                int scrollDelta = 0;
                if (currentKeyboardState.IsKeyDown(Keys.PageUp) && _previousKeyboardState.IsKeyUp(Keys.PageUp)) scrollDelta = _chatLog.NumberOfShowingLines;
                if (currentKeyboardState.IsKeyDown(Keys.PageDown) && _previousKeyboardState.IsKeyUp(Keys.PageDown)) scrollDelta = -_chatLog.NumberOfShowingLines;
                if (scrollDelta != 0) _chatLog.ScrollLines(scrollDelta);
            }

            UpdatePreviousKeyboardState(currentKeyboardState);
        }

        public void UpdatePreviousKeyboardState(KeyboardState currentKeyboardState)
        {
            _previousKeyboardState = currentKeyboardState;
        }
    }
}
