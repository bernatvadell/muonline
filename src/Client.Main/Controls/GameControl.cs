using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Controls
{

    public abstract class GameControl : IChildItem<GameControl>, IDisposable
    {
        public GraphicsDevice GraphicsDevice => MuGame.Instance.GraphicsDevice;
        public GameControl Parent { get; set; }
        public ChildrenCollection<GameControl> Controls { get; private set; }
        public GameControlStatus Status { get; private set; } = GameControlStatus.NonInitialized;

        public ControlAlign Align { get; set; }
        public bool AutoSize { get; set; } = true;
        public bool Interactive { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public float Scale { get; set; } = 1f;
        public Margin Margin { get; set; } = Margin.Empty;

        public virtual Rectangle ScreenLocation => new(
            (Parent?.ScreenLocation.X ?? 0) + X + Margin.Left - Margin.Right,
            (Parent?.ScreenLocation.Y ?? 0) + Y + Margin.Top - Margin.Bottom,
            (int)(Width * Scale),
            (int)(Height * Scale)
        );

        public virtual Rectangle Viewport => new(
            ScreenLocation.X,
            ScreenLocation.Y,
            ScreenLocation.Width + Margin.Right,
            ScreenLocation.Height + Margin.Bottom
        );

        public bool Visible { get; set; } = true;

        public event EventHandler Click;

        public bool IsMouseOver { get; set; }
        public bool IsMousePressed { get; set; }
        public bool HasFocus { get; set; }

        protected GameControl()
        {
            Controls = new ChildrenCollection<GameControl>(this);
        }

        public virtual void OnClick()
        {
            HasFocus = true;
            Click?.Invoke(this, EventArgs.Empty);
        }

        public virtual async Task Initialize()
        {
            if (Status != GameControlStatus.NonInitialized) return;

            try
            {
                Status = GameControlStatus.Initializing;
               
                await Load();

                var tasks = new Task[Controls.Count];

                var controls = Controls.ToArray();

                for (int i = 0; i < controls.Length; i++)
                    tasks[i] = controls[i].Initialize();

                await Task.WhenAll(tasks);

                AfterLoad();

                Status = GameControlStatus.Ready;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                Status = GameControlStatus.Error;
            }
        }

        public virtual Task Load()
        {
            return Task.CompletedTask;
        }

        public virtual void AfterLoad()
        {
            for (int i = 0; i < Controls.Count; i++)
                Controls[i].AfterLoad();
        }

        public virtual void Update(GameTime gameTime)
        {
            if (Status == GameControlStatus.NonInitialized && (Parent == null || Parent.Status == GameControlStatus.Ready))
                Task.Run(() => Initialize());

            if (Status != GameControlStatus.Ready || !Visible) return;

            IsMouseOver = Interactive && MuGame.Instance.Mouse.Position.X >= ScreenLocation.X && MuGame.Instance.Mouse.Position.X <= ScreenLocation.X + ScreenLocation.Width &&
                MuGame.Instance.Mouse.Position.Y >= ScreenLocation.Y && MuGame.Instance.Mouse.Position.Y <= ScreenLocation.Y + ScreenLocation.Height;

            if (IsMouseOver)
                MuGame.Instance.ActiveScene.MouseControl = this;

            int maxWidth = 0;
            int maxHeight = 0;

            for (int i = 0; i < Controls.Count; i++)
            {
                var control = Controls[i];
                control.Update(gameTime);

                if ((control.X + control.Width) > maxWidth)
                    maxWidth = control.X + control.ScreenLocation.Width;

                if ((control.Y + control.Height) > maxHeight)
                    maxHeight = control.Y + control.ScreenLocation.Height;
            }

            if (AutoSize)
            {
                Width = maxWidth;
                Height = maxHeight;
            }

            if (Align != ControlAlign.None)
                AlignControl();
        }

        protected virtual void AlignControl()
        {
            if (Parent == null)
            {
                Y = 0;
                X = 0;
                return;
            }

            if (Align.HasFlag(ControlAlign.Top))
                Y = 0;
            else if (Align.HasFlag(ControlAlign.Bottom))
                Y = Parent.Height - Height;
            else if (Align.HasFlag(ControlAlign.VerticalCenter))
                Y = (Parent.Height / 2) - (Height / 2);

            if (Align.HasFlag(ControlAlign.Left))
                X = 0;
            else if (Align.HasFlag(ControlAlign.Right))
                X = Parent.Width - Width;
            else if (Align.HasFlag(ControlAlign.HorizontalCenter))
                X = (Parent.Width / 2) - (Width / 2);
        }

        public virtual void Draw(GameTime gameTime)
        {
            if (Status != GameControlStatus.Ready || !Visible) return;

            for (int i = 0; i < Controls.Count; i++)
                Controls[i].Draw(gameTime);
        }

        public virtual void DrawAfter(GameTime gameTime)
        {
            if (Status != GameControlStatus.Ready || !Visible) return;

            for (int i = 0; i < Controls.Count; i++)
                Controls[i].DrawAfter(gameTime);
        }

        public virtual void Dispose()
        {
            var controls = Controls.ToArray();

            for (int i = 0; i < controls.Length; i++)
                controls[i].Dispose();

            Controls.Clear();

            Status = GameControlStatus.Disposed;

            GC.SuppressFinalize(this);
        }

        public void BringToFront()
        {
            if (Status == GameControlStatus.Disposed) return;
            if (Parent == null) return;
            if (Parent.Controls[^1] == this) return;
            var parent = Parent;
            Parent.Controls.Remove(this);
            parent.Controls.Add(this);
        }

        public void SendToBack()
        {
            if (Status == GameControlStatus.Disposed) return;
            if (Parent == null) return;
            if (Parent.Controls[0] == this) return;
            var parent = Parent;
            Parent.Controls.Remove(this);
            parent.Controls.Insert(0, this);
        }

        protected void DrawBorder()
        {
            int borderWidth = 2;
            var fillColor = Color.White;
            var borderColor = Color.Red;
            var spriteBatch = MuGame.Instance.SpriteBatch;


            spriteBatch.Draw(MuGame.Instance.Pixel, new Rectangle(Viewport.X, Viewport.Y, Viewport.Width, Viewport.Height), fillColor);
            spriteBatch.Draw(MuGame.Instance.Pixel, new Rectangle(Viewport.X, Viewport.Y, Viewport.Width, borderWidth), borderColor);
            spriteBatch.Draw(MuGame.Instance.Pixel, new Rectangle(Viewport.X, Viewport.Y + Viewport.Height - borderWidth, Viewport.Width, borderWidth), borderColor);
            spriteBatch.Draw(MuGame.Instance.Pixel, new Rectangle(Viewport.X, Viewport.Y, borderWidth, Viewport.Height), borderColor);
            spriteBatch.Draw(MuGame.Instance.Pixel, new Rectangle(Viewport.X + Viewport.Width - borderWidth, Viewport.Y, borderWidth, Viewport.Height), borderColor);
        }
    }
}
