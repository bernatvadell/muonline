using Microsoft.Xna.Framework;
using System;

namespace Client.Main.Objects
{
    public class ParticleObject : WorldObject
    {
        private WorldObject _particle;

        public float Duration { get; set; }
        public Vector3 Gravity { get; set; }
        public Vector3 Velocity { get; set; }
        public bool Rotation { get; set; }

        public ParticleObject(Type particleType)
        {
            _particle = (WorldObject)Activator.CreateInstance(particleType);
            Children.Add(_particle);

            Velocity = Vector3.Zero;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            Duration -= (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (Duration <= 0)
            {
                Dispose();
                return;
            }

            float elapsedSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
            Velocity += Gravity * elapsedSeconds;
            Position += Velocity * elapsedSeconds;

            Angle = new Vector3(
                Angle.X + Velocity.Z * elapsedSeconds,
                Angle.Y + Velocity.Y * elapsedSeconds,
                Angle.Z + Velocity.X * elapsedSeconds
            );
        }
    }
}
