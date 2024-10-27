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

        public DialogControl()
        {
            Align = ControlAlign.HorizontalCenter | ControlAlign.VerticalCenter;
        }

        public void Close()
        {
            Closed?.Invoke(this, EventArgs.Empty);
            Dispose();
        }

        public void ShowDialog()
        {
            MuGame.Instance.ActiveScene.Controls.Add(this);
            AlignControl();
        }
    }
}
