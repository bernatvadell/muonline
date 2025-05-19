using Client.Main.Controllers;
using Client.Main.Models;
using Client.Main.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
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
        private bool _isCurrentlyPressedByMouse = false;

        // Properties
        public GraphicsDevice GraphicsDevice => MuGame.Instance.GraphicsDevice;
        public GameControl Root => Parent?.Root ?? this;
        public GameControl Parent { get; set; }
        public BaseScene Scene => this is BaseScene scene ? scene : Parent?.Scene;
        public virtual WorldControl World => Scene?.World;

        public ChildrenCollection<GameControl> Controls { get; private set; }
        public GameControlStatus Status { get; protected set; } = GameControlStatus.NonInitialized;
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
        public event EventHandler VisibilityChanged; // Optional event for visibility changes

        // Constructors
        protected GameControl()
        {
            Controls = new ChildrenCollection<GameControl>(this);
        }

        // Public Methods
        public virtual bool OnClick()
        {
            // Focus this control when clicked, if it's interactive.
            // Scene.FocusControl is now set in BaseScene's Update.
            // Here, we just invoke the event.
            Click?.Invoke(this, EventArgs.Empty);
            return Click != null; // consumes if there's a subscriber
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

            // moved: Scene.MouseControl = this; 
            // MouseControl is now determined by BaseScene to ensure topmost logic.

            if (Interactive && Visible) // process clicks only if interactive and visible
            {
                if (IsMouseOver) // mouse is currently over this control
                {
                    if (mouse.LeftButton == ButtonState.Pressed)
                    {
                        IsMousePressed = true; // for UI styling, indicate it's being pressed
                        if (MuGame.Instance.PrevMouseState.LeftButton == ButtonState.Released)
                        {
                            _isCurrentlyPressedByMouse = true; // this control initiated the press sequence
                            Scene?.FocusControlIfInteractive(this); // attempt to set focus
                        }
                    }
                    else if (mouse.LeftButton == ButtonState.Released)
                    {
                        // mouse is released WHILE OVER this control
                        if (_isCurrentlyPressedByMouse && MuGame.Instance.PrevMouseState.LeftButton == ButtonState.Pressed)
                        {
                            // click occurred (press and release over this control)
                            if (OnClick()) // call OnClick and check if it was handled
                            {
                                if (Scene is BaseScene baseScene && this != baseScene.World)
                                {
                                    baseScene.SetMouseInputConsumed();
                                }
                            }
                        }
                        _isCurrentlyPressedByMouse = false; // reset press initiator
                        IsMousePressed = false; // reset styling state
                    }
                }
                else // mouse is not over this control
                {
                    IsMousePressed = false; // not pressed for styling if mouse isn't over
                    // if mouse was pressed on this control but then dragged off and released elsewhere,
                    // reset if mouse is released not over this control.
                    if (mouse.LeftButton == ButtonState.Released)
                    {
                        _isCurrentlyPressedByMouse = false;
                    }
                }
            }
            else // not interactive or not visible
            {
                IsMousePressed = false;
                _isCurrentlyPressedByMouse = false;
            }

            // Iterate over a copy for updating children to prevent collection modification issues
            var childrenToUpdate = Controls.ToArray();
            foreach (var control in childrenToUpdate)
            {
                // If a control was disposed by a previous sibling's update in the same frame,
                // it might still be in `childrenToUpdate` but its Status would be Disposed.
                if (control.Status != GameControlStatus.Disposed)
                {
                    control.Update(gameTime);
                }
            }

            if (AutoViewSize)
            {
                int maxWidth = 0, maxHeight = 0;
                // For AutoViewSize, iterate over the current Controls collection (or a fresh snapshot)
                // as children's Update methods might have added/removed other controls.
                var currentChildrenForLayout = Controls.ToArray();
                foreach (var control in currentChildrenForLayout)
                {
                    if (control.Status == GameControlStatus.Disposed) continue; // Skip disposed controls for layout

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
                ViewSize = new Point(Math.Max(ControlSize.X, maxWidth), Math.Max(ControlSize.Y, maxHeight));
            }

            if (Align != ControlAlign.None)
                AlignControl();
        }

        public virtual bool ProcessMouseScroll(int scrollDelta)
        {
            // Base implementation: does not handle scroll, bubbles to children if needed.
            // Most specific controls (like scrollbars) will override this.
            // For container controls, they might iterate their children.
            return false;
        }

        public virtual void Draw(GameTime gameTime)
        {
            if (Status != GameControlStatus.Ready || !Visible)
                return;

            DrawBackground();
            DrawBorder();

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
            var controlsArray = Controls.ToArray(); // Array to avoid modification issues during iteration

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