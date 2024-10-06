using Client.Main.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
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
        public GameControl MouseControl { get; set; }
        public GameControl FocusedControl { get; set; }

        public BaseScene()
        {
            Controls.Add(Cursor = new CursorControl());
        }

        public override Task Initialize(GraphicsDevice graphicsDevice)
        {
            Width = MuGame.Instance.Width;
            Height = MuGame.Instance.Height;

            return base.Initialize(graphicsDevice);
        }

        public override void Update(GameTime gameTime)
        {
            var currentMouseControl = MouseControl;
            MouseControl = null;
            base.Update(gameTime);

            if (MouseControl != null && MuGame.Instance.Mouse.LeftButton == ButtonState.Pressed && !MouseControl.IsMousePressed)
            {
                MouseControl.IsMousePressed = true;
            }
            else if (MouseControl != null && MouseControl == currentMouseControl && MuGame.Instance.Mouse.LeftButton == ButtonState.Released && MouseControl.IsMousePressed)
            {
                MouseControl.IsMousePressed = false;
                MouseControl.OnClick();
                FocusedControl = MouseControl;
            }
            else if (currentMouseControl != null && currentMouseControl.IsMousePressed)
            {
                currentMouseControl.IsMousePressed = false;
            }

            Cursor.BringToFront();
        }
    }
}
