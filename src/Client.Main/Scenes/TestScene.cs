using Client.Data;
using Client.Main.Controls;
using Client.Main.Objects.Effects;
using Client.Main.Objects.Login;
using Client.Main.Objects.Lorencia;
using Client.Main.Objects.Particles;
using Client.Main.Objects.Particles.Effects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Scenes
{
    public class TestScene : WorldControl
    {
        private ParticleSystem Particles;

        public TestScene() : base(-1)
        {
            Camera.Instance.Position = new Vector3(10, 10, 10);
            Camera.Instance.Target = new Vector3(1, 1, 1);
        }

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            await base.Load(graphicsDevice);
            await AddObject(new StatueTorchObject() { World = this });
        }

        public override void Update(GameTime time)
        {
            Camera.Instance.Position = new Vector3(300, 300, 300);
            Camera.Instance.Target = new Vector3(150, 150, 150);

            base.Update(time);
        }
    }
}
