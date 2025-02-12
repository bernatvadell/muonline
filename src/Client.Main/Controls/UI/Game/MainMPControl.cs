using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Controls.UI.Game
{
    public class MainMPControl : TextureControl
    {
        private readonly MainMPStatusControl _progress;
        // Store the label separately so it can be drawn last.
        private readonly LabelControl _label;
        private int _currentMP;
        private int _maxMP;

        public int CurrentMP
        {
            get => _currentMP;
            set { _currentMP = value; UpdatePercent(); }
        }
        public int MaxMP
        {
            get => _maxMP;
            set { _maxMP = value; UpdatePercent(); }
        }

        public MainMPControl()
        {
            TexturePath = "Interface/GFx/main_IE.ozd";
            AutoViewSize = false;
            TextureRectangle = new Rectangle(339, 4, 86, 86);
            ViewSize = new Point(86, 86);
            Align = ControlAlign.Bottom;
            BlendState = BlendState.AlphaBlend;
            // Add the progress control as a normal child.
            Controls.Add(_progress = new MainMPStatusControl
            {
                Align = ControlAlign.Bottom,
                Margin = new Margin { Bottom = 3 }
            });
            // Create the label (but do not add it as a child)
            _label = new LabelControl
            {
                Align = ControlAlign.VerticalCenter | ControlAlign.HorizontalCenter,
                FontSize = 10
            };
        }

        public void UpdatePercent()
        {
            _progress.Percentage = _maxMP <= 0 ? 0 : (float)_currentMP / _maxMP;
            _label.Text = $"{_currentMP}/{_maxMP}";
        }

        public override void Draw(GameTime gameTime)
        {
            // Draw the texture and the progress control normally.
            base.Draw(gameTime);
        }

        public void DrawLabel(GameTime gameTime)
        {
            _label.X = 865; //TODO Caclucate this
            _label.Y = 658; //TODO Caclucate this
            _label.Draw(gameTime);
        }
    }
}
