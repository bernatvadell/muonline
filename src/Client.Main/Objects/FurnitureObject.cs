using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    [ModelObjectType(min: ModelType.Furniture01, max: ModelType.Furniture07)]
    public class FurnitureObject : ModelObject
    {
        public FurnitureObject()
        {
            LightEnabled = false;
        }

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            var idx = (Type - (ushort)ModelType.Furniture01 + 1).ToString().PadLeft(2, '0');
            Model = await BMDLoader.Instance.Prepare($"Object1/Furniture{idx}.bmd");
            await base.Load(graphicsDevice);
        }
    }
}
