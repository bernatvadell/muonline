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
        public WorldControl World { get; protected set; }

        public CursorControl Cursor { get; }
        public GameControl MouseControl { get; set; }
        public GameControl FocusedControl { get; set; }

        public BaseScene()
        {
            AutoSize = false;
            Controls.Add(Cursor = new CursorControl());
        }

        public void ChangeWorld<T>() where T : WorldControl, new()
        {
            World?.Dispose();
            Controls.Add(World = new T());
            Task.Run(() => World.Initialize());
        }

        public async Task ChangeWorldAsync<T>() where T : WorldControl, new()
        {
            World?.Dispose();
            Controls.Add(World = new T());
            await World.Initialize();
        }

        public override async Task Load()
        {
            await base.Load();

            Width = MuGame.Instance.Width;
            Height = MuGame.Instance.Height;
        }

        public override void Update(GameTime gameTime)
        {
            var currentMouseControl = MouseControl;
            MouseControl = null;

            base.Update(gameTime);

            if (Status != GameControlStatus.Ready)
                return;

            if (World == null) return;

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
            else if (currentMouseControl != null && currentMouseControl.IsMousePressed && MouseControl != currentMouseControl)
            {
                currentMouseControl.IsMousePressed = false;
            }

            Cursor.BringToFront();
        }

        public override void Draw(GameTime gameTime)
        {
            if (World == null) return;

            base.Draw(gameTime);
        }
    }
}
