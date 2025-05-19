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
            TileWidth = 54;
            TileHeight = 30;
            ViewSize = new Point(TileWidth, TileHeight); // Ensure ViewSize is set from tile dimensions
            TileY = 0;
            BlendState = Blendings.Alpha;
            TexturePath = "Interface/message_ok_b_all.tga";
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (IsMouseOver && IsMousePressed) // check IsMousePressed from GameControl for click visual
                TileY = 2;
            else if (IsMouseOver) // hover state
                TileY = 1;
            else
                TileY = 0;
        }
    }
}
