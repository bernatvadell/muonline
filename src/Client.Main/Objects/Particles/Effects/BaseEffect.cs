using Microsoft.Xna.Framework;
using System.Linq;

namespace Client.Main.Objects.Particles.Effects
{
    public abstract class BaseEffect
    {
        public Particle Particle { get; set; }
        public abstract void Init();
        public abstract void Update(GameTime time);

        public T GetEffect<T>() where T : BaseEffect
        {
            return Particle.Effects.OfType<T>().FirstOrDefault();
        }

        public abstract BaseEffect Copy();
    }
}
