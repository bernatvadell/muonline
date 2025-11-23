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
        public ParticleSystemRegister OwnerRegister { get; set; }

        public Particle(Type particleType)
        {
            ParticleType = particleType;
            Object = Activator.CreateInstance(ParticleType) as WorldObject;
            Children.Add(Object);
        }

        public void ConfigureForReuse(Vector3 position, Vector3 angle, float scale, BaseEffect[] effects, bool initializeEffects)
        {
            Position = position;
            Angle = angle;
            Scale = scale;
            Alpha = 1f;
            Hidden = false;
            Effects = effects ?? Array.Empty<BaseEffect>();

            if (Effects.Length > 0)
            {
                for (var i = 0; i < Effects.Length; i++)
                {
                    Effects[i].Particle = this;
                    if (initializeEffects)
                    {
                        Effects[i].Init();
                    }
                }
            }
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
