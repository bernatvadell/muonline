using Client.Main.Controls;
using Client.Main.Controls.UI;
using Client.Main.Models;
using Client.Main.Objects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Threading.Tasks;

namespace Client.Main.Scenes
{
    public abstract class BaseScene : GameControl
    {
        public new WorldControl World { get; protected set; }

        public CursorControl Cursor { get; }
        public GameControl MouseControl { get; set; }
        public GameControl MouseHoverControl { get; set; }
        public DebugPanel DebugPanel { get; }

        public WorldObject MouseHoverObject { get; set; }

        public BaseScene()
        {
            AutoSize = false;
            Controls.Add(DebugPanel = new DebugPanel());
            Controls.Add(Cursor = new CursorControl());
        }

        public void ChangeWorld<T>() where T : WorldControl, new()
        {
            Task.Run(() => ChangeWorldAsync<T>()).ConfigureAwait(false);
        }

        public async Task ChangeWorldAsync<T>() where T : WorldControl, new()
        {
            World?.Dispose();
            Controls.Add(World = new T());
            await World.Initialize();
        }

        public override async Task Load()
        {
            Width = MuGame.Instance.Width;
            Height = MuGame.Instance.Height;
            await base.Load();
        }

        public override void Update(GameTime gameTime)
        {
            var currentMouseControl = MouseControl;
            MouseControl = null;
            MouseHoverObject = null;

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
                MouseHoverControl = MouseControl;
            }
            else if (currentMouseControl != null && currentMouseControl.IsMousePressed && MouseControl != currentMouseControl)
            {
                currentMouseControl.IsMousePressed = false;
            }

            if (MouseHoverObject != null && MuGame.Instance.PrevMouseState.LeftButton == ButtonState.Pressed && MuGame.Instance.Mouse.LeftButton == ButtonState.Released)
            {
                MouseHoverObject.OnClick();
            }

            DebugPanel.BringToFront();
            Cursor.BringToFront();
        }

        public override void Draw(GameTime gameTime)
        {
            if (World == null) return;

            base.Draw(gameTime);
        }
    }
}
