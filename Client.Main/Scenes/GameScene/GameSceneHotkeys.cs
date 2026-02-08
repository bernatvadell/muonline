using Client.Main.Controls.UI;
using Client.Main.Controls.UI.Game;
using Client.Main.Controllers;
using Client.Main.Models;
using Microsoft.Xna.Framework.Input;
using Client.Main.Controls.UI.Game.PauseMenu;
using Client.Main.Controls.UI.Game.Inventory;
using Client.Main.Controls.UI.Game.Character;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Client.Main.Core.Models;

namespace Client.Main.Scenes
{
    internal sealed class GameSceneHotkeys
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
        private readonly GameSceneObjectEditorController _objectEditorController;
        private readonly ILogger _logger;

        private readonly HotkeySet _global;
        private readonly HotkeySet _inWorld;

        public GameSceneHotkeys(
            GameScene scene,
            PauseMenuControl pauseMenu,
            GameScenePlayerMenuController playerMenuController,
            MoveCommandWindow moveCommandWindow,
            InventoryControl inventoryControl,
            CharacterInfoWindowControl characterInfoWindow,
            ChatInputBoxControl chatInput,
            ChatLogWindow chatLog,
            GameSceneObjectEditorController objectEditorController,
            ILogger logger = null)
        {
            _scene = scene;
            _pauseMenu = pauseMenu;
            _playerMenuController = playerMenuController;
            _moveCommandWindow = moveCommandWindow;
            _inventoryControl = inventoryControl;
            _characterInfoWindow = characterInfoWindow;
            _chatInput = chatInput;
            _chatLog = chatLog;
            _objectEditorController = objectEditorController;
            _logger = logger ?? NullLogger.Instance;

            _global = new HotkeySet(_logger, "GameScene.Global");
            _inWorld = new HotkeySet(_logger, "GameScene.InWorld");

            RegisterGlobalHotkeys(_global);
            RegisterInWorldHotkeys(_inWorld);
        }

        public void HandleGlobal(KeyboardState keyboard, KeyboardState prevKeyboard)
        {
            var context = CreateContext(keyboard, prevKeyboard);
            _global.Execute(context);
        }

        public void HandleInWorld(KeyboardState keyboard, KeyboardState prevKeyboard)
        {
            var context = CreateContext(keyboard, prevKeyboard);
            _inWorld.Execute(context);
        }

        private HotkeyContext CreateContext(KeyboardState keyboard, KeyboardState prevKeyboard)
        {
            bool isUiInputActive =
                (_scene.FocusControl is TextFieldControl)
                || (_scene.FocusControl == _moveCommandWindow && _moveCommandWindow.Visible)
                || (_pauseMenu != null && _pauseMenu.Visible);

            var modifiers = GetModifiers(keyboard);

            return new HotkeyContext(
                _scene,
                _moveCommandWindow,
                _chatLog,
                keyboard,
                prevKeyboard,
                MuGame.Instance.Mouse,
                MuGame.Instance.PrevMouseState,
                modifiers,
                isUiInputActive);
        }

        private static HotkeyModifiers GetModifiers(KeyboardState keyboard)
        {
            HotkeyModifiers modifiers = HotkeyModifiers.None;

            if (keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift))
            {
                modifiers |= HotkeyModifiers.Shift;
            }

            if (keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl))
            {
                modifiers |= HotkeyModifiers.Control;
            }

            if (keyboard.IsKeyDown(Keys.LeftAlt) || keyboard.IsKeyDown(Keys.RightAlt))
            {
                modifiers |= HotkeyModifiers.Alt;
            }

            return modifiers;
        }

        private static bool WhenEscapeNotConsumed(HotkeyContext context) => !context.Scene.IsKeyboardEscapeConsumedThisFrame;

        private static bool WhenMoveCommandFocused(HotkeyContext context)
        {
            return context.Scene.FocusControl == context.MoveCommandWindow && context.MoveCommandWindow.Visible;
        }

        private static bool WhenNotUiInput(HotkeyContext context) => !context.IsUiInputActive;

        private static bool WhenMouseNotConsumed(HotkeyContext context) => !context.Scene.IsMouseInputConsumedThisFrame;

        private static bool WhenChatLogFrameVisible(HotkeyContext context) => context.ChatLog.IsFrameVisible;

        private void RegisterGlobalHotkeys(HotkeySet hotkeys)
        {
            hotkeys.OnKeyPressed(
                Keys.Escape,
                TogglePauseMenu,
                when: WhenEscapeNotConsumed,
                stopPropagation: false);

            for (int i = 0; i < MoveCommandKeys.Length; i++)
            {
                var key = MoveCommandKeys[i];
                hotkeys.OnKeyPressed(
                    key,
                    context => _moveCommandWindow.ProcessKeyInput(key, false),
                    when: WhenMoveCommandFocused);
            }

            hotkeys.OnKeyPressed(Keys.I, ToggleInventory, when: WhenNotUiInput);
            hotkeys.OnKeyPressed(Keys.V, ToggleInventory, when: WhenNotUiInput);
            hotkeys.OnKeyPressed(Keys.C, ToggleCharacterInfo, when: WhenNotUiInput);
            hotkeys.OnKeyPressed(Keys.M, ToggleMoveCommand, when: WhenNotUiInput);
            hotkeys.OnKeyPressed(Keys.Enter, OpenChatInput, when: WhenNotUiInput);
        }

        private void RegisterInWorldHotkeys(HotkeySet hotkeys)
        {
            // Object editor: "/" + left click
            hotkeys.OnCustom(
                isTriggered: static context => context.Keyboard.IsKeyDown(Keys.OemQuestion) && context.IsLeftClickStarted,
                action: ActivateBlendingEditor,
                when: WhenMouseNotConsumed);

            // Object editor: DEL + left click
            hotkeys.OnCustom(
                isTriggered: static context => context.Keyboard.IsKeyDown(Keys.Delete) && context.IsLeftClickStarted,
                action: DeleteObject,
                when: WhenMouseNotConsumed);

            // Chat log hotkeys (as before)
            hotkeys.OnKeyPressed(Keys.F5, ToggleChatLogFrame);
            hotkeys.OnKeyPressed(Keys.F4, CycleChatLogSize);
            hotkeys.OnKeyPressed(Keys.F6, CycleChatLogBackgroundAlpha);
            hotkeys.OnKeyPressed(Keys.F2, CycleChatLogViewType);

            hotkeys.OnKeyPressed(Keys.PageUp, ScrollChatLogPageUp, when: WhenChatLogFrameVisible);
            hotkeys.OnKeyPressed(Keys.PageDown, ScrollChatLogPageDown, when: WhenChatLogFrameVisible);

            // Space bar to pick up nearest item in range
            hotkeys.OnKeyPressed(Keys.Space, PickupNearestItem, when: WhenNotUiInput);
        }

        private void TogglePauseMenu(HotkeyContext context)
        {
            if (_playerMenuController?.IsMenuVisible == true)
            {
                _playerMenuController.HideMenu();
                return;
            }

            if (_pauseMenu == null)
            {
                return;
            }

            _pauseMenu.Visible = !_pauseMenu.Visible;
            if (_pauseMenu.Visible)
            {
                _pauseMenu.BringToFront();
            }
        }

        private void ToggleInventory(HotkeyContext context)
        {
            bool wasVisible = _inventoryControl.Visible;
            if (wasVisible)
            {
                _inventoryControl.Hide();
            }
            else
            {
                _inventoryControl.Show();
            }

            if (!wasVisible)
            {
                SoundController.Instance.PlayBuffer("Sound/iCreateWindow.wav");
            }
        }

        private void ToggleCharacterInfo(HotkeyContext context)
        {
            if (_characterInfoWindow == null)
            {
                return;
            }

            bool wasVisible = _characterInfoWindow.Visible;
            if (wasVisible)
            {
                _characterInfoWindow.HideWindow();
            }
            else
            {
                _characterInfoWindow.ShowWindow();
            }

            if (!wasVisible)
            {
                SoundController.Instance.PlayBuffer("Sound/iCreateWindow.wav");
            }
        }

        private void ToggleMoveCommand(HotkeyContext context)
        {
            if (NpcShopControl.Instance.Visible)
            {
                return;
            }

            _moveCommandWindow.ToggleVisibility();
        }

        private void OpenChatInput(HotkeyContext context)
        {
            if (_chatInput.Visible)
            {
                return;
            }

            if (context.Scene.IsKeyboardEnterConsumedThisFrame)
            {
                return;
            }

            _chatInput.Show();
        }

        private void ActivateBlendingEditor(HotkeyContext context)
        {
            _objectEditorController?.HandleBlendingEditorActivation();
            context.Scene.SetMouseInputConsumed();
        }

        private void DeleteObject(HotkeyContext context)
        {
            _objectEditorController?.HandleObjectDeletion();
            context.Scene.SetMouseInputConsumed();
        }

        private void ToggleChatLogFrame(HotkeyContext context)
        {
            _chatLog?.ToggleFrame();
        }

        private void CycleChatLogSize(HotkeyContext context)
        {
            _chatLog?.CycleSize();
        }

        private void CycleChatLogBackgroundAlpha(HotkeyContext context)
        {
            _chatLog?.CycleBackgroundAlpha();
        }

        private void CycleChatLogViewType(HotkeyContext context)
        {
            if (_chatLog == null)
            {
                return;
            }

            var nextType = _chatLog.CurrentViewType + 1;
            if (!System.Enum.IsDefined(typeof(MessageType), nextType) || nextType == MessageType.Unknown) nextType = MessageType.All;
            if (nextType == MessageType.Info || nextType == MessageType.Error) nextType++;
            if (!System.Enum.IsDefined(typeof(MessageType), nextType) || nextType == MessageType.Unknown) nextType = MessageType.All;
            _chatLog.ChangeViewType(nextType);
            System.Console.WriteLine($"[ChatLog] Changed view to: {nextType}");
        }

        private void ScrollChatLogPageUp(HotkeyContext context)
        {
            _chatLog?.ScrollLines(_chatLog.NumberOfShowingLines);
        }

        private void ScrollChatLogPageDown(HotkeyContext context)
        {
            _chatLog?.ScrollLines(-_chatLog.NumberOfShowingLines);
        }

        private void PickupNearestItem(HotkeyContext context)
        {
            var network = MuGame.Network;
            var scopeManager = network?.GetScopeManager();
            var characterState = network?.GetCharacterState();

            if (scopeManager == null || characterState == null)
            {
                _logger.LogWarning("Cannot pickup item: ScopeManager or CharacterState is null");
                _chatLog?.AddMessage("System", "Cannot pickup item: system not ready.", MessageType.Error);
                return;
            }

            var nearestItemRawId = scopeManager.FindNearestPickupItemRawId(out bool outOfRange);
            if (nearestItemRawId.HasValue)
            {
                // Get the scope object to access item data
                ushort maskedId = (ushort)(nearestItemRawId.Value & 0x7FFF);
                var scopeObject = scopeManager.GetScopeObjectByMaskedId(maskedId);

                // Try to get item name for better feedback
                string itemName = "item";
                if (scopeManager.TryGetScopeObjectName(nearestItemRawId.Value, out var name) && !string.IsNullOrWhiteSpace(name))
                {
                    itemName = name;
                }

                _logger.LogInformation("Space pressed - picking up nearest item with RawId: {RawId}", nearestItemRawId.Value);

                if (scopeObject == null)
                {
                    _logger.LogWarning("Scope object for maskedId {MaskedId:X4} disappeared before pickup", maskedId);
                    return;
                }

                // Stash pending pickup data before sending request (like DroppedItemObject.OnClick does)
                characterState.SetPendingPickupRawId(nearestItemRawId.Value);

                if (scopeObject is ItemScopeObject itemScope)
                {
                    characterState.StashPickedItem(itemScope.ItemData.ToArray());
                }
                else if (scopeObject is MoneyScopeObject)
                {
                    _logger.LogDebug("Pickup initiated for Zen");
                }
                else
                {
                    _logger.LogWarning("Unknown scope object type for pickup: {Type}", scopeObject.ObjectType);
                    return;
                }

                var characterService = network.GetCharacterService();
                if (characterService == null)
                {
                    _logger.LogWarning("CharacterService is null, cannot send pickup request");
                    return;
                }

                // Send pickup request and handle result
                _ = Task.Run(async () =>
                {
                    try
                    {
                        bool success = await characterService.SendPickupItemRequestAsync(nearestItemRawId.Value, network.TargetVersion);
                        if (!success)
                        {
                            MuGame.ScheduleOnMainThread(() =>
                            {
                                _chatLog?.AddMessage("System", $"Failed to pick up {itemName}: not connected to server.", MessageType.Error);
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during pickup request for RawId {RawId}", nearestItemRawId.Value);
                    }
                });
            }
            else
            {
                if (outOfRange)
                {
                    _logger.LogDebug("Space pressed - nearest item is too far away");
                    _chatLog?.AddMessage("System", "Item is too far away.", MessageType.System);
                }
                else
                {
                    _logger.LogDebug("Space pressed - no items in pickup range");
                    _chatLog?.AddMessage("System", "No items in pickup range.", MessageType.System);
                }
            }
        }

        private readonly record struct HotkeyContext(
            GameScene Scene,
            MoveCommandWindow MoveCommandWindow,
            ChatLogWindow ChatLog,
            KeyboardState Keyboard,
            KeyboardState PreviousKeyboard,
            MouseState Mouse,
            MouseState PreviousMouse,
            HotkeyModifiers Modifiers,
            bool IsUiInputActive)
        {
            public bool IsLeftClickStarted =>
                Mouse.LeftButton == ButtonState.Pressed && PreviousMouse.LeftButton == ButtonState.Released;
        }

        private delegate bool HotkeyPredicate(HotkeyContext context);

        private delegate void HotkeyAction(HotkeyContext context);

        [System.Flags]
        private enum HotkeyModifiers : byte
        {
            None = 0,
            Shift = 1,
            Control = 2,
            Alt = 4
        }

        private enum HotkeyTrigger : byte
        {
            KeyDown = 0,
            Pressed = 1,
            Released = 2
        }

        private sealed class HotkeySet
        {
            private readonly ILogger _logger;
            private readonly string _name;
            private readonly List<HotkeyBinding> _bindings = new();

            public HotkeySet(ILogger logger, string name)
            {
                _logger = logger;
                _name = name;
            }

            public HotkeySet OnKeyDown(
                Keys key,
                HotkeyAction action,
                HotkeyPredicate when = null,
                bool stopPropagation = true)
            {
                AddKeyBinding(HotkeyBinding.ForKey(
                    key,
                    HotkeyTrigger.KeyDown,
                    HotkeyModifiers.None,
                    allowAdditionalModifiers: true,
                    action,
                    when,
                    stopPropagation));
                return this;
            }

            public HotkeySet OnKeyPressed(
                Keys key,
                HotkeyAction action,
                HotkeyPredicate when = null,
                bool stopPropagation = true)
            {
                AddKeyBinding(HotkeyBinding.ForKey(
                    key,
                    HotkeyTrigger.Pressed,
                    HotkeyModifiers.None,
                    allowAdditionalModifiers: true,
                    action,
                    when,
                    stopPropagation));
                return this;
            }

            public HotkeySet OnKeyPressed(
                Keys key,
                HotkeyModifiers modifiers,
                HotkeyAction action,
                HotkeyPredicate when = null,
                bool stopPropagation = true,
                bool exactModifiers = false)
            {
                AddKeyBinding(HotkeyBinding.ForKey(
                    key,
                    HotkeyTrigger.Pressed,
                    modifiers,
                    allowAdditionalModifiers: !exactModifiers,
                    action,
                    when,
                    stopPropagation));
                return this;
            }

            public HotkeySet OnKeyReleased(
                Keys key,
                HotkeyAction action,
                HotkeyPredicate when = null,
                bool stopPropagation = true)
            {
                AddKeyBinding(HotkeyBinding.ForKey(
                    key,
                    HotkeyTrigger.Released,
                    HotkeyModifiers.None,
                    allowAdditionalModifiers: true,
                    action,
                    when,
                    stopPropagation));
                return this;
            }

            public HotkeySet OnCustom(HotkeyPredicate isTriggered, HotkeyAction action, HotkeyPredicate when = null)
            {
                _bindings.Add(HotkeyBinding.Custom(isTriggered, action, when));
                return this;
            }

            public void Execute(HotkeyContext context)
            {
                Span<Keys> keyDownHandled = stackalloc Keys[_bindings.Count];
                Span<Keys> keyPressedHandled = stackalloc Keys[_bindings.Count];
                Span<Keys> keyReleasedHandled = stackalloc Keys[_bindings.Count];

                int keyDownHandledCount = 0;
                int keyPressedHandledCount = 0;
                int keyReleasedHandledCount = 0;

                for (int i = 0; i < _bindings.Count; i++)
                {
                    var binding = _bindings[i];
                    if (binding.IsKeyBinding && binding.StopsPropagation && IsKeyAlreadyHandled(binding, keyDownHandled, keyDownHandledCount, keyPressedHandled, keyPressedHandledCount, keyReleasedHandled, keyReleasedHandledCount))
                    {
                        continue;
                    }

                    if (!binding.TryExecute(context))
                    {
                        continue;
                    }

                    if (binding.IsKeyBinding && binding.StopsPropagation)
                    {
                        MarkKeyHandled(binding, keyDownHandled, ref keyDownHandledCount, keyPressedHandled, ref keyPressedHandledCount, keyReleasedHandled, ref keyReleasedHandledCount);
                    }
                }
            }

            private static bool IsKeyAlreadyHandled(
                HotkeyBinding binding,
                Span<Keys> keyDownHandled,
                int keyDownHandledCount,
                Span<Keys> keyPressedHandled,
                int keyPressedHandledCount,
                Span<Keys> keyReleasedHandled,
                int keyReleasedHandledCount)
            {
                return binding.Trigger switch
                {
                    HotkeyTrigger.KeyDown => IsKeyInList(binding.Key, keyDownHandled, keyDownHandledCount),
                    HotkeyTrigger.Pressed => IsKeyInList(binding.Key, keyPressedHandled, keyPressedHandledCount),
                    HotkeyTrigger.Released => IsKeyInList(binding.Key, keyReleasedHandled, keyReleasedHandledCount),
                    _ => false
                };
            }

            private static bool IsKeyInList(Keys key, Span<Keys> handled, int count)
            {
                for (int i = 0; i < count; i++)
                {
                    if (handled[i] == key)
                    {
                        return true;
                    }
                }

                return false;
            }

            private static void MarkKeyHandled(
                HotkeyBinding binding,
                Span<Keys> keyDownHandled,
                ref int keyDownHandledCount,
                Span<Keys> keyPressedHandled,
                ref int keyPressedHandledCount,
                Span<Keys> keyReleasedHandled,
                ref int keyReleasedHandledCount)
            {
                switch (binding.Trigger)
                {
                    case HotkeyTrigger.KeyDown:
                        keyDownHandled[keyDownHandledCount++] = binding.Key;
                        break;
                    case HotkeyTrigger.Pressed:
                        keyPressedHandled[keyPressedHandledCount++] = binding.Key;
                        break;
                    case HotkeyTrigger.Released:
                        keyReleasedHandled[keyReleasedHandledCount++] = binding.Key;
                        break;
                }
            }

            private void AddKeyBinding(HotkeyBinding binding)
            {
                WarnOnOverlap(binding);

                // Keep conditional bindings in registration order, but ensure that for a given (Key, Trigger)
                // more specific modifier bindings are evaluated first.
                int insertIndex = _bindings.Count;
                for (int i = 0; i < _bindings.Count; i++)
                {
                    var existing = _bindings[i];
                    if (!existing.IsKeyBinding || existing.Key != binding.Key || existing.Trigger != binding.Trigger)
                    {
                        continue;
                    }

                    if (existing.ModifierSpecificity < binding.ModifierSpecificity)
                    {
                        insertIndex = i;
                        break;
                    }

                    insertIndex = i + 1;
                }

                _bindings.Insert(insertIndex, binding);
            }

            private void WarnOnOverlap(HotkeyBinding binding)
            {
                if (!binding.IsKeyBinding || binding.When != null)
                {
                    return;
                }

                for (int i = 0; i < _bindings.Count; i++)
                {
                    var existing = _bindings[i];
                    if (!existing.IsKeyBinding || existing.Key != binding.Key || existing.Trigger != binding.Trigger || existing.When != null)
                    {
                        continue;
                    }

                    if (!existing.ModifiersCanOverlapWith(binding))
                    {
                        continue;
                    }

                    _logger.LogWarning(
                        "Hotkey overlap in {HotkeySet}: {Key} ({Trigger}) registered more than once without conditions; order will decide which runs.",
                        _name,
                        binding.Key,
                        binding.Trigger);
                    return;
                }
            }

            private readonly struct HotkeyBinding
            {
                private readonly Keys _key;
                private readonly HotkeyTrigger _trigger;
                private readonly HotkeyPredicate _isTriggered;
                private readonly HotkeyPredicate _when;
                private readonly HotkeyAction _action;
                private readonly HotkeyModifiers _requiredModifiers;
                private readonly bool _allowAdditionalModifiers;
                private readonly bool _stopPropagation;

                private HotkeyBinding(
                    Keys key,
                    HotkeyTrigger trigger,
                    HotkeyModifiers requiredModifiers,
                    bool allowAdditionalModifiers,
                    HotkeyAction action,
                    HotkeyPredicate when,
                    bool stopPropagation)
                {
                    _key = key;
                    _trigger = trigger;
                    _action = action;
                    _when = when;
                    _isTriggered = null;
                    _requiredModifiers = requiredModifiers;
                    _allowAdditionalModifiers = allowAdditionalModifiers;
                    _stopPropagation = stopPropagation;
                }

                private HotkeyBinding(HotkeyPredicate isTriggered, HotkeyAction action, HotkeyPredicate when)
                {
                    _key = default;
                    _trigger = default;
                    _action = action;
                    _when = when;
                    _isTriggered = isTriggered;
                    _requiredModifiers = default;
                    _allowAdditionalModifiers = default;
                    _stopPropagation = default;
                }

                public bool IsKeyBinding => _isTriggered == null;

                public Keys Key => _key;

                public HotkeyTrigger Trigger => _trigger;

                public bool StopsPropagation => _stopPropagation;

                public HotkeyPredicate When => _when;

                public int ModifierSpecificity
                {
                    get
                    {
                        int count = CountModifiers(_requiredModifiers);
                        return (count * 10) + (_allowAdditionalModifiers ? 0 : 1);
                    }
                }

                public static HotkeyBinding ForKey(
                    Keys key,
                    HotkeyTrigger trigger,
                    HotkeyModifiers requiredModifiers,
                    bool allowAdditionalModifiers,
                    HotkeyAction action,
                    HotkeyPredicate when,
                    bool stopPropagation)
                {
                    return new HotkeyBinding(key, trigger, requiredModifiers, allowAdditionalModifiers, action, when, stopPropagation);
                }

                public static HotkeyBinding Custom(HotkeyPredicate isTriggered, HotkeyAction action, HotkeyPredicate when)
                {
                    return new HotkeyBinding(isTriggered, action, when);
                }

                public bool TryExecute(HotkeyContext context)
                {
                    if (_when != null && !_when(context))
                    {
                        return false;
                    }

                    bool triggered = _isTriggered != null
                        ? _isTriggered(context)
                        : IsKeyTriggered(context);

                    if (!triggered)
                    {
                        return false;
                    }

                    _action(context);
                    return true;
                }

                public bool ModifiersCanOverlapWith(HotkeyBinding other)
                {
                    if (!IsKeyBinding || !other.IsKeyBinding)
                    {
                        return false;
                    }

                    if (_allowAdditionalModifiers && other._allowAdditionalModifiers)
                    {
                        // Both accept supersets; intersection always exists (union of requirements).
                        return true;
                    }

                    if (!_allowAdditionalModifiers && !other._allowAdditionalModifiers)
                    {
                        return _requiredModifiers == other._requiredModifiers;
                    }

                    var exact = _allowAdditionalModifiers ? other : this;
                    var superset = _allowAdditionalModifiers ? this : other;

                    return (exact._requiredModifiers & superset._requiredModifiers) == superset._requiredModifiers;
                }

                private bool IsKeyTriggered(HotkeyContext context)
                {
                    if (!ModifiersMatch(context.Modifiers))
                    {
                        return false;
                    }

                    return _trigger switch
                    {
                        HotkeyTrigger.KeyDown => context.Keyboard.IsKeyDown(_key),
                        HotkeyTrigger.Pressed => context.Keyboard.IsKeyDown(_key) && context.PreviousKeyboard.IsKeyUp(_key),
                        HotkeyTrigger.Released => context.Keyboard.IsKeyUp(_key) && context.PreviousKeyboard.IsKeyDown(_key),
                        _ => false
                    };
                }

                private bool ModifiersMatch(HotkeyModifiers current)
                {
                    return _allowAdditionalModifiers
                        ? (current & _requiredModifiers) == _requiredModifiers
                        : current == _requiredModifiers;
                }

                private static int CountModifiers(HotkeyModifiers modifiers)
                {
                    int count = 0;
                    if ((modifiers & HotkeyModifiers.Shift) != 0) count++;
                    if ((modifiers & HotkeyModifiers.Control) != 0) count++;
                    if ((modifiers & HotkeyModifiers.Alt) != 0) count++;
                    return count;
                }
            }
        }
    }
}
