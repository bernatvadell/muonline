using Client.Main.Objects.Effects; // Contains the particle system.
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.SelectWrold
{
    public class WaterSplashObject : ModelObject
    {
        // Reference to the water mist particle system.
        private WaterMistParticleSystem _particleSystem;
        public override async Task Load()
        {
            _particleSystem = new WaterMistParticleSystem()
            {
                EmissionRate = 8f,
                ScaleRange = new Vector2(0.1f, 3f),
                LifetimeRange = new Vector2(5f, 13f),
                HorizontalVelocityRange = new Vector2(-8f, 8f),
                UpwardVelocityRange = new Vector2(12f, 18f),
                SpawnRadius = new Vector2(30f, 30f),
                MaxDistance = 5000f,
                ScaleGrowth = 0.5f,
                ParticleColor = new Color((byte)150, (byte)170, (byte)200, (byte)60)  // Zmniejszona jasność i alpha
                // Wind = new Vector2(300f, -350f)
            };
            _particleSystem.Position = Position;
            _particleSystem.World = World;  // Set World BEFORE Load()
            World.Objects.Add(_particleSystem);
            await _particleSystem.Load();
            await base.Load();
        }
    }
}
