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
            TileY = 0;
            BlendState = BlendState.AlphaBlend;
            TexturePath = "Interface/message_ok_b_all.tga";
        }


        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (MuGame.Instance.ActiveScene.MouseControl == this && IsMousePressed)
                TileY = 2;
            else if (MuGame.Instance.ActiveScene.MouseControl == this)
                TileY = 1;
            else
                TileY = 0;
        }
    }
}
