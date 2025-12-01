using Client.Main.Objects.Effects; // Contains the particle system.
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Login
{
    public class WaterSplashObject : ModelObject
    {
        // Reference to the water mist particle system.
        private WaterMistParticleSystem _particleSystem;

        public override async Task Load()
        {
            _particleSystem = new WaterMistParticleSystem()
            {
                EmissionRate = 10f,
                ScaleRange = new Vector2(1.5f, 5),
                LifetimeRange = new Vector2(2f, 6f),
                HorizontalVelocityRange = new Vector2(-8f, 8f),
                UpwardVelocityRange = new Vector2(12f, 18f),
                SpawnRadius = new Vector2(40f, 40f),
                MaxDistance = 5000f,
                ScaleGrowth = 0.5f,
                Wind = new Vector2(300f, -350f),
                UpwardAcceleration = -250f,
                ParticleColor = new Color((byte)150, (byte)170, (byte)200, (byte)60)  // Zmniejszona jasność i alpha
            };
            _particleSystem.Position = Position;
            _particleSystem.World = World;  // Set World BEFORE Load()
            World.Objects.Add(_particleSystem);
            await _particleSystem.Load();
            await base.Load();
        }
    }
}