using Client.Main.Models;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Controls.UI.Game
{
    public class MainHPBackgroundControl : TextureControl
    {
        private readonly MainHPStatusControl _progress;
        private readonly LabelControl _label;
        private int _currentHP;
        private int _maxHP;

        public int CurrentHP { get => _currentHP; set { _currentHP = value; UpdatePercent(); } }
        public int MaxHP { get => _maxHP; set { _maxHP = value; UpdatePercent(); } }

        public MainHPBackgroundControl()
        {
            TexturePath = "Interface/GFx/main_IE.ozd";
            AutoSize = false;
            OffsetX = 428;
            OffsetY = 4;
            Width = 86;
            Height = 86;
            BlendState = BlendState.AlphaBlend;
            Controls.Add(_progress = new MainHPStatusControl { Align = ControlAlign.Bottom });
            Controls.Add(_label = new LabelControl { Align = ControlAlign.VerticalCenter | ControlAlign.HorizontalCenter, FontSize = 8 });
        }

        public void UpdatePercent()
        {
            _progress.Percentage = MaxHP <= 0 ? 0 : (float)CurrentHP / (float)MaxHP;
            _label.Text = $"{CurrentHP}/{MaxHP}";
        }
    }
}
