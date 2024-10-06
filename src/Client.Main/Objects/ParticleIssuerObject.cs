using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    public class ParticleIssuerObject : WorldObject
    {
        private readonly List<Type> _particleTypes = [];

        private int _maxParticles = 100;

        private float _regenerationMin = 0.5f;
        private float _regenerationMax = 2f;
        private float _durationMin = 2f;
        private float _durationMax = 2f;
        private Vector3 _gravity = new(0, 9.8f, 0);
        private float _scaleMin = 1f;
        private float _scaleMax = 1f;
        private bool _rotation = false;

        private Random _random = new();
        private float _regenerationTimer;


        public ParticleIssuerObject()
        {
            _regenerationTimer = RandomRegenerationTime();
        }

        private float RandomRegenerationTime()
        {
            return (float)(_random.NextDouble() * (_regenerationMax - _regenerationMin) + _regenerationMin);
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
            if (_particleTypes.Count > 0 && Children.Count < _maxParticles)
            {
                int randomIndex = _random.Next(_particleTypes.Count);
                var particleType = _particleTypes[randomIndex];

                // Emitir la partícula
                var particle = new ParticleObject(particleType)
                {
                    Position = this.Position,
                    Scale = RandomScale(),
                    Gravity = _gravity,
                    Duration = RandomDuration(),
                    Rotation = _rotation
                };

                Task.Run(() => particle.Load(GraphicsDevice));
                Children.Add(particle);
            }
        }


        public ParticleIssuerObject Use<T>() where T : WorldObject
        {
            _particleTypes.Add(typeof(T));
            return this;
        }

        public ParticleIssuerObject SetMaxParticles(int max)
        {
            _maxParticles = max;
            return this;
        }

        public ParticleIssuerObject SetRegeneration(float min, float max)
        {
            _regenerationMin = min;
            _regenerationMax = max;
            return this;
        }

        public ParticleIssuerObject SetDuration(float min, float max)
        {
            _durationMin = min;
            _durationMax = max;
            return this;
        }

        public ParticleIssuerObject SetGravity(Vector3 value)
        {
            _gravity = value;
            return this;
        }

        public ParticleIssuerObject EnableRotation()
        {
            _rotation = true;
            return this;
        }

        public ParticleIssuerObject SetScale(float min, float max)
        {
            _scaleMin = min;
            _scaleMax = max;
            return this;
        }

        public static ParticleIssuerObject Create()
        {
            return new ParticleIssuerObject();
        }

        private float RandomScale()
        {
            return (float)(_random.NextDouble() * (_scaleMax - _scaleMin) + _scaleMin);
        }
        private float RandomDuration()
        {
            return (float)(_random.NextDouble() * (_durationMax - _durationMin) + _durationMin);
        }
    }
}
