using Client.Main.Controls;
using Client.Main.Controls.UI;
using Client.Main.Controls.UI.Login;
using Client.Main.Models;
using Client.Main.Objects.Effects;
using Client.Main.Objects.Player;
using Client.Main.Objects.Worlds.Lorencia;
using Client.Main.Worlds;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Scenes
{
    public class TestScene : BaseScene
    {
        private LoginDialog _loginDialog;

        public TestScene()
        {
            Controls.Add(_loginDialog = new LoginDialog()
            {
                Visible = false,
                Align = ControlAlign.HorizontalCenter | ControlAlign.VerticalCenter
            });
        }

        public override async Task Load()
        {
            await ChangeWorldAsync<EmptyWorldControl>();
            await base.Load();

            _loginDialog.Visible = true;
        }
    }
}
