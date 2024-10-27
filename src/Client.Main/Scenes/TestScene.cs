using Client.Main.Controls;
using Client.Main.Controls.UI;
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
        private TestDialog _testDialog;

        public TestScene()
        {
            //Camera.Instance.Position = new Vector3(300, 300, 300);
            //Camera.Instance.Target = new Vector3(150, 150, 150);
            ChangeWorld<NewLoginWorld>();
        }

        public override async Task Load()
        {
            await base.Load();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (Status != GameControlStatus.Ready || !Visible) return;
        }

    }
}
