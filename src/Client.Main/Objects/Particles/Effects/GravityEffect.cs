using Microsoft.Xna.Framework;

namespace Client.Main.Objects.Particles.Effects
{
    public class GravityEffect : BaseEffect
    {
        public float InitialVelocity { get; set; }
        public Vector3 CurrentVelocity { get; set; }
        public Vector3 Gravity { get; set; }

        public Vector3 MinGravity { get; set; }
        public Vector3 MaxGravity { get; set; }

        public override void Init()
        {
            Gravity = new Vector3(
               (float)(MinGravity.X + (MuGame.Random.NextDouble() * (MaxGravity.X - MinGravity.X))),
               (float)(MinGravity.Y + (MuGame.Random.NextDouble() * (MaxGravity.Y - MinGravity.Y))),
               (float)(MinGravity.Z + (MuGame.Random.NextDouble() * (MaxGravity.Z - MinGravity.Z)))
            );
            CurrentVelocity = Gravity * InitialVelocity;
        }

        public override void Update(GameTime time)
        {
            if (Particle == null) return;

            float elapsedSeconds = (float)time.ElapsedGameTime.TotalSeconds;
            CurrentVelocity += Gravity * elapsedSeconds;
            Particle.Position += CurrentVelocity * elapsedSeconds;
        }

        public override BaseEffect Copy()
        {
            return Create(MinGravity, MaxGravity, InitialVelocity);
        }

        public static GravityEffect Create(Vector3 minGravity, Vector3 maxGravity, float initialVelocity = 1f)
        {
            return new GravityEffect
            {
                InitialVelocity = initialVelocity,
                MinGravity = minGravity,
                MaxGravity = maxGravity
            };
        }

        public static GravityEffect Create(Vector3 gravity, float initialVelocity = 1f)
        {
            return Create(gravity, gravity, initialVelocity);
        }
    }
}
