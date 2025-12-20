using System;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Controls.UI;
using Client.Main.Controls.UI.Game;
using Client.Main.Models;
using Client.Main.Objects.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Client.Main.Scenes
{
    internal sealed class GameScenePlayerMenuController
    {
        private const double PlayerMenuHintCooldownSeconds = 0.3;

        private readonly GameScene _scene;
        private readonly Action<string> _whisperRequested;
        private readonly Action<ushort, string> _duelRequested;

        private PlayerContextMenu _playerContextMenu;
        private LabelControl _playerMenuHint;
        private double _playerMenuHoverTime;
        private ushort? _playerMenuHoverId;
        private double _playerMenuHintCooldown;
        private float _playerMenuHintAlpha;

        public GameScenePlayerMenuController(GameScene scene, Action<string> whisperRequested, Action<ushort, string> duelRequested)
        {
            _scene = scene ?? throw new ArgumentNullException(nameof(scene));
            _whisperRequested = whisperRequested;
            _duelRequested = duelRequested;
        }

        public void Initialize()
        {
            if (_playerMenuHint != null)
                return;

            _playerMenuHint = new LabelControl
            {
                Text = "ALT + RMB: player menu",
                FontSize = 11f,
                Padding = new Margin { Left = 6, Right = 6, Top = 4, Bottom = 4 },
                BackgroundColor = new Color(0, 0, 0, 180),
                TextColor = Color.White,
                HasShadow = true,
                Visible = false,
                Interactive = false
            };
            _scene.Controls.Add(_playerMenuHint);
            _playerMenuHint.BringToFront();
        }

        public bool IsMenuVisible => _playerContextMenu?.Visible == true;

        public void HideMenu()
        {
            if (_playerContextMenu?.Visible == true)
            {
                _playerContextMenu.Visible = false;
                _playerMenuHintCooldown = PlayerMenuHintCooldownSeconds;
            }
        }

        public void ResetOnWorldUnavailable()
        {
            if (_playerContextMenu?.Visible == true)
            {
                _playerContextMenu.Visible = false;
            }
            if (_playerMenuHint?.Visible == true)
            {
                _playerMenuHint.Visible = false;
            }
            _playerMenuHoverTime = 0;
            _playerMenuHoverId = null;
            _playerMenuHintCooldown = 0;
            _playerMenuHintAlpha = 0;
        }

        public void Update(GameTime gameTime, KeyboardState keyboardState, MouseState uiMouse, MouseState prevUiMouse)
        {
            bool contextMenuOpened = false;

            if (!_scene.IsMouseInputConsumedThisFrame &&
                _scene.MouseHoverObject is PlayerObject hoveredPlayer &&
                hoveredPlayer != _scene.Hero)
            {
                bool altPressed = keyboardState.IsKeyDown(Keys.LeftAlt) || keyboardState.IsKeyDown(Keys.RightAlt);
                bool rightClickReleased = prevUiMouse.RightButton == ButtonState.Pressed &&
                                          uiMouse.RightButton == ButtonState.Released;

                if (altPressed && rightClickReleased)
                {
                    ShowPlayerContextMenu(hoveredPlayer, uiMouse.Position);
                    _scene.SetMouseInputConsumed();
                    contextMenuOpened = true;
                }
            }

            if (_playerContextMenu?.Visible == true && !contextMenuOpened)
            {
                bool clickReleasedOutside =
                    ((prevUiMouse.LeftButton == ButtonState.Pressed && uiMouse.LeftButton == ButtonState.Released) ||
                     (prevUiMouse.RightButton == ButtonState.Pressed && uiMouse.RightButton == ButtonState.Released))
                    && !_playerContextMenu.IsMouseOver;

                bool targetMissing = !IsPlayerAvailable(_playerContextMenu.TargetPlayerId);

                if (clickReleasedOutside || targetMissing)
                {
                    _playerContextMenu.Visible = false;
                    _playerMenuHintCooldown = PlayerMenuHintCooldownSeconds;
                }
            }

            UpdatePlayerMenuHint(gameTime, keyboardState, _scene.MouseHoverObject as PlayerObject, uiMouse.Position);
        }

        private void ShowPlayerContextMenu(PlayerObject targetPlayer, Point mousePos)
        {
            if (_playerContextMenu == null)
            {
                _playerContextMenu = new PlayerContextMenu();
                _scene.Controls.Add(_playerContextMenu);
                _playerContextMenu.WhisperRequested += _whisperRequested;
                _playerContextMenu.DuelRequested += _duelRequested;
            }

            _playerContextMenu.SetTarget(targetPlayer.NetworkId, targetPlayer.Name);
            _playerContextMenu.SetDuelButtonEnabled(true);
            _playerContextMenu.ShowAt(mousePos.X, mousePos.Y);
            _playerContextMenu.BringToFront();
            _playerMenuHintCooldown = PlayerMenuHintCooldownSeconds;
            _playerMenuHintAlpha = 0;
            if (_playerMenuHint != null)
            {
                _playerMenuHint.Visible = false;
            }
        }

        private void UpdatePlayerMenuHint(GameTime gameTime, KeyboardState keyboardState, PlayerObject hoveredPlayer, Point mousePos)
        {
            const double hintDelaySeconds = 0.35;
            double dt = gameTime.ElapsedGameTime.TotalSeconds;

            if (_playerMenuHintCooldown > 0)
            {
                _playerMenuHintCooldown = Math.Max(0, _playerMenuHintCooldown - dt);
            }

            bool ctrlDown = keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl);
            bool menuOpen = _playerContextMenu?.Visible == true;
            bool playerValid = hoveredPlayer != null && hoveredPlayer != _scene.Hero && hoveredPlayer.World == _scene.World;
            bool shouldShow = playerValid && !ctrlDown && !menuOpen && _playerMenuHintCooldown <= 0;

            if (!playerValid)
            {
                _playerMenuHoverId = null;
                _playerMenuHoverTime = 0;
            }

            if (shouldShow && hoveredPlayer != null && _playerMenuHoverId == hoveredPlayer.NetworkId)
            {
                _playerMenuHoverTime += gameTime.ElapsedGameTime.TotalSeconds;
            }
            else if (hoveredPlayer != null)
            {
                _playerMenuHoverId = hoveredPlayer.NetworkId;
                _playerMenuHoverTime = 0;
            }

            bool pastDelay = _playerMenuHoverTime >= hintDelaySeconds;
            float targetAlpha = (shouldShow && pastDelay) ? 1f : 0f;
            _playerMenuHintAlpha = MathHelper.Lerp(_playerMenuHintAlpha, targetAlpha, (float)Math.Min(1, dt * 8));

            if (_playerMenuHint == null)
            {
                return;
            }

            _playerMenuHint.Alpha = _playerMenuHintAlpha;

            if (_playerMenuHintAlpha < 0.01f)
            {
                _playerMenuHint.Visible = false;
                return;
            }

            var hintPosition = new Point(mousePos.X + 14, mousePos.Y + 18);
            int hintWidth = _playerMenuHint.ControlSize.X + _playerMenuHint.Padding.Left + _playerMenuHint.Padding.Right;
            int hintHeight = _playerMenuHint.ControlSize.Y + _playerMenuHint.Padding.Top + _playerMenuHint.Padding.Bottom;
            _playerMenuHint.ViewSize = new Point(hintWidth, hintHeight);

            int maxX = UiScaler.VirtualSize.X - hintWidth - 4;
            int maxY = UiScaler.VirtualSize.Y - hintHeight - 4;
            _playerMenuHint.X = Math.Clamp(hintPosition.X, 4, Math.Max(4, maxX));
            _playerMenuHint.Y = Math.Clamp(hintPosition.Y, 4, Math.Max(4, maxY));
            _playerMenuHint.Visible = true;
            _playerMenuHint.BringToFront();
        }

        private bool IsPlayerAvailable(ushort playerId)
        {
            if (playerId == 0 || _scene.World is not WalkableWorldControl walkableWorld)
                return false;

            if (walkableWorld.WalkerObjectsById.TryGetValue(playerId, out var walker) && walker is PlayerObject po)
            {
                return po.World == _scene.World;
            }

            return false;
        }
    }
}
