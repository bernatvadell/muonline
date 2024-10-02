using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    [ModelObjectType(min: ModelType.Tree01, max: ModelType.Tree12)]
    public class TreeObject : ModelObject
    {
        public TreeObject()
        {
            LightEnabled = false;
        }

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            var idx = (Type - (ushort)ModelType.Tree01 + 1).ToString().PadLeft(2, '0');
            Model = await BMDLoader.Instance.Prepare($"Object1/Tree{idx}.bmd");
            await base.Load(graphicsDevice);
        }
    }
}
