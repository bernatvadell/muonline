using Client.Main.Models;
using Client.Main.Objects.Particles.Effects;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Client.Main.Objects.Particles
{
    public class ParticleSystemRegister(ParticleSystem issuer)
    {
        public ParticleSystem System { get; } = issuer;
        public Type ParticleType { get; set; }
        public Vector3 PositionMin { get; set; } = new(0, 0, 0);
        public Vector3 PositionMax { get; set; } = new(0, 0, 0);
        public float ScaleMin { get; set; } = 1f;
        public float ScaleMax { get; set; } = 1f;
        public bool Rotation { get; set; }
        public List<BaseEffect> Effects { get; set; } = [];
        private readonly Queue<Particle> _pool = new();

        public Particle Emit()
        {
            var particle = RentParticle();

            return particle;
        }

        public ParticleSystemRegister UseEffect(BaseEffect effect)
        {
            Effects.Add(effect);
            return this;
        }

        public ParticleSystemRegister SetPosition(Vector3 min, Vector3 max)
        {
            PositionMin = min;
            PositionMax = max;
            return this;
        }

        public ParticleSystemRegister SetScale(float min, float max)
        {
            ScaleMin = min;
            ScaleMax = max;
            return this;
        }

        public ParticleSystemRegister EnableRotation()
        {
            Rotation = true;
            return this;
        }

        private float RandomScale()
        {
            return (float)(MuGame.Random.NextDouble() * (ScaleMax - ScaleMin) + ScaleMin);
        }

        private Vector3 RandomPosition()
        {
            return new Vector3(
                PositionMin.X + (float)(MuGame.Random.NextDouble() * (PositionMax.X - PositionMin.X)),
                PositionMin.Y + (float)(MuGame.Random.NextDouble() * (PositionMax.Y - PositionMin.Y)),
                PositionMin.Z + (float)(MuGame.Random.NextDouble() * (PositionMax.Z - PositionMin.Z))
            );
        }

        private Vector3 RandomAngle()
        {
            if (!Rotation)
                return new Vector3(0, 0, 0);

            return new Vector3(
                (float)(MuGame.Random.NextDouble() * MathF.PI * 2),
                (float)(MuGame.Random.NextDouble() * MathF.PI * 2),
                (float)(MuGame.Random.NextDouble() * MathF.PI * 2)
            );
        }

        private Particle RentParticle()
        {
            var position = RandomPosition();
            var angle = RandomAngle();
            var scale = RandomScale();
            var effects = Effects.Select(x => x.Copy()).ToArray();

            for (int i = 0; i < effects.Length; i++)
            {
                if (effects[i] is DurationEffect duration)
                {
                    duration.OnExpired = System.RecycleParticle;
                }
            }

            Particle particle;
            if (_pool.Count > 0)
            {
                particle = _pool.Dequeue();
                particle.OwnerRegister = this;
                particle.Hidden = false;
                bool initNow = particle.Status == Models.GameControlStatus.Ready;
                particle.ConfigureForReuse(position, angle, scale, effects, initNow);
            }
            else
            {
                particle = new Particle(ParticleType)
                {
                    OwnerRegister = this
                };
                particle.ConfigureForReuse(position, angle, scale, effects, initializeEffects: false);
            }

            return particle;
        }

        internal void ReturnToPool(Particle particle)
        {
            if (particle == null)
                return;

            particle.Hidden = true;
            particle.Effects = Array.Empty<BaseEffect>();
            particle.OwnerRegister = this;
            _pool.Enqueue(particle);
        }
    }
}
