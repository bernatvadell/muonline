using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Controls.UI.Game
{
    // Class storing layout data (MainLayout.json)
    public class LayoutInfo
    {
        public string Name { get; set; }
        public float ScreenX { get; set; }
        public float ScreenY { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Z { get; set; } // Rendering order
    }

    // Class storing texture rectangle data (MainRect.json)
    public class TextureRectData
    {
        public string Name { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    // Extends UIControl with RenderOrder – facilitates sorting during rendering
    public abstract class ExtendedUIControl : UIControl
    {
        public int RenderOrder { get; set; }
    }

    /// <summary>
    /// Base class for dynamic control layout.
    /// Responsible for loading data from JSON files, scaling, and setting custom alpha values.
    /// </summary>
    public abstract class DynamicLayoutControl : ExtendedUIControl
    {
        // Base resolution of the project
        protected int DesignWidth { get; set; } = 1280;
        protected int DesignHeight { get; set; } = 720;

        // Scaling multiplier – can be changed to increase or decrease control sizes.
        public float CustomScale { get; set; } = 1.0f;

        protected List<LayoutInfo> LayoutInfos { get; private set; }
        protected List<TextureRectData> TextureRectDatas { get; private set; }

        // Factory dictionary – enables creation of custom controls (e.g., MP/HP)
        protected Dictionary<string, Func<LayoutInfo, UIControl>> ControlFactories { get; private set; } = new Dictionary<string, Func<LayoutInfo, UIControl>>();

        // Dictionary specifying a custom alpha value for selected elements
        protected Dictionary<string, float> AlphaOverrides { get; private set; } = new Dictionary<string, float>();

        // Abstract properties that must be overridden by the inheriting class to provide the appropriate resources.
        protected abstract string LayoutJsonResource { get; }
        protected abstract string TextureRectJsonResource { get; }

        // Default texture path – can be overridden if the resources are different.
        protected virtual string DefaultTexturePath => "Interface/GFx/main_IE.ozd";

        public DynamicLayoutControl()
        {
            LoadLayoutData();
            CreateControls();
            UpdateLayout();
        }

        protected virtual void LoadLayoutData()
        {
            LayoutInfos = LoadEmbeddedJson<List<LayoutInfo>>(LayoutJsonResource);
            TextureRectDatas = LoadEmbeddedJson<List<TextureRectData>>(TextureRectJsonResource);

            // Sort by Z (from lowest to highest)
            LayoutInfos = LayoutInfos.OrderBy(info => info.Z).ToList();
        }

        protected virtual T LoadEmbeddedJson<T>(string resourceName)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new FileNotFoundException($"Resource not found: {resourceName}. Available: {string.Join(", ", assembly.GetManifestResourceNames())}");
                using (StreamReader reader = new StreamReader(stream))
                {
                    string json = reader.ReadToEnd();
                    return JsonSerializer.Deserialize<T>(json);
                }
            }
        }

        // Creates controls based on layout data.
        protected virtual void CreateControls()
        {
            foreach (var info in LayoutInfos)
            {
                UIControl ctrl;
                if (ControlFactories.TryGetValue(info.Name, out var factory))
                {
                    // Custom control (e.g., MainMPControl, MainHPControl)
                    ctrl = factory(info);
                }
                else
                {
                    // Default control – TextureControl
                    var textureCtrl = new TextureControl
                    {
                        AutoViewSize = false,
                        TexturePath = DefaultTexturePath,
                        // ⬇  OZD ≠ premultiply → NonPremultiplied
                        BlendState = BlendState.NonPremultiplied,
                        Name = info.Name
                    };

                    // Sets the texture rectangle if data is available
                    var texRect = TextureRectDatas.FirstOrDefault(t => t.Name == info.Name);
                    textureCtrl.TextureRectangle = texRect != null
                        ? new Rectangle(texRect.X, texRect.Y, texRect.Width, texRect.Height)
                        : new Rectangle(0, 0, info.Width, info.Height);

                    ctrl = textureCtrl;
                }

                // If the control inherits from ExtendedUIControl, its RenderOrder is set based on the Z field.
                if (ctrl is ExtendedUIControl ex) ex.RenderOrder = info.Z;

                // Setting alpha if a value is assigned in AlphaOverrides
                if (AlphaOverrides.TryGetValue(info.Name, out var a) && ctrl is TextureControl tc)
                    tc.Alpha = a;

                // Stores layout data in Tag (optional)
                ctrl.Tag = info;
                Controls.Add(ctrl);
            }
        }

        // Scales controls according to the current resolution – also takes CustomScale into account
        public virtual void UpdateLayout()
        {
            int currentWidth = MuGame.Instance.GraphicsDevice.Viewport.Width;
            int currentHeight = MuGame.Instance.GraphicsDevice.Viewport.Height;
            float scaleX = (float)currentWidth / DesignWidth;
            float scaleY = (float)currentHeight / DesignHeight;
            float uniformScale = Math.Min(scaleX, scaleY) * CustomScale;

            foreach (var ctrl in Controls)
            {
                if (ctrl.Tag is LayoutInfo info)
                {
                    ctrl.X = (int)(info.ScreenX * uniformScale);
                    ctrl.Y = (int)(info.ScreenY * uniformScale);
                    ctrl.ViewSize = new Point((int)(info.Width * uniformScale), (int)(info.Height * uniformScale));
                    ctrl.Scale = uniformScale;
                }
            }
        }

        protected override void OnScreenSizeChanged()
        {
            base.OnScreenSizeChanged();
            UpdateLayout();
        }
    }
}