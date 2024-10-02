using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    [ModelObjectType(ModelType.Straw01, ModelType.Straw02)]
    public class StrawObject : ModelObject
    {
        public StrawObject()
        {
            LightEnabled = false;
        }

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            var idx = (Type - (ushort)ModelType.Straw01 + 1).ToString().PadLeft(2, '0');
            Model = await BMDLoader.Instance.Prepare($"Object1/Straw{idx}.bmd");
            await base.Load(graphicsDevice);
        }
    }
}
