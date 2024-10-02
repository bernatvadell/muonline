using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    [ModelObjectType(min: ModelType.Carriage01, max: ModelType.Carriage04)]
    public class CarriageObject : ModelObject
    {
        public CarriageObject()
        {
            LightEnabled = false;
        }

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            var idx = (Type - (ushort)ModelType.Carriage01 + 1).ToString().PadLeft(2, '0');
            Model = await BMDLoader.Instance.Prepare($"Object1/Carriage{idx}.bmd");
            await base.Load(graphicsDevice);
        }
    }
}
