using Client.Main.Controls.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Controls
{
    public enum CursorStatus
    {
        Normal,
        AddIn,
        Attack,
        Attack2,
        DontMove,
        Eye,
        Get,
        Id,
        LeanAgainst,
        Push,
        Repair,
        SitDown,
        Talk
    }

    public class CursorControl : TextureControl
    {
        private CursorStatus Status { get; set; }

        public CursorControl()
        {
            BlendState = BlendState.AlphaBlend;
            TexturePath = "Interface/Cursor.tga";
        }

        public override void Update(GameTime gameTime)
        {
            var mouseState = Mouse.GetState();
            
            X = mouseState.X;
            Y = mouseState.Y;

            if (mouseState.LeftButton == ButtonState.Pressed)
                TexturePath = "Interface/CursorPush.tga";
            else
                TexturePath = "Interface/Cursor.tga";

            base.Update(gameTime);
        }
    }
}
