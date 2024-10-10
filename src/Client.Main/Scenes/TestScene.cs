using Client.Data;
using Client.Main.Controls;
using Client.Main.Controls.UI;
using Client.Main.Objects.Effects;
using Client.Main.Objects.Login;
using Client.Main.Objects.Lorencia;
using Client.Main.Objects.Particles;
using Client.Main.Objects.Particles.Effects;
using Client.Main.Objects.Player;
using Client.Main.Worlds;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Scenes
{
    public class TestScene : BaseScene
    {
        private PlayerObject _player;
        private double nextTime = 0;
        private MoveTargetPostEffectObject _moveEffect;

        public TestScene()
        {
            Camera.Instance.Position = new Vector3(300, 300, 300);
            Camera.Instance.Target = new Vector3(150, 150, 150);
        }

        public override async Task Load()
        {
            await ChangeWorldAsync<EmptyWorldControl>();
            await World.AddObjectAsync(_player = new PlayerObject());
            await World.AddObjectAsync(_moveEffect = new MoveTargetPostEffectObject());
            await base.Load();
            //var wnd = MessageWindow.Show("Press the button to change scene");
            //wnd.Closed += (s, e) => MuGame.Instance.ChangeScene<GameScene>();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (Status != GameControlStatus.Ready || !Visible) return;

            nextTime -= gameTime.ElapsedGameTime.TotalMilliseconds;

            if (nextTime <= 0)
            {
                _player.CurrentAction++;
                nextTime = 5000;
            }

            float angle = (float)gameTime.TotalGameTime.TotalSeconds;
            Camera.Instance.Position = new Vector3(500, 500, 400);
            Camera.Instance.Target = new Vector3(0, 0, 200);
            _player.Angle = new Vector3(0, 0, 110);
        }

        public override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Brown);
            base.Draw(gameTime);
        }
    }
}
