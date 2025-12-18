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
        private readonly ButtonControl _duelButton;
        private readonly ButtonControl _storeButton;
        private readonly ButtonControl _whisperButton;

        private ushort _targetPlayerId;
        private string _targetPlayerName;

        public event Action<string> WhisperRequested;
        public event Action<ushort, string> DuelRequested;

        public PlayerContextMenu()
        {
            AutoViewSize = false;
            ControlSize = new Point(170, 150);
            ViewSize = ControlSize;
            BackgroundColor = new Color(30, 30, 50, 240);
            BorderColor = new Color(100, 100, 150, 255);
            BorderThickness = 1;
            Interactive = true;
            Visible = false;

            var buttons = CreateButtons();
            _partyButton = buttons.party;
            _tradeButton = buttons.trade;
            _duelButton = buttons.duel;
            _storeButton = buttons.store;
            _whisperButton = buttons.whisper;

            _partyButton.Click += OnPartyInviteClicked;
            _tradeButton.Click += OnTradeRequestClicked;
            _duelButton.Click += OnDuelRequestClicked;
            _storeButton.Click += OnStoreRequestClicked;
            _whisperButton.Click += OnWhisperClicked;

            Controls.Add(_partyButton);
            Controls.Add(_tradeButton);
            Controls.Add(_duelButton);
            Controls.Add(_storeButton);
            Controls.Add(_whisperButton);
        }

        /// <summary>
        /// Sets the target player for context menu actions.
        /// </summary>
        public void SetTarget(ushort playerId, string playerName)
        {
            _targetPlayerId = playerId;
            _targetPlayerName = playerName ?? string.Empty;
        }

        public ushort TargetPlayerId => _targetPlayerId;
        public string TargetPlayerName => _targetPlayerName;

        public void SetDuelButtonEnabled(bool enabled)
        {
            _duelButton.Enabled = enabled;
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

        private void OnDuelRequestClicked(object sender, EventArgs e)
        {
            _logger?.LogInformation("Duel request clicked for player: {Name} (ID: {Id})", _targetPlayerName, _targetPlayerId);
            DuelRequested?.Invoke(_targetPlayerId, _targetPlayerName);

            Visible = false;
        }

        private async void OnStoreRequestClicked(object sender, EventArgs e)
        {
            _logger?.LogInformation($"Requesting store list from player: {_targetPlayerName} (ID: {_targetPlayerId})");

            var characterService = MuGame.Network?.GetCharacterService();
            if (characterService != null)
            {
                await characterService.SendPlayerStoreListRequestAsync(_targetPlayerId, _targetPlayerName);
            }
            else
            {
                _logger?.LogWarning("CharacterService not available for player store request");
            }

            Visible = false;
        }

        private void OnWhisperClicked(object sender, EventArgs e)
        {
            _logger?.LogInformation($"Starting whisper to player: {_targetPlayerName} (ID: {_targetPlayerId})");
            WhisperRequested?.Invoke(_targetPlayerName);
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

        private (ButtonControl party, ButtonControl trade, ButtonControl duel, ButtonControl store, ButtonControl whisper) CreateButtons()
        {
            int x = 5;
            int y = 5;
            int width = 160;
            int height = 28;

            ButtonControl Make(string text, int offsetY)
            {
                return new ButtonControl
                {
                    Text = text,
                    X = x,
                    Y = offsetY,
                    ControlSize = new Point(width, height),
                    ViewSize = new Point(width, height),
                    AutoViewSize = false,
                    BackgroundColor = new Color(50, 50, 80, 200),
                    HoverBackgroundColor = new Color(80, 80, 120, 220),
                    PressedBackgroundColor = new Color(40, 40, 70, 220),
                    FontSize = 12f,
                    TextColor = Color.White
                };
            }

            var party = Make("Party Invite", y);
            var trade = Make("Trade Request", y + height * 1);
            var duel = Make("Duel Request", y + height * 2);
            var store = Make("View Store", y + height * 3);
            var whisper = Make("Whisper", y + height * 4);

            return (party, trade, duel, store, whisper);
        }
    }
}
