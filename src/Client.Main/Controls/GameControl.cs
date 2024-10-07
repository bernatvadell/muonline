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
    public enum GameControlStatus
    {
        NonInitialized,
        Initializing,
        Ready,
        Error,
        Disposed
    }

    public abstract class GameControl : IChildItem<GameControl>, IDisposable
    {
        public GraphicsDevice GraphicsDevice => MuGame.Instance.GraphicsDevice;
        public GameControl Parent { get; set; }
        public ChildrenCollection<GameControl> Controls { get; private set; }
        public GameControlStatus Status { get; private set; } = GameControlStatus.NonInitialized;

        public bool Interactive { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public float Scale { get; set; } = 1f;

        public virtual Rectangle Rectangle => new(0, 0, Width, Height);

        public int ScreenX => X + (Parent?.ScreenX ?? 0);
        public int ScreenY => Y + (Parent?.ScreenY ?? 0);
        public int ScreenWidth => Math.Min(Rectangle.Width, (Parent?.ScreenWidth ?? int.MaxValue));
        public int ScreenHeight => Math.Min(Rectangle.Height, (Parent?.ScreenHeight ?? int.MaxValue));
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

                var tasks = new Task[Controls.Count];

                await Load();

                for (int i = 0; i < Controls.Count; i++)
                    tasks[i] = Controls[i].Initialize();

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

            IsMouseOver = Interactive && MuGame.Instance.Mouse.Position.X >= Rectangle.X && MuGame.Instance.Mouse.Position.X <= Rectangle.X + Rectangle.Width &&
                MuGame.Instance.Mouse.Position.Y >= Rectangle.Y && MuGame.Instance.Mouse.Position.Y <= Rectangle.Y + Rectangle.Height;

            if (IsMouseOver)
                MuGame.Instance.ActiveScene.MouseControl = this;

            for (int i = 0; i < Controls.Count; i++)
                Controls[i].Update(gameTime);
        }

        public virtual void Draw(GameTime gameTime)
        {
            if (Status != GameControlStatus.Ready || !Visible) return;

            for (int i = 0; i < Controls.Count; i++)
                Controls[i].Draw(gameTime);
        }

        public virtual void Dispose()
        {
            var controls = Controls.ToArray();

            for (int i = 0; i < controls.Length; i++)
                controls[i].Dispose();

            Controls.Clear();

            Status = GameControlStatus.Disposed;
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

        protected void DrawBorder()
        {
            var rectangle = new Rectangle(new Point(ScreenX, ScreenY), new Point(ScreenWidth, ScreenHeight));
            int borderWidth = 2;
            var fillColor = Color.White;
            var borderColor = Color.Red;
            var spriteBatch = MuGame.Instance.SpriteBatch;

            spriteBatch.Draw(MuGame.Instance.Pixel, new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height), fillColor);
            spriteBatch.Draw(MuGame.Instance.Pixel, new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, borderWidth), borderColor);
            spriteBatch.Draw(MuGame.Instance.Pixel, new Rectangle(rectangle.X, rectangle.Y + rectangle.Height - borderWidth, rectangle.Width, borderWidth), borderColor);
            spriteBatch.Draw(MuGame.Instance.Pixel, new Rectangle(rectangle.X, rectangle.Y, borderWidth, rectangle.Height), borderColor);
            spriteBatch.Draw(MuGame.Instance.Pixel, new Rectangle(rectangle.X + rectangle.Width - borderWidth, rectangle.Y, borderWidth, rectangle.Height), borderColor);
        }
    }
}
