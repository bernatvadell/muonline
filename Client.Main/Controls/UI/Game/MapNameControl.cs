using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Client.Main.Controllers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Controls.UI.Game
{
    public class MapNameControl : TextureControl
    {
        private float _displayTimer = 0f;
        private LabelControl _label;

        public MapNameControl()
        {
            var layoutInfo = LoadLayoutInfo();
            var texRectData = LoadTextureRectData();

            if (layoutInfo != null)
            {
                X = (int)layoutInfo.ScreenX;
                Y = (int)layoutInfo.ScreenY;
                ViewSize = new Point(layoutInfo.Width, layoutInfo.Height);
            }

            if (texRectData != null)
            {
                TextureRectangle = new Rectangle(texRectData.X, texRectData.Y, texRectData.Width, texRectData.Height);
            }

            TexturePath = "Interface/GFx/MapName_I2.ozd";
            AutoViewSize = false;
            Visible = true;
            Alpha = 1f;

            _label = new LabelControl
            {
                Text = "Map Name",
                FontSize = 22,
                TextColor = Color.WhiteSmoke,
                UseManualPosition = true
            };

            UpdateLabelPosition();
        }

        public string LabelText
        {
            get => _label.Text;
            set
            {
                _label.Text = value;
                UpdateLabelPosition();
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            _displayTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (_displayTimer <= 5f)
            {
                Alpha = 1f;
            }
            else if (_displayTimer > 5f && _displayTimer <= 7f)
            {
                float fadeProgress = (_displayTimer - 5f) / 2f;
                Alpha = MathHelper.SmoothStep(1f, 0f, fadeProgress);
            }
            else
            {
                Alpha = 0f;
            }

            if (Alpha < 0.4f)
                _label.Visible = false;
            else
            {
                _label.Visible = true;
                _label.Alpha = Alpha;
            }
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible)
                return;

            SpriteBatch sprite = GraphicsManager.Instance.Sprite;
            sprite.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
            sprite.Draw(Texture, DisplayRectangle, TextureRectangle, Color.White * Alpha);
            sprite.End();

            _label.Draw(gameTime);
        }

        private void UpdateLabelPosition()
        {
            _label.X = X + (ViewSize.X - _label.ControlSize.X) / 2;
            _label.Y = Y + (ViewSize.Y - _label.ControlSize.Y) / 2 + 14;
        }

        private LayoutInfo LoadLayoutInfo()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream("Client.Main.Controls.UI.Game.Layouts.MapNameLayout.json"))
            {
                if (stream == null)
                    return null;
                using (StreamReader reader = new StreamReader(stream))
                {
                    string json = reader.ReadToEnd();
                    var list = JsonSerializer.Deserialize<List<LayoutInfo>>(json);
                    return list.FirstOrDefault(item => item.Name == "MapName");
                }
            }
        }

        private TextureRectData LoadTextureRectData()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream("Client.Main.Controls.UI.Game.Layouts.MapNameRect.json"))
            {
                if (stream == null)
                    return null;
                using (StreamReader reader = new StreamReader(stream))
                {
                    string json = reader.ReadToEnd();
                    var list = JsonSerializer.Deserialize<List<TextureRectData>>(json);
                    return list.FirstOrDefault(item => item.Name == "MapName");
                }
            }
        }
    }
}
