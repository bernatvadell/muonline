using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    [ModelObjectType(ModelType.Stair)]
    public class StairObject : ModelObject
    {
        public StairObject()
        {
            LightEnabled = false;
        }

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            Model = await BMDLoader.Instance.Prepare($"Object1/Stair01.bmd");
            await base.Load(graphicsDevice);
        }
    }
}
