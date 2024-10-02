using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    [ModelObjectType(ModelType.Bonfire)]
    public class BonfireObject : ModelObject
    {
        public BonfireObject()
        {
            LightEnabled = false;
        }

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            Model = await BMDLoader.Instance.Prepare($"Object1/Bonfire01.bmd");
            await base.Load(graphicsDevice);
        }
    }
}
