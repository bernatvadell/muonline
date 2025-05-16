using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Net.NetworkInformation;
using Client.Main.Models;

namespace Client.Main.Controls.UI.Login
{
    public class ServerButton : SpriteControl
    {
        private readonly LabelControl _label;
        private SpriteControl _status;

        public byte Gauge { get; set; } = 0;
        public bool Available => Gauge < 100f;

        public byte Index { get; set; }
        public new string Name { get => _label.Text; set => _label.Text = value; }

        public ServerButton()
        {
            AutoViewSize = false;
            ViewSize = new Point(192, 26);

            Interactive = true;
            TileWidth = 192;
            TileHeight = 26;
            TileY = 0;
            BlendState = Blendings.Alpha;
            TexturePath = "Interface/server_b2_all.tga";

            Controls.Add(_label = new LabelControl
            {
                Align = ControlAlign.HorizontalCenter,
                Y = 4
            });

            Controls.Add(_status = new SpriteControl
            {
                TexturePath = "Interface/server_b2_loding.jpg",
                X = 9,
                Y = 19,
                TileWidth = 18,
                TileHeight = 4,
            });
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            Interactive = Available;
            _status.TileWidth = 167 * Gauge;

            if (!Available)
                TileY = 2;
            else if (IsMouseOver && IsMousePressed) // check IsMousePressed from GameControl
                TileY = 1; // typically, a "pressed" state might be different, but C++ logic was Scene.MouseControl == this
            else if (IsMouseOver) // hover state
                TileY = 1;
            else
                TileY = 0;
        }
    }
}
