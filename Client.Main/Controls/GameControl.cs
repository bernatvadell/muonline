using Client.Main.Controllers;
using Client.Main.Models;
using Client.Main.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Client.Main.Controls
{
    public abstract class GameControl : IChildItem<GameControl>, IDisposable
    {
        private Point _controlSize, _viewSize;

        public GraphicsDevice GraphicsDevice => MuGame.Instance.GraphicsDevice;
        public GameControl Root => Parent?.Root ?? this;
        public GameControl Parent { get; set; }
        public BaseScene Scene => this is BaseScene scene ? scene : Parent?.Scene;
        public virtual WorldControl World => Scene?.World;

        public ChildrenCollection<GameControl> Controls { get; private set; }
        public GameControlStatus Status { get; private set; } = GameControlStatus.NonInitialized;
        public ControlAlign Align { get; set; }
        public bool AutoViewSize { get; set; } = true;
        public bool Interactive { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public Point ControlSize { get => _controlSize; set => _controlSize = value; }
        public Point ViewSize
        {
            get => _viewSize;
            set { if (_viewSize != value) { _viewSize = value; OnScreenSizeChanged(); } }
        }
        public float Scale { get; set; } = 1f;
        public Margin Margin { get; set; } = Margin.Empty;
        public Margin Padding { get; set; } = new Margin();
        public Color BorderColor { get; set; }
        public int BorderThickness { get; set; } = 0;
        public Color BackgroundColor { get; set; } = Color.Transparent;
        public Point Offset { get; set; }
        public virtual Point DisplayPosition => new(
            (Parent?.DisplayRectangle.X ?? 0) + X + Margin.Left - Margin.Right + Offset.X,
            (Parent?.DisplayRectangle.Y ?? 0) + Y + Margin.Top - Margin.Bottom + Offset.Y
        );
        public virtual Point DisplaySize => new((int)(ViewSize.X * Scale), (int)(ViewSize.Y * Scale));
        public virtual Rectangle DisplayRectangle => new(DisplayPosition, DisplaySize);
        public bool Visible { get; set; } = true;

        // Added property for storing additional data (e.g., design info)
        public object Tag { get; set; }
        // Added Name property to identify controls
        public string Name { get; set; }
        // Added Alpha property for controlling transparency (1 = fully opaque)
        public float Alpha { get; set; } = 1f;

        public event EventHandler Click;
        public event EventHandler SizeChanged;
        public event EventHandler Focus;
        public event EventHandler Blur;

        public bool IsMouseOver { get; set; }
        public bool IsMousePressed { get; set; }
        public bool HasFocus => Scene?.FocusControl == this;

        protected GameControl()
        {
            Controls = new ChildrenCollection<GameControl>(this);
        }

        public virtual void OnClick()
        {
            MuGame.Instance.ActiveScene.FocusControl = this;
            Click?.Invoke(this, EventArgs.Empty);
        }

        public virtual void OnFocus()
        {
            Focus?.Invoke(this, EventArgs.Empty);
        }

        public virtual void OnBlur()
        {
            Blur?.Invoke(this, EventArgs.Empty);
        }

        public virtual async Task Initialize()
        {
            if (Status != GameControlStatus.NonInitialized)
                return;

            try
            {
                Status = GameControlStatus.Initializing;

                var controls = Controls.ToArray();
                var taskList = new List<Task>(controls.Length);

                for (int i = 0; i < controls.Length; i++)
                {
                    var control = controls[i];
                    if (control.Status == GameControlStatus.NonInitialized)
                        taskList.Add(control.Initialize());
                }

                await Task.WhenAll(taskList);

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

            // Cache mouse and display rectangle to avoid repeated property lookups
            var mouse = MuGame.Instance.Mouse;
            Rectangle rect = DisplayRectangle;
            IsMouseOver = Interactive &&
                          (mouse.Position.X >= rect.X && mouse.Position.X <= rect.X + rect.Width &&
                           mouse.Position.Y >= rect.Y && mouse.Position.Y <= rect.Y + rect.Height);
            if (IsMouseOver && Scene != null)
                Scene.MouseControl = this;

            int maxWidth = 0, maxHeight = 0;
            int count = Controls.Count; // Cache count to avoid repeated property access
            for (int i = 0; i < count; i++)
            {
                var control = Controls[i];
                control.Update(gameTime);

                int controlWidth = control.DisplaySize.X + control.Margin.Left;
                int controlHeight = control.DisplaySize.Y + control.Margin.Top;

                if (!Align.HasFlag(ControlAlign.Left))
                    controlWidth += control.X;
                if (!Align.HasFlag(ControlAlign.Bottom))
                    controlHeight += control.Y;

                if (controlWidth > maxWidth)
                    maxWidth = controlWidth;
                if (controlHeight > maxHeight)
                    maxHeight = controlHeight;
            }

            if (AutoViewSize)
                ViewSize = new Point(Math.Max(ControlSize.X, maxWidth), Math.Max(ControlSize.Y, maxHeight));

            if (Align != ControlAlign.None)
                AlignControl();
        }

        public virtual void SetVisible(bool isVisible)
        {
            // Można dodać logikę tutaj, jeśli potrzebne przy zmianie widoczności
            // np. anulowanie animacji, zatrzymanie dźwięków itp.
            if (Visible != isVisible)
            {
                Visible = isVisible;
                // OnVisibilityChanged?.Invoke(this, EventArgs.Empty); // Opcjonalny event
            }
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
                Y = (int)(Parent.DisplaySize.Y - DisplaySize.Y);
            else if (Align.HasFlag(ControlAlign.VerticalCenter))
                Y = (int)((Parent.DisplaySize.Y / 2) - (DisplaySize.Y / 2));

            if (Align.HasFlag(ControlAlign.Left))
                X = 0;
            else if (Align.HasFlag(ControlAlign.Right))
                X = (int)(Parent.DisplaySize.X - DisplaySize.X);
            else if (Align.HasFlag(ControlAlign.HorizontalCenter))
                X = (int)((Parent.DisplaySize.X / 2) - (DisplaySize.X / 2));
        }

        public virtual void Draw(GameTime gameTime)
        {
            if (Status != GameControlStatus.Ready || !Visible)
                return;

            var sb = GraphicsManager.Instance.Sprite;
            try
            {
                sb.Begin();
                DrawBackground();
                DrawBorder();
            }
            finally
            {
                if (sb.GraphicsDevice != null)
                    sb.End();
            }

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
                DisplayRectangle,
                BackgroundColor
            );
        }

        protected void DrawBorder()
        {
            if (BorderThickness <= 0)
                return;

            GraphicsManager.Instance.Sprite.Draw(GraphicsManager.Instance.Pixel, new Rectangle(DisplayRectangle.X, DisplayRectangle.Y, DisplayRectangle.Width, BorderThickness), BorderColor);
            GraphicsManager.Instance.Sprite.Draw(GraphicsManager.Instance.Pixel, new Rectangle(DisplayRectangle.X, DisplayRectangle.Y + DisplayRectangle.Height - BorderThickness, DisplayRectangle.Width, BorderThickness), BorderColor);
            GraphicsManager.Instance.Sprite.Draw(GraphicsManager.Instance.Pixel, new Rectangle(DisplayRectangle.X, DisplayRectangle.Y, BorderThickness, DisplayRectangle.Height), BorderColor);
            GraphicsManager.Instance.Sprite.Draw(GraphicsManager.Instance.Pixel, new Rectangle(DisplayRectangle.X + DisplayRectangle.Width - BorderThickness, DisplayRectangle.Y, BorderThickness, DisplayRectangle.Height), BorderColor);
        }

        protected virtual void OnScreenSizeChanged()
        {
            SizeChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
