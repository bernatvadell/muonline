using Microsoft.Xna.Framework;
using System;

namespace Client.Main.Objects.Particles.Effects
{
    public class DurationEffect : BaseEffect
    {
        public float Duration { get; set; }
        public float MinDuration { get; set; }
        public float MaxDuration { get; set; }
        public Action<Particle> OnExpired { get; set; }

        public override void Init()
        {
            Duration = (float)(MinDuration + (MuGame.Random.NextDouble() * (MaxDuration - MinDuration)));
        }

        public override void Update(GameTime time)
        {
            Duration -= FPSCounter.Instance.FPS_ANIMATION_FACTOR;

            if (Duration <= 0)
            {
                if (OnExpired != null && Particle != null)
                {
                    OnExpired(Particle);
                }
                else
                {
                    Particle?.Dispose();
                }
                return;
            }
        }

        public override BaseEffect Copy()
        {
            return Create(MinDuration, MaxDuration);
        }

        public static DurationEffect Create(float minDuration, float maxDuration)
        {
            return new DurationEffect
            {
                MinDuration = minDuration,
                MaxDuration = maxDuration
            };
        }
    }
}
