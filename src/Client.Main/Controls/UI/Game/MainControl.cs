using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Controls.UI.Game
{
    public class MainControl : UIControl
    {
        public MainControl()
        {
            Align = ControlAlign.HorizontalCenter | ControlAlign.Bottom;

            Controls.Add(new TextureControl
            {
                TexturePath = "Interface/GFx/main_IE.ozd",
                AutoSize = false,
                OffsetX = 339,
                OffsetY = 2,
                Width = 86,
                Height = 86,
                X = 151 + 88 + 143 + 78 + 76 + 140,
                Margin = new Margin { Bottom = 17 },
                Align = ControlAlign.Bottom,
                BlendState = BlendState.AlphaBlend
            });

            Controls.Add(new TextureControl
            {
                TexturePath = "Interface/GFx/main_IE.ozd",
                AutoSize = false,
                OffsetX = 430,
                OffsetY = 2,
                Width = 86,
                Height = 86,
                X = 153,
                Margin = new Margin { Bottom = 17 },
                Align = ControlAlign.Bottom,
                BlendState = BlendState.AlphaBlend
            });

            Controls.Add(new TextureControl
            {
                TexturePath = "Interface/GFx/main_IE.ozd",
                AutoSize = false,
                OffsetX = 0,
                OffsetY = 109,
                Width = 151,
                Height = 64,
                Align = ControlAlign.Bottom,
                BlendState = BlendState.AlphaBlend
            });

            Controls.Add(new TextureControl
            {
                TexturePath = "Interface/GFx/main_IE.ozd",
                AutoSize = false,
                OffsetX = 105,
                OffsetY = 0,
                Width = 88,
                Height = 107,
                X = 151,
                Align = ControlAlign.Bottom,
                BlendState = BlendState.AlphaBlend
            });

            Controls.Add(new TextureControl
            {
                TexturePath = "Interface/GFx/main_IE.ozd",
                AutoSize = false,
                OffsetX = 193,
                OffsetY = 0,
                Width = 143,
                Height = 91,
                X = 151 + 88,
                Align = ControlAlign.Bottom,
                BlendState = BlendState.AlphaBlend
            });

            Controls.Add(new TextureControl
            {
                TexturePath = "Interface/GFx/main_IE.ozd",
                AutoSize = false,
                OffsetX = 660,
                OffsetY = 0,
                Width = 78,
                Height = 70,
                X = 151 + 88 + 143,
                Align = ControlAlign.Bottom,
                BlendState = BlendState.AlphaBlend
            });

            Controls.Add(new TextureControl
            {
                TexturePath = "Interface/GFx/main_IE.ozd",
                AutoSize = false,
                OffsetX = 891,
                OffsetY = 0,
                Width = 76,
                Height = 67,
                X = 151 + 88 + 143 + 78,
                Align = ControlAlign.Bottom,
                BlendState = BlendState.AlphaBlend
            });

            Controls.Add(new TextureControl
            {
                TexturePath = "Interface/GFx/main_IE.ozd",
                AutoSize = false,
                OffsetX = 518,
                OffsetY = 0,
                Width = 142,
                Height = 89,
                X = 151 + 88 + 143 + 78 + 76,
                Align = ControlAlign.Bottom,
                BlendState = BlendState.AlphaBlend
            });

            Controls.Add(new TextureControl
            {
                TexturePath = "Interface/GFx/main_IE.ozd",
                AutoSize = false,
                OffsetX = 0,
                OffsetY = 0,
                Width = 105,
                Height = 107,
                X = 151 + 88 + 143 + 78 + 76 + 142,
                Align = ControlAlign.Bottom,
                BlendState = BlendState.AlphaBlend
            });

            Controls.Add(new TextureControl
            {
                TexturePath = "Interface/GFx/main_IE.ozd",
                AutoSize = false,
                OffsetX = 738,
                OffsetY = 0,
                Width = 150,
                Height = 68,
                X = 151 + 88 + 143 + 78 + 76 + 142 + 105,
                Align = ControlAlign.Bottom,
                BlendState = BlendState.AlphaBlend
            });
        }
    }
}
