using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Controls.UI
{
    public class LabelControl : GameControl
    {
        private string _text;

        public string Text { get => _text; set { if (_text != value) { _text = value; OnChangeText(); } } }

        public LabelControl()
        {
            AutoSize = false;
        }

        public override void Draw(GameTime gameTime)
        {
            MuGame.Instance.SpriteBatch.Begin();
            MuGame.Instance.SpriteBatch.DrawString(MuGame.Instance.Font, Text, ScreenLocation.Location.ToVector2(), Color.White);
            MuGame.Instance.SpriteBatch.End();

            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;

            base.Draw(gameTime);
        }

        private void OnChangeText()
        {
            if (string.IsNullOrEmpty(Text))
            {
                Width = 0;
                Height = 0;
                return;
            }

            var textSize = MuGame.Instance.Font.MeasureString(Text);
            Width = (int)textSize.X;
            Height = (int)textSize.Y;
        }
    }
}
