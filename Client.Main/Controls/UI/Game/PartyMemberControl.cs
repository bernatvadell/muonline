using Client.Main.Controls.UI.Common;
using Client.Main.Core.Client;
using Client.Main.Core.Utilities;
using Microsoft.Xna.Framework;

namespace Client.Main.Controls.UI.Game
{
    public class PartyMemberControl : UIControl
    {
        private readonly LabelControl _nameLabel;
        private readonly LabelControl _infoLabel;
        
        private readonly LabelControl _healthPercentLabel;
        private readonly ButtonControl _leaveButton;

        private readonly ColorBarControl[] _healthSegments;

        public PartyMemberInfo MemberInfo { get; private set; }
        public bool IsCurrentPlayer { get; private set; }

        public PartyMemberControl()
        {
            AutoViewSize = false;
            ViewSize = new Point(160, 48);
            BackgroundColor = new Color(15, 15, 25) * 0.9f;
            BorderColor = new Color(100, 150, 200) * 0.8f;
            BorderThickness = 1;

            _nameLabel = new LabelControl
            {
                X = 6,
                Y = 4,
                FontSize = 11f,
                TextColor = Color.White,
                IsBold = true,
                HasShadow = true,
                ShadowColor = Color.Black,
                ShadowOffset = new Vector2(1, 1),
                ShadowOpacity = 0.8f
            };

            _healthSegments = new ColorBarControl[4];
            int segmentWidth = 30;
            int segmentSpacing = 2;

            for (int i = 0; i < 4; i++)
            {
                _healthSegments[i] = new ColorBarControl
                {
                    X = 6 + i * (segmentWidth + segmentSpacing),
                    Y = 20,
                    ViewSize = new Point(segmentWidth, 7),
                    BackgroundColor = new Color(60, 20, 20) * 0.8f,
                    FillColor = GetHealthSegmentColor(i),
                    BorderColor = Color.Black * 0.8f,
                    BorderThickness = 1
                };
                Controls.Add(_healthSegments[i]);
            }

            _healthPercentLabel = new LabelControl
            {
                X = 4 * (segmentWidth + segmentSpacing) + 5,
                Y = 18,
                FontSize = 8f,
                TextColor = Color.Yellow,
                IsBold = true,
                HasShadow = true,
                ShadowColor = Color.Black,
                ShadowOffset = new Vector2(1, 1),
                ShadowOpacity = 0.8f
            };

            _infoLabel = new LabelControl
            {
                X = 6,
                Y = 28,
                FontSize = 9f,
                TextColor = Color.LightBlue,
                HasShadow = true,
                ShadowColor = Color.Black,
                ShadowOffset = new Vector2(1, 1),
                ShadowOpacity = 0.7f
            };

            _leaveButton = new ButtonControl
            {
                X = ViewSize.X - 18,
                Y = 3,
                ViewSize = new Point(15, 15),
                Text = "×",
                FontSize = 12f,
                TextColor = Color.White,
                BackgroundColor = new Color(150, 50, 50) * 0.8f,
                BorderColor = new Color(200, 100, 100),
                BorderThickness = 1,
                Visible = false,
            };
            _leaveButton.Click += OnLeaveButtonClick;

            Controls.Add(_nameLabel);
            Controls.Add(_infoLabel);
            Controls.Add(_healthPercentLabel);
            Controls.Add(_leaveButton);
        }

        private Color GetHealthSegmentColor(int segmentIndex)
        {
            switch (segmentIndex)
            {
                case 0: return new Color(220, 20, 20);
                case 1: return new Color(220, 20, 20);
                case 2: return new Color(220, 20, 20);
                case 3: return new Color(220, 20, 20);
                default: return Color.Red;
            }
        }

        public void UpdateData(PartyMemberInfo memberInfo, bool isCurrentPlayer = false)
        {
            MemberInfo = memberInfo;
            IsCurrentPlayer = isCurrentPlayer;

            _nameLabel.Text = memberInfo.Name;

            _healthPercentLabel.Text = $"{(int)(memberInfo.HealthPercentage * 100)}%";

            if (memberInfo.HealthPercentage <= 0.25f)
                _healthPercentLabel.TextColor = Color.Red;
            else if (memberInfo.HealthPercentage <= 0.5f)
                _healthPercentLabel.TextColor = Color.Orange;
            else
                _healthPercentLabel.TextColor = Color.LimeGreen;

            if (memberInfo.HealthPercentage <= 0.25f)
                _nameLabel.TextColor = Color.Red;
            else if (memberInfo.HealthPercentage <= 0.5f)
                _nameLabel.TextColor = Color.Orange;
            else
                _nameLabel.TextColor = Color.White;

            UpdateHealthSegments(memberInfo.HealthPercentage);

            string mapName = MapDatabase.GetMapName(memberInfo.MapId);
            _infoLabel.Text = $"{mapName} ({memberInfo.PositionX}, {memberInfo.PositionY})";

            _leaveButton.Visible = isCurrentPlayer;

            if (isCurrentPlayer)
            {
                BorderColor = new Color(150, 200, 100) * 0.9f; // Zielona ramka
                BackgroundColor = new Color(15, 25, 15) * 0.9f; // Lekko zielone tło
            }
            else
            {
                BorderColor = new Color(100, 150, 200) * 0.8f; // Standardowa ramka
                BackgroundColor = new Color(15, 15, 25) * 0.9f; // Standardowe tło
            }
        }

        private void UpdateHealthSegments(float healthPercentage)
        {
            float segmentSize = 0.25f;

            for (int i = 0; i < 4; i++)
            {
                float segmentStart = i * segmentSize;
                float segmentEnd = (i + 1) * segmentSize;

                if (healthPercentage <= segmentStart)
                {
                    _healthSegments[i].Percentage = 0f;
                }
                else if (healthPercentage >= segmentEnd)
                {
                    _healthSegments[i].Percentage = 1f;
                }
                else
                {
                    float segmentFill = (healthPercentage - segmentStart) / segmentSize;
                    _healthSegments[i].Percentage = segmentFill;
                }

                if (healthPercentage <= 0.25f && i == 0)
                {
                    _healthSegments[i].Alpha = 0.5f + (float)(System.Math.Sin(System.DateTime.Now.Millisecond * 0.01) * 0.3);
                }
                else
                {
                    _healthSegments[i].Alpha = 1f;
                }
            }
        }

        private void OnLeaveButtonClick(object sender, System.EventArgs e)
        {
            if (IsCurrentPlayer && MemberInfo != null)
            {
                var characterService = MuGame.Network?.GetCharacterService();
                _ = characterService?.SendPartyKickRequestAsync(MemberInfo.Index);
            }
        }
    }
}