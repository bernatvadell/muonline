using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Client.Main.Controls.UI
{

    public class OkButton : SpriteControl
    {
        public OkButton()
        {
            Interactive = true;
            ElementWidth = 54;
            ElementHeight = 30;
            Position = 0;
            BlendState = BlendState.AlphaBlend;
            TexturePath = "Interface/message_ok_b_all.tga";
        }


        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (MuGame.Instance.Mouse.LeftButton == ButtonState.Pressed && MuGame.Instance.ActiveScene.MouseControl == this)
                Position = 2;
            else if (MuGame.Instance.ActiveScene.MouseControl == this)
                Position = 1;
            else
                Position = 0;
        }
    }
}
