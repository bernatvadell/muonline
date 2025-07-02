using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Effects
{
    /// <summary>
    /// Single additive spark flying away from the center and upward.
    /// Corrected for a Z-up coordinate system.
    /// </summary>
    public class LevelUpFlare : SpriteObject
    {
        private static readonly Random _rng = new Random();

        private Vector3 _velocity;
        private float _life;
        private float _lifeTotal;
        private float _scale0;

        public override string TexturePath => "Effect/Flare.jpg";

        public LevelUpFlare(Vector3 startPos)
        {
            Position = startPos;
            IsTransparent = true;
            BlendState = BlendState.Additive;
            LightEnabled = false;

            float angle = (float)(_rng.NextDouble() * MathHelper.TwoPi);

            float horizontalSpeed = (float)(_rng.NextDouble() * 40 + 40);

            float upwardSpeed = (float)(_rng.NextDouble() * 100 + 150);

            _velocity = new Vector3(
                MathF.Cos(angle) * horizontalSpeed,
                MathF.Sin(angle) * horizontalSpeed,
                upwardSpeed
            );

            _life = _lifeTotal = (float)(_rng.NextDouble() * 1.5 + 2.5);
            _scale0 = (float)(_rng.NextDouble() * 1.2 + 1.0);
            Scale = _scale0;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            Position += _velocity * dt;

            _velocity.Z -= 100f * dt;

            _life -= dt;
            if (_life <= 0f)
            {
                World?.RemoveObject(this);
                Dispose();
                return;
            }

            float t = _life / _lifeTotal;

            if (t > 0.2f)
                Alpha = 1.0f;
            else
                Alpha = t / 0.2f;

            Scale = _scale0 * (0.8f + t * 0.2f);
        }
    }
}