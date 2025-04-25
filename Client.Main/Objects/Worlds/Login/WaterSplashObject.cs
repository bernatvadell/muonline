using Client.Main.Objects.Effects; // Contains the particle system.
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Login
{
    public class WaterSplashObject : ModelObject
    {
        // Texture for the water mist particles.
        private Texture2D _particleTexture;
        // Reference to the water mist particle system.
        private WaterMistParticleSystem _particleSystem;

        public override async Task Load()
        {
            _particleTexture = MuGame.Instance.Content.Load<Texture2D>("WaterSplashParticle");
            _particleSystem = new WaterMistParticleSystem(_particleTexture)
            {
                EmissionRate = 40f,
                ParticleScaleRange = new Vector2(0.2f, 0.4f),
                ParticleLifetimeRange = new Vector2(3f, 4f),
                ParticleHorizontalVelocityRange = new Vector2(-8f, 8f),
                ParticleUpwardVelocityRange = new Vector2(12f, 18f),
                ParticlePositionOffsetRange = new Vector2(3f, 3f),
                RotateParticles = false,
                DepthScaleMax = 3f,
                DepthScaleMin = 0.7f,
                DistanceScaleExponent = 3f,
                MaxEffectiveDistance = 5000f,
                ScaleGrowthFactor = 0.5f,
                Wind = new Vector2(300f, -350f),
                UpwardAcceleration = -250f
            };
            _particleSystem.Position = Position;
            _particleSystem.Emit(Position, 50);
            World.Objects.Add(_particleSystem);
            await base.Load();
        }
    }
}