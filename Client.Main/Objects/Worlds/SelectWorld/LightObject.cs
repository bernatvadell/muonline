using Client.Main.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.SelectWrold
{
    public class LightObject : ModelObject
    {
        public override async Task Load()
        {
            var idx = (Type + 1).ToString().PadLeft(2, '0');
            Model = await BMDLoader.Instance.Prepare($"Object94/Object{idx}.bmd");
            BlendState = BlendState.AlphaBlend;
            BlendMesh = 2;
            BlendMeshState = BlendState.Additive;
            LightEnabled = true;
            IsTransparent = true;
            Scale = 1.1f;
            Alpha = 0.5f;
            await base.Load();
        }

        public override void Draw(GameTime gameTime)
        {
            // Save the current DepthStencilState
            DepthStencilState previousDepthState = MuGame.Instance.GraphicsDevice.DepthStencilState;
            // Set DepthStencilState to disable depth buffer writes for transparent objects
            MuGame.Instance.GraphicsDevice.DepthStencilState = MuGame.Instance.DisableDepthMask;

            base.Draw(gameTime);

            // Restore the previous DepthStencilState
            MuGame.Instance.GraphicsDevice.DepthStencilState = previousDepthState;
        }
    }
}
