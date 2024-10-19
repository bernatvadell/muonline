using Client.Main.Models;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Controls.UI.Game
{
    public class MainMPBackgroundControl : TextureControl
    {
        private readonly MainMPStatusControl _progress;
        private readonly LabelControl _label;
        private int _currentMP;
        private int _maxMP;

        public int CurrentMP { get => _currentMP; set { _currentMP = value; UpdatePercent(); } }
        public int MaxMP { get => _maxMP; set { _maxMP = value; UpdatePercent(); } }

        public MainMPBackgroundControl()
        {
            TexturePath = "Interface/GFx/main_IE.ozd";
            AutoSize = false;
            OffsetX = 339;
            OffsetY = 4;
            Width = 86;
            Height = 86;
            Align = ControlAlign.Bottom;
            BlendState = BlendState.AlphaBlend;
            Controls.Add(_progress = new MainMPStatusControl { Align = ControlAlign.Bottom });
            Controls.Add(_label = new LabelControl { Align = ControlAlign.VerticalCenter | ControlAlign.HorizontalCenter, FontSize = 8 });
        }

        public void UpdatePercent()
        {
            _progress.Percentage = _maxMP <= 0 ? 0 : (float)_currentMP / (float)_maxMP;
            _label.Text = $"{_currentMP}/{_maxMP}";
        }
    }
}
