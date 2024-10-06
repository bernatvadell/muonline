using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Particles.Effects
{
    public class BrightEffect : BaseEffect
    {
        private DurationEffect _durationEffect;

        public override void Init()
        {
            _durationEffect = GetEffect<DurationEffect>();
            Particle.Alpha = 0;
        }

        public override void Update(GameTime time)
        {
            if (Particle == null) return;

            if (_durationEffect != null && _durationEffect.Duration < 15)
                Particle.Alpha -= FPSCounter.Instance.FPS_ANIMATION_FACTOR * 0.2f;
            if (Particle.Alpha < 1)
                Particle.Alpha += FPSCounter.Instance.FPS_ANIMATION_FACTOR * (MuGame.Random.Next() % 2 + 2) * 0.1f;
            else
                Particle.Alpha = 1;
        }

        public override BaseEffect Copy()
        {
            return Create();
        }

        public static BrightEffect Create()
        {
            return new BrightEffect();
        }
    }
}
