using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    [MapObjectType(min: ModelType.Cannon01, max: ModelType.Cannon03)]
    public class CannonObject : WorldObject
    {
        public CannonObject()
        {
            LightEnabled = false;
        }

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            var idx = (Type - (ushort)ModelType.Cannon01 + 1).ToString().PadLeft(2, '0');
            Model = await BMDLoader.Instance.Prepare($"Object1/Cannon{idx}.bmd");
            await base.Load(graphicsDevice);
        }
    }
}
