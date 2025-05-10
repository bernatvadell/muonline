using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Controls.UI;
using Client.Main.Helpers;
using Client.Main.Models;
using Client.Main.Objects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
        public GameControl FocusControl { get; set; }
        public DebugPanel DebugPanel { get; }

        public WorldObject MouseHoverObject { get; set; }
        public bool IsMouseHandledByUI { get; set; }

        public BaseScene()
        {
            AutoViewSize = false;
            ViewSize = new(MuGame.Instance.Width, MuGame.Instance.Height);

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

        public override void Update(GameTime gameTime)
        {
            var currentFocusControl = FocusControl;
            var currentMouseControl = MouseControl;

            MouseControl = null;
            MouseHoverObject = null;

            base.Update(gameTime);

            if (Status != GameControlStatus.Ready)
                return;

            if (World == null) return;

            if (FocusControl != currentFocusControl)
            {
                currentFocusControl?.OnBlur();
                FocusControl?.OnFocus();
            }

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
            if (World == null)
                return;

            World.Draw(gameTime);

            World.DrawAfter(gameTime);

            // UI 2-D
            using (new SpriteBatchScope(
                       GraphicsManager.Instance.Sprite,
                       SpriteSortMode.Deferred,
                       BlendState.AlphaBlend,
                       SamplerState.PointClamp,
                       DepthStencilState.None))
            {
                for (int i = 0; i < Controls.Count; i++)
                {
                    var ctrl = Controls[i];
                    if (ctrl != World && ctrl.Visible)
                        ctrl.Draw(gameTime);
                }
            }
        }
    }
}
