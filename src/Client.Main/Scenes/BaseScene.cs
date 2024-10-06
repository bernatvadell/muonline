using Client.Main.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Scenes
{
    public abstract class BaseScene : GameControl
    {
        public CursorControl Cursor { get; }

        public BaseScene()
        {
            Controls.Add(Cursor = new CursorControl());
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            Cursor.BringToFront();
        }
    }
}
