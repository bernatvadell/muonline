using Client.Main.Models;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Controls.UI
{
    public abstract class DialogControl : UIControl
    {
        public event EventHandler Closed;

        public void Close()
        {
            Closed?.Invoke(this, EventArgs.Empty);
            Dispose();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            X = (int)(GraphicsDevice.Viewport.Width - Width) / 2;
            Y = (int)(GraphicsDevice.Viewport.Height - Height) / 2;
        }

        public void ShowDialog()
        {
            MuGame.Instance.ActiveScene.Controls.Add(this);
            AlignControl();
        }
    }
}
