using Client.Main.Content;
using Client.Main.Controllers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.SelectWrold
{
    public class FlowersObject2 : ModelObject
    {
        public override async Task Load()
        {
            var idx = (Type + 1).ToString().PadLeft(2, '0');
            Model = await BMDLoader.Instance.Prepare($"Object94/Object{idx}.bmd");
            // Use NonPremultiplied blend state; ensure your texture is prepared accordingly
            BlendState = BlendState.NonPremultiplied;
            LightEnabled = true;
            IsTransparent = true;
            BlendMesh = 0;
            BlendMeshState = BlendState.Additive;
            await base.Load();
        }

        public override void Draw(GameTime gameTime)
        {
            // Save the current ReferenceAlpha value from the built-in AlphaTestEffect
            int previousReferenceAlpha = GraphicsManager.Instance.AlphaTestEffect3D.ReferenceAlpha;
            // Set a higher alpha threshold to clip out edge pixels (adjust the value as needed)
            GraphicsManager.Instance.AlphaTestEffect3D.ReferenceAlpha = 220;

            // Save the current sampler state for slot 0
            SamplerState previousSampler = MuGame.Instance.GraphicsDevice.SamplerStates[0];
            // Set the sampler state to LinearClamp to prevent texture bleeding from edges
            MuGame.Instance.GraphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;

            // Draw the model using the modified AlphaTestEffect parameters
            base.Draw(gameTime);

            // Restore the original sampler state and ReferenceAlpha value
            MuGame.Instance.GraphicsDevice.SamplerStates[0] = previousSampler;
            GraphicsManager.Instance.AlphaTestEffect3D.ReferenceAlpha = previousReferenceAlpha;
        }
    }
}
