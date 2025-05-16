using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Controls.UI;
using Client.Main.Helpers;
using Client.Main.Models;
using Client.Main.Objects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        public bool IsMouseInputConsumedThisFrame { get; private set; }
        public bool IsKeyboardEnterConsumedThisFrame { get; private set; }

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

        public async Task ChangeWorldAsync<T>(Action<string, float> progressCallback = null) where T : WorldControl, new()
        {
            progressCallback?.Invoke("Disposing previous world...", 0.0f * (progressCallback == null ? 1.0f : 1.0f)); // Progress for this step
            World?.Dispose();

            progressCallback?.Invoke($"Creating World {typeof(T).Name}...", 0.1f * (progressCallback == null ? 1.0f : 1.0f));
            var newWorld = new T();
            Controls.Add(newWorld);

            progressCallback?.Invoke($"Initializing World {typeof(T).Name}...", 0.2f * (progressCallback == null ? 1.0f : 1.0f));
            // If WorldControl needs fine-grained progress, it would need InitializeWithProgressReporting too.
            // For now, its Initialize() is treated as one block.
            await newWorld.Initialize();

            World = newWorld;
            progressCallback?.Invoke($"World {typeof(T).Name} Ready.", 1.0f * (progressCallback == null ? 1.0f : 1.0f));
        }

        public virtual async Task InitializeWithProgressReporting(Action<string, float> progressCallback)
        {
            if (Status != GameControlStatus.NonInitialized)
                return;

            void Report(string message, float progress) => progressCallback?.Invoke(message, progress);

            try
            {
                Status = GameControlStatus.Initializing;
                Report($"Initializing {GetType().Name}...", 0.05f);

                var controlsToInitialize = Controls.Where(c => c.Status == GameControlStatus.NonInitialized).ToArray();
                if (controlsToInitialize.Any())
                {
                    // This part is usually very fast, so one report is fine.
                    // If individual controls become heavy, this might need more detail.
                    Report($"Initializing UI Controls for {GetType().Name}...", 0.10f);
                    var controlTasks = new List<Task>(controlsToInitialize.Length);
                    foreach (var control in controlsToInitialize)
                    {
                        controlTasks.Add(control.Initialize());
                    }
                    await Task.WhenAll(controlTasks);
                    Report($"UI Controls Initialized for {GetType().Name}.", 0.15f);
                }
                else
                {
                    Report($"No new UI Controls to initialize for {GetType().Name}.", 0.15f);
                }

                await LoadSceneContentWithProgress(progressCallback);

                Report($"Finalizing {GetType().Name}...", 0.95f);
                AfterLoad();

                Status = GameControlStatus.Ready;
                Report($"{GetType().Name} Ready.", 1.0f);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Error during InitializeWithProgressReporting for {GetType().Name}: {e.Message}{Environment.NewLine}{e.StackTrace}");
                Status = GameControlStatus.Error;
                Report($"Error initializing {GetType().Name}: {e.Message}", 1.0f);
                throw;
            }
        }

        protected virtual async Task LoadSceneContentWithProgress(Action<string, float> progressCallback)
        {
            // Base implementation calls the old Load method and reports basic progress
            progressCallback?.Invoke($"Loading content for {GetType().Name}...", 0.2f); // Generic start
            await Load(); // Calls existing Load method of the derived scene
            progressCallback?.Invoke($"Content loaded for {GetType().Name}.", 0.9f); // Generic end
        }

        public override void Update(GameTime gameTime)
        {
            var currentFocusControl = FocusControl;
            var currentMouseControl = MouseControl;

            MouseControl = null;
            MouseHoverObject = null;
            IsMouseInputConsumedThisFrame = false;
            IsKeyboardEnterConsumedThisFrame = false;

            // Determine MouseHoverControl and MouseControl for the scene
            // Iterate UI controls (children of BaseScene) in reverse order (topmost first)
            GameControl topmostHoverForTooltip = null;
            GameControl topmostInteractiveForScroll = null;

            // set scene's MouseControl (which is the target for scroll)
            MouseControl = null; 

            for (int i = Controls.Count - 1; i >= 0; i--)
            {
                var uiCtrl = Controls[i];
                if (uiCtrl.Visible && uiCtrl.IsMouseOver) // IsMouseOver is updated in control's own Update
                {
                    if (topmostHoverForTooltip == null)
                    {
                        topmostHoverForTooltip = uiCtrl;
                    }
                    if (topmostInteractiveForScroll == null && uiCtrl.Interactive)
                    {
                        topmostInteractiveForScroll = uiCtrl; 
                        // for scroll, we take the first topmost interactive one.
                        // clicks are handled by controls themselves now.
                    }
                }
            }
            MouseHoverControl = topmostHoverForTooltip; // this is for general hover (tooltips, visual effects)
            MouseControl = topmostInteractiveForScroll; // this is specifically for scroll dispatch


            // no UI control captured the mouse for scroll interaction, check the World itself
            if (MouseControl == null && World != null && World.Visible && World.Interactive && World.IsMouseOver)
            {
                MouseControl = World; // world can handle scroll (camera zoom)
                if (MouseHoverControl == null)  // no UI element was hovered for tooltips
                {
                    MouseHoverControl = World;
                }
            }

            base.Update(gameTime);

            if (Status != GameControlStatus.Ready)
                return;

            if (World == null) return;

            // focus management (driven by GameControl.OnClick via FocusControlIfInteractive)
            if (FocusControl != currentFocusControl)
            {
                currentFocusControl?.OnBlur();
                FocusControl?.OnFocus();
            }

            // scroll handling - using the MouseControl determined above
            if (MouseControl != null && MouseControl.Interactive) // MouseControl here is the target for scroll
            {
                int scrollWheelChange = MuGame.Instance.Mouse.ScrollWheelValue - MuGame.Instance.PrevMouseState.ScrollWheelValue;
                if (scrollWheelChange != 0)
                {
                    int normalizedScrollDelta = scrollWheelChange; // positive for up, negative for down
                    if (MouseControl.ProcessMouseScroll(normalizedScrollDelta)) // UI handled it
                    {
                        if (MouseControl != World) // world can scroll (zoom) without "consuming" in this sense for other UI
                        {
                            IsMouseInputConsumedThisFrame = true;
                        }
                    }
                }

                if (MuGame.Instance.Mouse.LeftButton == ButtonState.Pressed && !MouseControl.IsMousePressed)
                {
                    MouseControl.IsMousePressed = true;
                }
                else if (MouseControl == currentMouseControl && MuGame.Instance.Mouse.LeftButton == ButtonState.Released && MouseControl.IsMousePressed)
                {
                    MouseControl.IsMousePressed = false;
                    MouseControl.OnClick();
                    if (MouseControl != World) // a UI element (not the world) handled the click
                    {
                        IsMouseInputConsumedThisFrame = true;
                    }
                    MouseHoverControl = MouseControl;
                }
                else if (currentMouseControl != null && currentMouseControl.IsMousePressed && MouseControl != currentMouseControl)
                {
                    currentMouseControl.IsMousePressed = false;
                }
            }

            // handle 3D world object clicks if UI didn't consume input
            if (!IsMouseInputConsumedThisFrame && MouseHoverObject != null &&
                MuGame.Instance.PrevMouseState.LeftButton == ButtonState.Pressed &&
                MuGame.Instance.Mouse.LeftButton == ButtonState.Released)
            {
                MouseHoverObject.OnClick();
            }

            DebugPanel.BringToFront();
            Cursor.BringToFront();
        }

        public void FocusControlIfInteractive(GameControl control)
        {
            if (control != null && control.Interactive && FocusControl != control)
            {
                FocusControl = control;
                // Focus/OnBlur will be called in the main update loop check
            }
        }

        public void SetMouseInputConsumed()
        {
            IsMouseInputConsumedThisFrame = true;
        }

        public void ConsumeKeyboardEnter()
        {
            IsKeyboardEnterConsumedThisFrame = true;
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
