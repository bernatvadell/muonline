using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Client.Main.Objects.Particles
{
    public class ParticleSystem : WorldObject
    {
        private readonly List<ParticleSystemRegister> _particles = [];

        private int _maxParticles = 100;
        private float _regenerationTimer;
        private float _regenerationMin = 0.5f;
        private float _regenerationMax = 2f;

        public ParticleSystem()
        {
        }

        private float RandomRegenerationTime()
        {
            return (float)(MuGame.Random.NextDouble() * (_regenerationMax - _regenerationMin) + _regenerationMin);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (!Visible) return;

            _regenerationTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (_regenerationTimer <= 0 && Children.Count < _maxParticles)
            {
                EmitParticle();
                _regenerationTimer = RandomRegenerationTime();
            }
        }

        private void EmitParticle()
        {
            if (_particles.Count > 0 && Children.Count < _maxParticles)
            {
                int randomIndex = MuGame.Random.Next(_particles.Count);
                var particleType = _particles[randomIndex];
                var particle = particleType.Emit();
                Children.Add(particle);

                if (particle.Status == Models.GameControlStatus.NonInitialized)
                {
                    Task.Run(() => particle.Load());
                }
            }
        }

        public ParticleSystemRegister Register<T>() where T : WorldObject
        {
            var register = new ParticleSystemRegister(this) { ParticleType = typeof(T) };
            _particles.Add(register);
            return register;
        }

        public ParticleSystem SetMaxParticles(int max)
        {
            _maxParticles = max;
            return this;
        }

        public ParticleSystem SetRegeneration(float min, float max)
        {
            _regenerationMin = min;
            _regenerationMax = max;
            return this;
        }

        public static ParticleSystem Create()
        {
            return new ParticleSystem();
        }

        internal void RecycleParticle(Particle particle)
        {
            if (particle == null)
                return;

            // Detach without disposing so we can reuse the instance.
            bool detached = Children.Detach(particle);
            if (!detached)
            {
                return;
            }

            particle.OwnerRegister?.ReturnToPool(particle);
        }
    }
}
