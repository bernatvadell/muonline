using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects.Logo
{
    public class MuGameObject : ModelObject
    {
        public MuGameObject()
        {
            LightEnabled = true;
        }

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            Model = await BMDLoader.Instance.Prepare("Logo/Logo03.bmd");
            await base.Load(graphicsDevice);
            BlendState = BlendState.Additive;
        }
    }
}
