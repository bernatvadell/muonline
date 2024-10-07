using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Controls.UI
{
    public class ServerGroupButton : SpriteControl
    {
        private readonly LabelControl _label;

        public byte Index { get; set; }
        public string Name { get => _label.Text; set => _label.Text = value; }
        public bool Selected { get; set; }

        public ServerGroupButton()
        {
            Controls.Add(_label = new LabelControl
            {
                Align = ControlAlign.HorizontalCenter,
                Y = 6
            });

            Interactive = true;
            TileWidth = 110;
            TileHeight = 26;
            TileY = 0;
            BlendState = BlendState.AlphaBlend;
            TexturePath = "Interface/cha_bt.tga";
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (Selected)
                TileY = 3;
            else if (MuGame.Instance.Mouse.LeftButton == ButtonState.Pressed && MuGame.Instance.ActiveScene.MouseControl == this)
                TileY = 2;
            else if (MuGame.Instance.ActiveScene.MouseControl == this)
                TileY = 1;
            else
                TileY = 0;
        }
    }
}
