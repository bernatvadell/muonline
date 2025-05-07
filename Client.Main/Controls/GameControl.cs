﻿using Client.Main.Controllers;
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
        // Fields
        private Point _controlSize, _viewSize;

        // Properties
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

        public bool IsMouseOver { get; set; }
        public bool IsMousePressed { get; set; }
        public bool HasFocus => Scene?.FocusControl == this;

        // Events
        public event EventHandler Click;
        public event EventHandler SizeChanged;
        public event EventHandler Focus;
        public event EventHandler Blur;

        // Constructors
        protected GameControl()
        {
            Controls = new ChildrenCollection<GameControl>(this);
        }

        // Public Methods
        public virtual void OnClick()
        {
            if (MuGame.Instance.ActiveScene != null)
            {
                MuGame.Instance.ActiveScene.FocusControl = this;
            }
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

                var controlsArray = Controls.ToArray(); // ToArray to avoid modification issues during iteration
                var taskList = new List<Task>(controlsArray.Length);

                for (int i = 0; i < controlsArray.Length; i++)
                {
                    var control = controlsArray[i];
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
            {
                // Fire and forget initialization
                _ = Initialize();
            }

            if (Status != GameControlStatus.Ready || !Visible) return;

            // Cache mouse and display rectangle to avoid repeated property lookups
            var mouse = MuGame.Instance.Mouse;
            Rectangle rect = DisplayRectangle;
            IsMouseOver = Interactive &&
                          mouse.Position.X >= rect.X && mouse.Position.X <= rect.X + rect.Width &&
                          mouse.Position.Y >= rect.Y && mouse.Position.Y <= rect.Y + rect.Height;

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
                if (!Align.HasFlag(ControlAlign.Bottom)) // Should be Top for this logic
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
            // Logic can be added here if needed when visibility changes
            // e.g., canceling animations, stopping sounds, etc.
            if (Visible != isVisible)
            {
                Visible = isVisible;
                // OnVisibilityChanged?.Invoke(this, EventArgs.Empty); // Optional event
            }
        }

        public virtual void Draw(GameTime gameTime)
        {
            if (Status != GameControlStatus.Ready || !Visible)
                return;

            var sb = GraphicsManager.Instance.Sprite;
            bool beginCalled = false;
            try
            {
                // Check if SpriteBatch is already in a Begin/End block from a parent
                // This is a simplified check; a more robust solution might involve a stack or state tracking.
                // For now, assume if it's not null and not in a Begin state, we can call Begin.
                // This part is tricky without knowing the exact state management of SpriteBatch elsewhere.
                // A common pattern is to pass the SpriteBatch down or ensure Begin/End is called at the highest level.
                // For simplicity, let's assume each control manages its own Begin/End for background/border.
                // Children will then call their own Begin/End.
                // This might lead to multiple Begin/End calls, which is not ideal for performance.
                // A better approach is often to have a single Begin/End at the scene or root UI level.
                // However, sticking to the original structure for now:
                sb.Begin();
                beginCalled = true;
                DrawBackground();
                DrawBorder();
            }
            finally
            {
                if (beginCalled && sb.GraphicsDevice != null) // Only End if Begin was called by this control
                    sb.End();
            }

            GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.BlendState = BlendState.Opaque; // Usually AlphaBlend for UI

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
            var controlsArray = Controls.ToArray(); // ToArray to avoid modification issues during iteration

            for (int i = 0; i < controlsArray.Length; i++)
            {
                controlsArray[i].Dispose();
            }

            Controls.Clear();

            Parent?.Controls.Remove(this);

            Status = GameControlStatus.Disposed;
            // Make sure to unhook any event handlers to prevent memory leaks
            Click = null;
            SizeChanged = null;
            Focus = null;
            Blur = null;
        }

        public void BringToFront()
        {
            if (Status == GameControlStatus.Disposed) return;
            if (Parent == null) return;
            if (Parent.Controls.Count == 0 || Parent.Controls[^1] == this) return; // Check count before accessing ^1
            var currentParent = Parent; // Store parent in case 'this' is removed and Parent becomes null
            currentParent.Controls.Remove(this);
            currentParent.Controls.Add(this);
        }

        public void SendToBack()
        {
            if (Status == GameControlStatus.Disposed) return;
            if (Parent == null) return;
            if (Parent.Controls.Count == 0 || Parent.Controls[0] == this) return; // Check count before accessing [0]
            var currentParent = Parent;
            currentParent.Controls.Remove(this);
            currentParent.Controls.Insert(0, this);
        }

        // Protected Methods
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
                Y = Parent.DisplaySize.Y - DisplaySize.Y;
            else if (Align.HasFlag(ControlAlign.VerticalCenter))
                Y = (Parent.DisplaySize.Y / 2) - (DisplaySize.Y / 2);

            if (Align.HasFlag(ControlAlign.Left))
                X = 0;
            else if (Align.HasFlag(ControlAlign.Right))
                X = Parent.DisplaySize.X - DisplaySize.X;
            else if (Align.HasFlag(ControlAlign.HorizontalCenter))
                X = (Parent.DisplaySize.X / 2) - (DisplaySize.X / 2);
        }

        protected void DrawBackground()
        {
            if (BackgroundColor.A == 0) // Check alpha for transparency
                return;

            GraphicsManager.Instance.Sprite.Draw(
                GraphicsManager.Instance.Pixel,
                DisplayRectangle,
                BackgroundColor * Alpha // Apply control's alpha
            );
        }

        protected void DrawBorder()
        {
            if (BorderThickness <= 0 || BorderColor.A == 0) // Check alpha for transparency
                return;

            Color finalBorderColor = BorderColor * Alpha; // Apply control's alpha

            // Top border
            GraphicsManager.Instance.Sprite.Draw(GraphicsManager.Instance.Pixel, new Rectangle(DisplayRectangle.X, DisplayRectangle.Y, DisplayRectangle.Width, BorderThickness), finalBorderColor);
            // Bottom border
            GraphicsManager.Instance.Sprite.Draw(GraphicsManager.Instance.Pixel, new Rectangle(DisplayRectangle.X, DisplayRectangle.Y + DisplayRectangle.Height - BorderThickness, DisplayRectangle.Width, BorderThickness), finalBorderColor);
            // Left border
            GraphicsManager.Instance.Sprite.Draw(GraphicsManager.Instance.Pixel, new Rectangle(DisplayRectangle.X, DisplayRectangle.Y, BorderThickness, DisplayRectangle.Height), finalBorderColor);
            // Right border
            GraphicsManager.Instance.Sprite.Draw(GraphicsManager.Instance.Pixel, new Rectangle(DisplayRectangle.X + DisplayRectangle.Width - BorderThickness, DisplayRectangle.Y, BorderThickness, DisplayRectangle.Height), finalBorderColor);
        }

        protected virtual void OnScreenSizeChanged()
        {
            SizeChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}