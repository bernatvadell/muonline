using Client.Main.Objects.Particles.Effects;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
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

        public Particle Emit()
        {
            var particle = new Particle(ParticleType)
            {
                Position = RandomPosition(),
                Scale = RandomScale(),
                Angle = RandomAngle(),
                Effects = [.. Effects.Select(x => x.Copy())]
            };

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
    }
}
