using Client.Data;
using Client.Main.Controls;
using Client.Main.Objects.Effects;
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

            Particles = ParticleSystem.Create()
               .SetMaxParticles(30)
               .SetRegeneration(0.01f, 0.05f)
               .Register<FireHik01Effect>()
                   .UseEffect(GravityEffect.Create(new Vector3(0, 0, 0.64f), new Vector3(0, 0, 0.88f), 100f))
                   .UseEffect(DurationEffect.Create(27, 32))
                   .UseEffect(BrightEffect.Create())
                   .SetScale(0.72f, 1.44f)
                   .EnableRotation()
               .System.Register<FireHik02Effect>()
                   .UseEffect(GravityEffect.Create(new Vector3(0, 0, 0.64f), new Vector3(0, 0, 0.88f), 100f))
                   .UseEffect(DurationEffect.Create(17, 12))
                   .UseEffect(BrightEffect.Create())
                   .SetScale(0.72f, 1.44f)
                   .EnableRotation()
               .System.Register<FireHik03Effect>()
                   .UseEffect(DurationEffect.Create(17, 22))
                   .UseEffect(GravityEffect.Create(new Vector3(0, 0, 0.64f), new Vector3(0, 0, 0.88f), 100f))
                   .UseEffect(BrightEffect.Create())
                   .SetScale(0.72f, 1.44f)
                   .EnableRotation()
               .System;

            Particles.Position = new Vector3(0, 0, 0);

            await AddObject(new BeerObject() { Type = (ushort)ModelType.Beer01, World = this });

            await AddObject(Particles);
        }

        public override void Update(GameTime time)
        {
            Camera.Instance.Position = new Vector3(300, 300, 300);
            Camera.Instance.Target = new Vector3(150, 150, 150);

            base.Update(time);
        }
    }
}
