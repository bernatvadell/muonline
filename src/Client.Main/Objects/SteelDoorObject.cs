using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    [ModelObjectType(ModelType.SteelDoor)]
    public class SteelDoorObject : ModelObject
    {
        public SteelDoorObject()
        {
            LightEnabled = false;
        }

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            Model = await BMDLoader.Instance.Prepare($"Object1/SteelDoor01.bmd");
            await base.Load(graphicsDevice);
        }
    }
}
