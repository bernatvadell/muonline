using System;
using System.Collections.Concurrent;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Effects
{
    /// <summary>
    /// Small glowing particle that drifts away from orbiting lights and fades out.
    /// Creates a magical sparkle trail effect.
    /// </summary>
    public class ElfBuffSparkle : SpriteObject
    {
        private static readonly ConcurrentBag<ElfBuffSparkle> _pool = new();

        private Vector3 _velocity;
        private float _lifetime;
        private float _initialScale;
        private float _rotationSpeed;
        private float _age;

        public override string TexturePath => "Effect/Shiny05.jpg";

        public ElfBuffSparkle(Vector3 startPosition, float hueShift = 1f)
        {
            Reset(startPosition, hueShift);
        }

        public static ElfBuffSparkle Rent(Vector3 startPosition, float hueShift = 1f)
        {
            if (_pool.TryTake(out var sparkle))
            {
                sparkle.Reset(startPosition, hueShift);
                return sparkle;
            }

            return new ElfBuffSparkle(startPosition, hueShift);
        }

        private void Reset(Vector3 startPosition, float hueShift)
        {
            Position = startPosition;
            Angle = Vector3.Zero;

            IsTransparent = true;
            AffectedByTransparency = true;
            BlendState = BlendState.Additive;
            DepthState = DepthStencilState.DepthRead;
            LightEnabled = false;
            BoundingBoxLocal = new BoundingBox(Vector3.Zero, Vector3.Zero);
            Hidden = false;

            _age = 0f;

            _lifetime = MathHelper.Lerp(0.4f, 0.8f, (float)MuGame.Random.NextDouble());
            _initialScale = MathHelper.Lerp(0.7f, 1.3f, (float)MuGame.Random.NextDouble());
            _rotationSpeed = MathHelper.Lerp(-6f, 6f, (float)MuGame.Random.NextDouble());

            Scale = _initialScale;
            Alpha = MathHelper.Lerp(0.75f, 1f, (float)MuGame.Random.NextDouble());

            float angle = (float)(MuGame.Random.NextDouble() * MathHelper.TwoPi);
            float horizontalSpeed = MathHelper.Lerp(15f, 45f, (float)MuGame.Random.NextDouble());
            float verticalSpeed = MathHelper.Lerp(20f, 60f, (float)MuGame.Random.NextDouble());

            _velocity = new Vector3(
                MathF.Cos(angle) * horizontalSpeed,
                MathF.Sin(angle) * horizontalSpeed,
                verticalSpeed);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (Status != GameControlStatus.Ready)
                return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _age += dt;

            // Check lifetime
            if (_age >= _lifetime)
            {
                Recycle();
                return;
            }

            // Move with slight deceleration
            float t = _age / _lifetime;
            float speedMult = 1f - t * 0.6f;
            Position += _velocity * speedMult * dt;

            // Gentle gravity/float effect
            Position += new Vector3(0, 0, -15f * dt);

            // Fade out and shrink
            float fadeT = t * t; // Quadratic fade for smoother end
            Alpha = MathHelper.Lerp(0.8f, 0f, fadeT);
            Scale = _initialScale * MathHelper.Lerp(1f, 0.3f, t);

            // Rotate
            Angle = new Vector3(0, 0, _age * _rotationSpeed);
        }

        private void Recycle()
        {
            if (Parent != null)
                Parent.Children.Detach(this);
            else if (World != null)
                World.Objects.Detach(this);

            World = null;
            Hidden = true;
            _pool.Add(this);
        }
    }
}
