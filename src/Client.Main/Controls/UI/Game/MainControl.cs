using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Net.NetworkInformation;

namespace Client.Main.Controls.UI.Game
{
    public class MainControl : UIControl
    {
        public MainControl()
        {
            Align = ControlAlign.HorizontalCenter | ControlAlign.Bottom;

            Controls.Add(new MainMPControl
            {
                X = 151 + 88 + 143 + 78 + 76 + 140,
                Margin = new Margin { Bottom = 16 },
                Align = ControlAlign.Bottom,
                BlendState = BlendState.AlphaBlend,
                CurrentMP = 4500,
                MaxMP = 4500
            });

            Controls.Add(new MainHPControl
            {
                X = 154,
                Margin = new Margin { Bottom = 16 },
                Align = ControlAlign.Bottom,
                CurrentHP = 500,
                MaxHP = 854
            });

            Controls.Add(new TextureControl
            {
                TexturePath = "Interface/GFx/main_IE.ozd",
                TextureRectangle = new Rectangle(0, 109, 151, 64),
                AutoViewSize = false,
                ViewSize = new(151, 64),
                Align = ControlAlign.Bottom,
                BlendState = BlendState.AlphaBlend
            });

            Controls.Add(new TextureControl
            {
                TexturePath = "Interface/GFx/main_IE.ozd",
                TextureRectangle = new Rectangle(105, 0, 88, 107),
                AutoViewSize = false,
                ViewSize = new(88, 107),
                X = 151,
                Align = ControlAlign.Bottom,
                BlendState = BlendState.AlphaBlend
            });

            Controls.Add(new TextureControl
            {
                TexturePath = "Interface/GFx/main_IE.ozd",
                TextureRectangle = new Rectangle(193, 0, 143, 91),
                AutoViewSize = false,
                ViewSize = new(143, 91),
                X = 151 + 88,
                Align = ControlAlign.Bottom,
                BlendState = BlendState.AlphaBlend
            });

            Controls.Add(new TextureControl
            {
                TexturePath = "Interface/GFx/main_IE.ozd",
                TextureRectangle = new Rectangle(660, 0, 78, 70),
                AutoViewSize = false,
                ViewSize = new(78, 70),
                X = 151 + 88 + 143,
                Align = ControlAlign.Bottom,
                BlendState = BlendState.AlphaBlend
            });

            Controls.Add(new TextureControl
            {
                TexturePath = "Interface/GFx/main_IE.ozd",
                TextureRectangle = new Rectangle(891, 0, 76, 67),
                AutoViewSize = false,
                ViewSize = new(76, 67),
                X = 151 + 88 + 143 + 78,
                Align = ControlAlign.Bottom,
                BlendState = BlendState.AlphaBlend
            });

            Controls.Add(new TextureControl
            {
                TexturePath = "Interface/GFx/main_IE.ozd",
                TextureRectangle = new Rectangle(518, 0, 142, 89),
                AutoViewSize = false,
                ViewSize = new(142, 89),
                X = 151 + 88 + 143 + 78 + 76,
                Align = ControlAlign.Bottom,
                BlendState = BlendState.AlphaBlend
            });

            Controls.Add(new TextureControl
            {
                TexturePath = "Interface/GFx/main_IE.ozd",
                AutoViewSize = false,
                TextureRectangle = new Rectangle(0, 0, 105, 107),
                ViewSize = new(105, 107),
                X = 151 + 88 + 143 + 78 + 76 + 142,
                Align = ControlAlign.Bottom,
                BlendState = BlendState.AlphaBlend
            });

            Controls.Add(new TextureControl
            {
                TexturePath = "Interface/GFx/main_IE.ozd",
                TextureRectangle = new Rectangle(738, 0, 150, 68),
                AutoViewSize = false,
                ViewSize = new(150, 68),
                X = 151 + 88 + 143 + 78 + 76 + 142 + 105,
                Align = ControlAlign.Bottom,
                BlendState = BlendState.AlphaBlend,
            });
        }
    }
}
