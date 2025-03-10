using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Linq;

namespace Client.Main.Controls.UI.Game
{
    public class NpcShopControl : DynamicLayoutControl
    {
        protected override string LayoutJsonResource => "Client.Main.Controls.UI.Game.Layouts.NpcShopLayout.json";
        protected override string TextureRectJsonResource => "Client.Main.Controls.UI.Game.Layouts.NpcShopRect.json";
        protected override string DefaultTexturePath => "Interface/GFx/NpcShop_I3.ozd";
        private static NpcShopControl _instance;

        public NpcShopControl()
        {
            Visible = false;

            var rows = 14;
            var cols = 8;

            var ScreenX = 170;
            var ScreenY = 180;
            for(var i = 0; i < rows; i++)
            {
                for(var j = 0; j < cols; j++)
                {
                    var textureCtrl = new TextureControl
                    {
                        AutoViewSize = false,
                        TexturePath = DefaultTexturePath,
                        BlendState = BlendState.AlphaBlend,
                        Name = "Cell-" + i + j,
                    };
                    textureCtrl.TextureRectangle = new Rectangle
                    {
                        X = 545,
                        Y = 217,
                        Width = 29,
                        Height = 31
                    };
                    textureCtrl.Tag = new LayoutInfo
                    {
                        Name = "Cell",
                        ScreenX = ScreenX,
                        ScreenY = ScreenY,
                        Width = 29,
                        Height = 29,
                        Z = 5
                    };
                    Controls.Add(textureCtrl);
                    ScreenX += 25;
                }
                ScreenY += 25;
                ScreenX = 170;
            }
           
        }
        public static NpcShopControl Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new NpcShopControl();
                }
                return _instance;
            }
        }
        public override void Update(GameTime gameTime)
        {
            KeyboardState newState = Keyboard.GetState();
            if (newState.IsKeyDown(Keys.Escape))
            {
                Visible = false;
            }

            //

            base.Update(gameTime);
        }
    }
}