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
        private float _targetPercentage;
        private const float LerpSpeed = 5f;

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
            TextureRectangle = new Rectangle(336, 0, 86, 86);
            ViewSize = new Point(86, 86);
            BlendState = BlendState.AlphaBlend;
            // Add the progress control as a normal child.
            Controls.Add(_progress = new MainMPStatusControl
            {
                Align = ControlAlign.Bottom,
                Margin = new Margin { Bottom = 2 }
            });
            // Create the label (but do not add it as a child)
            _label = new LabelControl
            {
                Align = ControlAlign.VerticalCenter | ControlAlign.HorizontalCenter,
                FontSize = 10,
                IsBold = true,
                ShadowOpacity = 0.3f,
                BoldWeight = 1
            };

            _targetPercentage = _progress.Percentage;
        }

        public void UpdatePercent()
        {
            _targetPercentage = MaxMP <= 0 ? 0 : (float)_currentMP / MaxMP;
            _label.Text = $"{CurrentMP}/{MaxMP}";
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            float lerpAmount = LerpSpeed * deltaTime;
            _progress.Percentage = MathHelper.Lerp(_progress.Percentage, _targetPercentage, lerpAmount);
        }

        public override void Draw(GameTime gameTime)
        {
            // Draw the texture and the progress control normally.
            base.Draw(gameTime);
        }

        private void CenterLabel()
        {
            var rect = DisplayRectangle;
            _label.X = rect.X + (rect.Width - _label.ControlSize.X) / 2;
            _label.Y = rect.Y + (rect.Height - _label.ControlSize.Y) / 2;
        }

        public void SetValues(int current, int max)
        {
            _currentMP = current;
            _maxMP  = max;
            UpdatePercent();
        }

        public void DrawLabel(GameTime gameTime)
        {
            CenterLabel();
            _label.Draw(gameTime);
        }
    }
}
