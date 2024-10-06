using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects.Lorencia
{
    public class StreetLightObject : ModelObject
    {
        public StreetLightObject()
        {
            LightEnabled = true;
            BlendMesh = 1;
        }

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            Model = await BMDLoader.Instance.Prepare($"Object1/StreetLight01.bmd");
            await base.Load(graphicsDevice);
        }
    }
}
