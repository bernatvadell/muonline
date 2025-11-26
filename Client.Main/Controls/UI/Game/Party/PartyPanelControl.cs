using System.Linq;
using System.Threading.Tasks;
using Client.Main.Core.Client;
using Client.Main.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;

namespace Client.Main.Controls.UI.Game.Party
{
    public class PartyPanelControl : UIControl
    {
        private const int VerticalSpacing = 3;
        private const double UpdateIntervalSeconds = 5.0;

        private PartyManager _partyManager;
        private readonly ILogger<PartyPanelControl> _logger;
        private CharacterState _characterState;

        private double _timeSinceLastUpdate = 0; // Timer

        public PartyPanelControl()
        {
            _logger = MuGame.AppLoggerFactory.CreateLogger<PartyPanelControl>();
            Align = ControlAlign.Top | ControlAlign.Left;
            Margin = new Margin { Top = 10, Left = 10 };
            Visible = false;
        }

        public override async Task Load()
        {
            _partyManager = MuGame.Network.GetPartyManager();
            _characterState = MuGame.Network.GetCharacterState();

            if (_partyManager != null)
            {
                _partyManager.PartyUpdated += OnPartyUpdated;
            }
            await base.Load();
        }

        private void OnPartyUpdated()
        {
            _logger.LogDebug("PartyPanelControl.OnPartyUpdated fired.");
            // Clear previous controls to rebuild the list
            foreach (var control in Controls)
            {
                control.Dispose();
            }
            Controls.Clear();

            var members = _partyManager.GetPartyMembers();
            _logger.LogDebug("Party contains {Count} members.", members.Count);

            if (members.Count <= 1)
            {
                Visible = false;
                _logger.LogDebug("Party empty, hiding panel.");
                return;
            }

            Visible = true;
            _logger.LogDebug("Party not empty, showing panel.");
            int currentY = 0;
            foreach (var member in members)
            {
                var memberControl = new PartyMemberControl
                {
                    Y = currentY
                };

                bool isCurrentPlayer = IsCurrentPlayer(member);

                memberControl.UpdateData(member, isCurrentPlayer);
                Controls.Add(memberControl);
                // Initialize is needed so child controls (labels, etc.) can load their resources.
                _ = memberControl.Initialize();
                currentY += memberControl.ViewSize.Y + VerticalSpacing;
            }
        }

        private bool IsCurrentPlayer(PartyMemberInfo member)
        {
            if (_characterState == null)
                return false;

            if (!string.IsNullOrEmpty(member.Name) && !string.IsNullOrEmpty(_characterState.Name))
            {
                return string.Equals(member.Name, _characterState.Name, System.StringComparison.OrdinalIgnoreCase);
            }

            if (member.Id != 0 && _characterState.Id != 0)
            {
                return member.Id == _characterState.Id;
            }

            return false;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (!Visible)
            {
                _timeSinceLastUpdate = 0;
                return;
            }

            _timeSinceLastUpdate += gameTime.ElapsedGameTime.TotalSeconds;

            if (_timeSinceLastUpdate >= UpdateIntervalSeconds)
            {
                _timeSinceLastUpdate = 0;
                _logger.LogTrace("PartyPanel timer elapsed. Requesting party list update.");

                var characterService = MuGame.Network?.GetCharacterService();
                if (characterService != null)
                {
                    _ = characterService.RequestPartyListAsync();
                }
            }
        }

        public override void Dispose()
        {
            if (_partyManager != null)
            {
                _partyManager.PartyUpdated -= OnPartyUpdated;
            }
            base.Dispose();
        }
    }
}
