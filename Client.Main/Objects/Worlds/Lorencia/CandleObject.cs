using Client.Data;
using Client.Main.Content;
using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Lorencia
{
    public class CandleObject : ModelObject
    {
        private static readonly Random _random = new Random();

        // The dynamic light source for this candle.
        private DynamicLight _dynamicLight;

        // A unique time offset to desynchronize the flicker between instances.
        private float _timeOffset;

        // A small height offset to place the light source where the flame is.
        private float _flameHeight = 30f;

        public CandleObject()
        {
            LightEnabled = true;
            BlendMesh = 1;

            // Initialize the unique properties for this candle instance.
            _timeOffset = (float)_random.NextDouble() * 1000f;

            _dynamicLight = new DynamicLight
            {
                Owner = this,
                Color = new Vector3(1.0f, 0.8f, 0.5f), // A warm, yellowish color
                Radius = 250f, // Smaller radius for a small flame
                Intensity = 1.2f // Less intense than a bonfire
            };
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Object1/Candle01.bmd");
            await base.Load();

            // Add the dynamic light to the world's lighting system.
            if (World != null)
            {
                World.Terrain.AddDynamicLight(_dynamicLight);
            }
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
            // The light's position is the candle's world position plus a height offset for the flame.
            _dynamicLight.Position = WorldPosition.Translation + new Vector3(0, 0, _flameHeight);
            _dynamicLight.Intensity = intensity;
        }

        /// <summary>
        /// Calculates the flickering brightness of the candle flame based on time.
        /// </summary>
        private float CalculateBaseLuminosity(float time)
        {
            return 0.9f +
                   (float)Math.Sin(time * 1.8f) * 0.15f +
                   (float)Math.Sin(time * 3.7f) * 0.08f;
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