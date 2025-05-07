using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Controls.UI.Game
{
    public class MainHPControl : TextureControl
    {
        private readonly MainHPStatusControl _progress;
        // Store the label separately (do not add it to the Controls list)
        private readonly LabelControl _label;
        private int _currentHP;
        private int _maxHP;

        public int CurrentHP
        {
            get => _currentHP;
            set
            {
                _currentHP = value;
                UpdatePercent();
            }
        }

        public int MaxHP
        {
            get => _maxHP;
            set
            {
                _maxHP = value;
                UpdatePercent();
            }
        }

        public MainHPControl()
        {
            TexturePath = "Interface/GFx/main_IE.ozd";
            TextureRectangle = new Rectangle(427, 0, 86, 86);
            AutoViewSize = false;
            ViewSize = new Point(86, 86);
            BlendState = BlendState.AlphaBlend;

            // Add the progress bar as a child (drawn in the normal order)
            Controls.Add(_progress = new MainHPStatusControl
            {
                Align = ControlAlign.Bottom,
                Margin = new Margin { Bottom = 2 }
            });

            // Create the label but do not add it to Controls so it can be drawn on top later
            _label = new LabelControl
            {
                Align = ControlAlign.VerticalCenter | ControlAlign.HorizontalCenter,
                FontSize = 10,
                IsBold = true,
                ShadowOpacity = 0.3f,
                BoldWeight = 1
            };
        }

        public void UpdatePercent()
        {
            _progress.Percentage = MaxHP <= 0 ? 0 : (float)CurrentHP / MaxHP;
            _label.Text = $"{CurrentHP}/{MaxHP}";
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            // Draw the texture and the progress control (but NOT the label)
            base.Draw(gameTime);
        }

        private void CenterLabel()
        {
            var rect = DisplayRectangle;
            _label.X = rect.X + (rect.Width - _label.ControlSize.X) / 2;
            _label.Y = rect.Y + (rect.Height - _label.ControlSize.Y) / 2;
        }

        public void SetValues(int current, int max)  // Convenience setter
        {
            _currentHP = current;
            _maxHP = max;
            UpdatePercent();
        }

        public void DrawLabel(GameTime gameTime)
        {
            CenterLabel();
            _label.Draw(gameTime);
        }
    }
}
