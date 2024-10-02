using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    [ModelObjectType(ModelType.MerchantAnimal01, ModelType.MerchantAnimal02)]
    public class MerchantAnimalObject : ModelObject
    {
        public MerchantAnimalObject()
        {
            LightEnabled = false;
        }

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            var idx = (Type - (ushort)ModelType.MerchantAnimal01 + 1).ToString().PadLeft(2, '0');
            Model = await BMDLoader.Instance.Prepare($"Object1/MerchantAnimal{idx}.bmd");
            await base.Load(graphicsDevice);
        }
    }
}
