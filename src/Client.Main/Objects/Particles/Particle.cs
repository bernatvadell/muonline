using Client.Main.Objects.Particles.Effects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Client.Main.Objects.Particles
{
    public class Particle : WorldObject
    {
        public WorldObject Object { get; private set; }
        public BaseEffect[] Effects { get; set; } = [];
        public Type ParticleType { get; }

        public Particle(Type particleType)
        {
            ParticleType = particleType;
            Object = Activator.CreateInstance(ParticleType) as WorldObject;
            Children.Add(Object);
        }

        public override async Task Load()
        {
            await base.Load();

            for (var i = 0; i < Effects.Length; i++)
            {
                Effects[i].Particle = this;
                Effects[i].Init();
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (Status != Models.GameControlStatus.Ready) return;

            for (var i = 0; i < Effects.Length; i++)
                Effects[i].Update(gameTime);
        }
    }
}
