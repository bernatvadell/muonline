using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Controls.UI
{
    public enum Align
    {
        Left,
        Center,
        Right
    }

    public class LabelControl : GameControl
    {
        private string _text;
        private Vector2 _textSize;
        public string Text { get => _text; set { if (_text != value) { _text = value; OnChangeText(); } } }

        public Align Align { get; set; }

        public override void Draw(GameTime gameTime)
        {
            var screenX = ScreenX;
            var screenY = ScreenY;

            // Adjust the position based on the alignment
            switch (Align)
            {
                case Align.Left:
                    // No adjustment needed for left alignment, it's the default
                    break;
                case Align.Center:
                    screenX = (int)(ScreenX + (Width / 2) - (_textSize.X / 2));
                    break;
                case Align.Right:
                    screenX = (int)(ScreenX + Width - _textSize.X);
                    break;
            }

            MuGame.Instance.SpriteBatch.Begin();
            MuGame.Instance.SpriteBatch.DrawString(MuGame.Instance.Font, Text, new Vector2(screenX, screenY), Color.White);
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
                _textSize = Vector2.Zero;
                return;
            }

            _textSize = MuGame.Instance.Font.MeasureString(Text);
        }
    }
}
