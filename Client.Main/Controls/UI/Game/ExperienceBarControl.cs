#nullable enable
using Client.Main.Core.Client;
using Client.Main.Models;
using Microsoft.Xna.Framework;

namespace Client.Main.Controls.UI.Game
{
    /// <summary>
    /// Experience bar control - displays current level progress as percentage.
    /// Positioned near the skill quick slot at the bottom of the screen.
    /// </summary>
    public class ExperienceBarControl : UIControl
    {
        private readonly CharacterState _characterState;
        private readonly LabelControl _expLabel;

        public ExperienceBarControl(CharacterState characterState)
        {
            _characterState = characterState;

            // Position at bottom-center, below skill slot
            Align = ControlAlign.HorizontalCenter | ControlAlign.Bottom;
            Margin = new Margin { Bottom = 5 }; // Just above bottom edge, below skill slot

            ViewSize = new Point(120, 20);
            Interactive = false;
            BackgroundColor = Color.Transparent;
            BorderColor = Color.Transparent;

            // Experience percentage label
            _expLabel = new LabelControl
            {
                Text = "EXP: 0.0%",
                TextColor = Color.Cyan,
                X = 0,
                Y = 0,
                ViewSize = new Point(120, 20),
                Scale = 0.75f,
                TextAlign = HorizontalAlign.Center
            };
            Controls.Add(_expLabel);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            UpdateExpLabel();
        }

        private void UpdateExpLabel()
        {
            // Calculate experience requirement for previous level
            ushort currentLevel = _characterState.Level;
            ulong prevLevelExp = currentLevel > 1 ? (ulong)((currentLevel - 1 + 9) * (currentLevel - 1) * (currentLevel - 1) * 10) : 0;

            // Calculate experience range for current level
            ulong expInCurrentLevel = _characterState.Experience - prevLevelExp;
            ulong expNeededForLevel = _characterState.ExperienceForNextLevel - prevLevelExp;

            // Calculate percentage (0-100%)
            double expPercent = expNeededForLevel > 0 ? (expInCurrentLevel / (double)expNeededForLevel) * 100.0 : 0.0;

            _expLabel.Text = $"EXP: {expPercent:F1}%";
        }
    }
}
