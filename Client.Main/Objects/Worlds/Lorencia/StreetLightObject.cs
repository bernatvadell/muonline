using Client.Data;
using Client.Main.Content;
using Client.Main.Controls;
using Client.Main.Controls.Terrain;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Linq;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Lorencia
{
    public class StreetLightObject : ModelObject
    {
        private DynamicLight _dynamicLight;
        private bool _lightAdded = false;

        public StreetLightObject()
        {
            LightEnabled = true;
            BlendMesh = 1;
            BlendMeshState = BlendState.Additive;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Object1/StreetLight01.bmd");

            // Create dynamic light for street lamp
            _dynamicLight = new DynamicLight
            {
                Owner = this,
                Color = new Vector3(1f, 0.9f, 0.7f), // Warm white light
                Radius = 300f,
                Intensity = 2.8f,
                Position = Vector3.Zero
            };

            await base.Load();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            // Update light position to match object position
            if (_dynamicLight != null && World?.Terrain != null)
            {
                // Position light slightly above the lamp (adjust Y offset as needed)
                _dynamicLight.Position = WorldPosition.Translation + new Vector3(0f, 100f, 0f);

                // Add to terrain lighting system once
                if (!_lightAdded)
                {
                    World.Terrain.AddDynamicLight(_dynamicLight);
                    _lightAdded = true;
                }
            }
        }

        public override void Dispose()
        {
            if (_dynamicLight != null && World?.Terrain != null)
            {
                World.Terrain.RemoveDynamicLight(_dynamicLight);
                _dynamicLight = null;
            }
            base.Dispose();
        }
    }
}
