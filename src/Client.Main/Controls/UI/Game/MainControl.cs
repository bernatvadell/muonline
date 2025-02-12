using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Controls.UI.Game
{
    public class MainControl : UIControl
    {
        // Keep references to the HP/MP controls so we can later draw their labels.
        private readonly MainHPControl _hpControl;
        private readonly MainMPControl _mpControl;

        public MainControl()
        {
            Align = ControlAlign.HorizontalCenter | ControlAlign.Bottom;

            _mpControl = new MainMPControl
            {
                X = 151 + 88 + 143 + 78 + 76 + 140,
                Margin = new Margin { Bottom = 16 },
                Align = ControlAlign.Bottom,
                BlendState = BlendState.AlphaBlend,
                CurrentMP = 4500,
                MaxMP = 4500
            };

            _hpControl = new MainHPControl
            {
                X = 154,
                Margin = new Margin { Bottom = 16 },
                Align = ControlAlign.Bottom,
                CurrentHP = 500,
                MaxHP = 854
            };

            // Add HP/MP controls first – note that their label elements are now drawn later.
            Controls.Add(_mpControl);
            Controls.Add(_hpControl);

            // Add the rest of the texture controls in the desired order.
            Controls.Add(new TextureControl
            {
                TexturePath = "Interface/GFx/main_IE.ozd",
                TextureRectangle = new Rectangle(0, 109, 151, 64),
                AutoViewSize = false,
                ViewSize = new Point(151, 64),
                Align = ControlAlign.Bottom,
                BlendState = BlendState.AlphaBlend
            });

            Controls.Add(new TextureControl
            {
                TexturePath = "Interface/GFx/main_IE.ozd",
                TextureRectangle = new Rectangle(105, 0, 88, 107),
                AutoViewSize = false,
                ViewSize = new Point(88, 107),
                X = 151,
                Align = ControlAlign.Bottom,
                BlendState = BlendState.AlphaBlend
            });

            Controls.Add(new TextureControl
            {
                TexturePath = "Interface/GFx/main_IE.ozd",
                TextureRectangle = new Rectangle(193, 0, 143, 91),
                AutoViewSize = false,
                ViewSize = new Point(143, 91),
                X = 151 + 88,
                Align = ControlAlign.Bottom,
                BlendState = BlendState.AlphaBlend
            });

            Controls.Add(new TextureControl
            {
                TexturePath = "Interface/GFx/main_IE.ozd",
                TextureRectangle = new Rectangle(660, 0, 78, 70),
                AutoViewSize = false,
                ViewSize = new Point(78, 70),
                X = 151 + 88 + 143,
                Align = ControlAlign.Bottom,
                BlendState = BlendState.AlphaBlend
            });

            Controls.Add(new TextureControl
            {
                TexturePath = "Interface/GFx/main_IE.ozd",
                TextureRectangle = new Rectangle(891, 0, 76, 67),
                AutoViewSize = false,
                ViewSize = new Point(76, 67),
                X = 151 + 88 + 143 + 78,
                Align = ControlAlign.Bottom,
                BlendState = BlendState.AlphaBlend
            });

            Controls.Add(new TextureControl
            {
                TexturePath = "Interface/GFx/main_IE.ozd",
                TextureRectangle = new Rectangle(518, 0, 142, 89),
                AutoViewSize = false,
                ViewSize = new Point(142, 89),
                X = 151 + 88 + 143 + 78 + 76,
                Align = ControlAlign.Bottom,
                BlendState = BlendState.AlphaBlend
            });

            Controls.Add(new TextureControl
            {
                TexturePath = "Interface/GFx/main_IE.ozd",
                TextureRectangle = new Rectangle(0, 0, 105, 107),
                AutoViewSize = false,
                ViewSize = new Point(105, 107),
                X = 151 + 88 + 143 + 78 + 76 + 142,
                Align = ControlAlign.Bottom,
                BlendState = BlendState.AlphaBlend
            });

            Controls.Add(new TextureControl
            {
                TexturePath = "Interface/GFx/main_IE.ozd",
                TextureRectangle = new Rectangle(738, 0, 150, 68),
                AutoViewSize = false,
                ViewSize = new Point(150, 68),
                X = 151 + 88 + 143 + 78 + 76 + 142 + 105,
                Align = ControlAlign.Bottom,
                BlendState = BlendState.AlphaBlend,
            });
        }

        public override void Draw(GameTime gameTime)
        {
            // First, draw all controls in the usual order.
            base.Draw(gameTime);

            // Then, explicitly draw the label elements for the HP and MP controls on top.
            _hpControl.DrawLabel(gameTime);
            _mpControl.DrawLabel(gameTime);
        }
    }
}
