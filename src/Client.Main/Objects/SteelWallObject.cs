using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    [ModelObjectType(min: ModelType.SteelWall01, max: ModelType.SteelWall03)]
    public class SteelWallObject : ModelObject
    {
        public SteelWallObject()
        {
            LightEnabled = false;
        }

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            var idx = (Type - (ushort)ModelType.SteelWall01 + 1).ToString().PadLeft(2, '0');
            Model = await BMDLoader.Instance.Prepare($"Object1/SteelWall{idx}.bmd");
            await base.Load(graphicsDevice);
        }
    }
}
