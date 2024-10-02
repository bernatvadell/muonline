using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    [ModelObjectType(min: ModelType.StoneWall01, max: ModelType.StoneWall06)]
    public class StoneWallObject : ModelObject
    {
        public StoneWallObject()
        {
            LightEnabled = false;
        }

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            var idx = (Type - (ushort)ModelType.StoneWall01 + 1).ToString().PadLeft(2, '0');
            Model = await BMDLoader.Instance.Prepare($"Object1/StoneWall{idx}.bmd");
            await base.Load(graphicsDevice);
        }
    }

}
