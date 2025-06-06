using Client.Main.Content;
using Client.Main.Controls;
using Client.Main.Objects.Effects;
using Client.Main.Objects.Particles;
using Client.Main.Objects.Particles.Effects;
using Microsoft.Xna.Framework;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Login
{
    public class StatueTorchObject : ModelObject
    {
        private static readonly Random _random = new Random();

        private ILogger _logger = ModelObject.AppLoggerFactory?.CreateLogger<ModelObject>();

        // The dynamic light source and unique properties for this torch instance.
        private DynamicLight _dynamicLight;
        private float _timeOffset;

        // A small height offset to place the light source where the flame is.
        private float _flameHeight = 50f;

        public StatueTorchObject()
        {
            HiddenMesh = 0;
            LightEnabled = true;

            _timeOffset = (float)_random.NextDouble() * 1000f;

            _dynamicLight = new DynamicLight
            {
                Owner = this,
                Color = new Vector3(1.0f, 0.75f, 0.45f), // Warm fire color
                Radius = 350f, // A decent radius for a torch
                Intensity = 1.0f
            };

            Children.Add(new Flare01Effect() { Scale = 2f });

            Children.Add(
                ParticleSystem.Create()
                   .SetMaxParticles(30)
                   .SetRegeneration(0.01f, 0.05f)
                   .Register<FireHik01Effect>()
                       .UseEffect(GravityEffect.Create(new Vector3(0, 0, 0.64f), new Vector3(0, 0, 0.88f), 100f))
                       .UseEffect(DurationEffect.Create(27, 32))
                       .UseEffect(BrightEffect.Create())
                       .SetScale(0.72f, 1.44f)
                       .EnableRotation()
                   .System.Register<FireHik02Effect>()
                       .UseEffect(GravityEffect.Create(new Vector3(0, 0, 0.64f), new Vector3(0, 0, 0.88f), 100f))
                       .UseEffect(DurationEffect.Create(17, 12))
                       .UseEffect(BrightEffect.Create())
                       .SetScale(0.72f, 1.44f)
                       .EnableRotation()
                   .System.Register<FireHik03Effect>()
                       .UseEffect(DurationEffect.Create(17, 22))
                       .UseEffect(GravityEffect.Create(new Vector3(0, 0, 0.64f), new Vector3(0, 0, 0.88f), 100f))
                       .UseEffect(BrightEffect.Create())
                       .SetScale(0.72f, 1.44f)
                       .EnableRotation()
                   .System
            );
        }

        public override async Task Load()
        {
            var modelPath = Path.Join("Object74", "Object80.bmd");
            Model = await BMDLoader.Instance.Prepare(modelPath);

            if (Model == null)
                _logger?.LogDebug($"Can't load MapObject for model: {modelPath}");

            // Add the dynamic light to the world's lighting system.
            if (World != null)
            {
                World.Terrain.AddDynamicLight(_dynamicLight);
            }

            await base.Load();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            // Use the unique offset time to calculate the flicker.
            float time = (float)gameTime.TotalGameTime.TotalSeconds + _timeOffset;

            // Calculate the flickering intensity for this frame.
            float luminosity = CalculateBaseLuminosity(time);

            // Update the light's properties.
            UpdateDynamicLight(luminosity);
        }

        /// <summary>
        /// Updates the position and intensity of the dynamic light source.
        /// </summary>
        private void UpdateDynamicLight(float intensity)
        {
            // The light's position is based on the object's first bone transform plus a height offset.
            // This assumes the torch flame originates from near BoneTransform[0].
            // If the model is different, this index might need to change.
            Vector3 localPosition = BoneTransform[0].Translation + new Vector3(0, 0, _flameHeight);

            _dynamicLight.Position = WorldPosition.Translation + localPosition;
            _dynamicLight.Intensity = intensity;
        }

        /// <summary>
        /// Calculates the flickering brightness of the torch flame based on time.
        /// </summary>
        private float CalculateBaseLuminosity(float time)
        {
            return 0.9f +
                   (float)Math.Sin(time * 6.0f) * 0.15f +
                   (float)Math.Sin(time * 11.0f) * 0.08f;
        }

        public override void Dispose()
        {
            // Remove the dynamic light from the world when the object is disposed.
            if (World != null)
            {
                World.Terrain.RemoveDynamicLight(_dynamicLight);
            }
            base.Dispose();
        }
    }
}