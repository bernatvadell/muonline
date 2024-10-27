using Client.Main.Controllers;
using Client.Main.Models;
using Client.Main.Scenes;
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
        public GameControl Root => Parent?.Root ?? this;
        public GameControl Parent { get; set; }
        public BaseScene Scene => this is BaseScene scene ? scene : Parent?.Scene;
        public virtual WorldControl World => Scene?.World;

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

        public Color BorderColor { get; set; }
        public int BorderThickness { get; set; } = 0;
        public Color BackgroundColor { get; set; } = Color.Transparent;

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

                var tasks = new Task[Controls.Count];

                var controls = Controls.ToArray();

                for (int i = 0; i < controls.Length; i++)
                {
                    var control = controls[i];

                    if (control.Status == GameControlStatus.NonInitialized)
                        tasks[i] = control.Initialize();
                    else
                        tasks[i] = Task.CompletedTask;
                }

                await Task.WhenAll(tasks);

                await Load();

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

            if (IsMouseOver && Scene != null)
                Scene.MouseControl = this;

            int maxWidth = 0;
            int maxHeight = 0;

            for (int i = 0; i < Controls.Count; i++)
            {
                var control = Controls[i];
                control.Update(gameTime);

                var controlWidth = (int)(control.Width * control.Scale) + control.Margin.Left;
                var controlHeight = (int)(control.Height * control.Scale) + control.Margin.Top;

                if (!Align.HasFlag(ControlAlign.Left))
                    controlWidth += control.X;

                if (!Align.HasFlag(ControlAlign.Bottom))
                    controlHeight += control.Y;

                if (controlWidth > maxWidth)
                    maxWidth = controlWidth;

                if (controlHeight > maxHeight)
                    maxHeight = controlHeight;
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

            if (Align == ControlAlign.None)
                return;

            if (Align.HasFlag(ControlAlign.Top))
                Y = 0;
            else if (Align.HasFlag(ControlAlign.Bottom))
                Y = (int)(Parent.Height - (Height * Scale));
            else if (Align.HasFlag(ControlAlign.VerticalCenter))
                Y = (int)((Parent.Height / 2) - (Height / 2) * Scale);

            if (Align.HasFlag(ControlAlign.Left))
                X = 0;
            else if (Align.HasFlag(ControlAlign.Right))
                X = (int)(Parent.Width - Width * Scale);
            else if (Align.HasFlag(ControlAlign.HorizontalCenter))
                X = (int)((Parent.Width / 2) - (Width / 2) * Scale);
        }

        public virtual void Draw(GameTime gameTime)
        {
            if (Status != GameControlStatus.Ready || !Visible)
                return;

            GraphicsManager.Instance.Sprite.Begin();
            DrawBackground();
            DrawBorder();
            GraphicsManager.Instance.Sprite.End();

            GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.BlendState = BlendState.Opaque;


            for (int i = 0; i < Controls.Count; i++)
                Controls[i].Draw(gameTime);
        }

        public virtual void DrawAfter(GameTime gameTime)
        {
            if (Status != GameControlStatus.Ready || !Visible)
                return;

            for (int i = 0; i < Controls.Count; i++)
                Controls[i].DrawAfter(gameTime);
        }


        public virtual void Dispose()
        {
            var controls = Controls.ToArray();

            Parallel.For(0, controls.Length, i => controls[i].Dispose());

            Controls.Clear();

            Parent?.Controls.Remove(this);

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

        public void SendToBack()
        {
            if (Status == GameControlStatus.Disposed) return;
            if (Parent == null) return;
            if (Parent.Controls[0] == this) return;
            var parent = Parent;
            Parent.Controls.Remove(this);
            parent.Controls.Insert(0, this);
        }

        protected void DrawBackground()
        {
            if (BackgroundColor == Color.Transparent)
                return;

            GraphicsManager.Instance.Sprite.Draw(
                GraphicsManager.Instance.Pixel,
                ScreenLocation,
                BackgroundColor
            );
        }

        protected void DrawBorder()
        {
            if (BorderThickness <= 0)
                return;

            GraphicsManager.Instance.Sprite.Draw(GraphicsManager.Instance.Pixel, new Rectangle(ScreenLocation.X, ScreenLocation.Y, ScreenLocation.Width, BorderThickness), BorderColor);
            GraphicsManager.Instance.Sprite.Draw(GraphicsManager.Instance.Pixel, new Rectangle(ScreenLocation.X, ScreenLocation.Y + ScreenLocation.Height - BorderThickness, ScreenLocation.Width, BorderThickness), BorderColor);
            GraphicsManager.Instance.Sprite.Draw(GraphicsManager.Instance.Pixel, new Rectangle(ScreenLocation.X, ScreenLocation.Y, BorderThickness, ScreenLocation.Height), BorderColor);
            GraphicsManager.Instance.Sprite.Draw(GraphicsManager.Instance.Pixel, new Rectangle(ScreenLocation.X + ScreenLocation.Width - BorderThickness, ScreenLocation.Y, BorderThickness, ScreenLocation.Height), BorderColor);
        }
    }
}
