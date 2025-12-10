using System;
using Client.Main;
using Client.Main.Controls.UI.Common;
using Client.Main.Controllers;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;

namespace Client.Main.Controls.UI.Game
{
    /// <summary>
    /// Context menu for remote player interactions (Party, Trade).
    /// Triggered by ALT + Right Click on a remote player.
    /// </summary>
    public class PlayerContextMenu : UIControl
    {
        private static readonly ILogger _logger = MuGame.AppLoggerFactory?.CreateLogger<PlayerContextMenu>();

        private readonly ButtonControl _partyButton;
        private readonly ButtonControl _tradeButton;

        private ushort _targetPlayerId;
        private string _targetPlayerName;

        public PlayerContextMenu()
        {
            AutoViewSize = false;
            ControlSize = new Point(170, 66);
            ViewSize = ControlSize;
            BackgroundColor = new Color(30, 30, 50, 240);
            BorderColor = new Color(100, 100, 150, 255);
            BorderThickness = 1;
            Interactive = true;
            Visible = false;

            // Party Invite button
            _partyButton = new ButtonControl
            {
                Text = "Party Invite",
                X = 5,
                Y = 5,
                ControlSize = new Point(160, 28),
                ViewSize = new Point(160, 28),
                AutoViewSize = false,
                BackgroundColor = new Color(50, 50, 80, 200),
                HoverBackgroundColor = new Color(80, 80, 120, 220),
                PressedBackgroundColor = new Color(40, 40, 70, 220),
                FontSize = 12f,
                TextColor = Color.White
            };
            _partyButton.Click += OnPartyInviteClicked;
            Controls.Add(_partyButton);

            // Trade Request button
            _tradeButton = new ButtonControl
            {
                Text = "Trade Request",
                X = 5,
                Y = 33,
                ControlSize = new Point(160, 28),
                ViewSize = new Point(160, 28),
                AutoViewSize = false,
                BackgroundColor = new Color(50, 50, 80, 200),
                HoverBackgroundColor = new Color(80, 80, 120, 220),
                PressedBackgroundColor = new Color(40, 40, 70, 220),
                FontSize = 12f,
                TextColor = Color.White
            };
            _tradeButton.Click += OnTradeRequestClicked;
            Controls.Add(_tradeButton);
        }

        /// <summary>
        /// Sets the target player for context menu actions.
        /// </summary>
        public void SetTarget(ushort playerId, string playerName)
        {
            _targetPlayerId = playerId;
            _targetPlayerName = playerName ?? string.Empty;
        }

        /// <summary>
        /// Shows the context menu at the specified screen position.
        /// Position is clamped to stay within screen bounds.
        /// </summary>
        public void ShowAt(int x, int y)
        {
            X = x;
            Y = y;

            // Clamp to screen bounds
            int maxX = UiScaler.VirtualSize.X - ViewSize.X;
            int maxY = UiScaler.VirtualSize.Y - ViewSize.Y;
            X = Math.Clamp(X, 0, maxX);
            Y = Math.Clamp(Y, 0, maxY);

            Visible = true;
        }

        /// <summary>
        /// Handles Party Invite button click.
        /// </summary>
        private async void OnPartyInviteClicked(object sender, EventArgs e)
        {
            _logger?.LogInformation($"Party invite requested for player: {_targetPlayerName} (ID: {_targetPlayerId})");

            var characterService = MuGame.Network?.GetCharacterService();
            if (characterService != null)
            {
                await characterService.SendPartyInviteAsync(_targetPlayerId);
            }
            else
            {
                _logger?.LogWarning("CharacterService not available for party invite");
            }

            Visible = false;
        }

        /// <summary>
        /// Handles Trade Request button click.
        /// </summary>
        private async void OnTradeRequestClicked(object sender, EventArgs e)
        {
            _logger?.LogInformation($"Trade request sent to player: {_targetPlayerName} (ID: {_targetPlayerId})");

            var characterService = MuGame.Network?.GetCharacterService();
            if (characterService != null)
            {
                await characterService.SendTradeRequestAsync(_targetPlayerId);
            }
            else
            {
                _logger?.LogWarning("CharacterService not available for trade request");
            }

            Visible = false;
        }

        /// <summary>
        /// Override to consume clicks inside menu (prevent click-through).
        /// </summary>
        public override bool OnClick()
        {
            base.OnClick();
            return true;
        }
    }
}
