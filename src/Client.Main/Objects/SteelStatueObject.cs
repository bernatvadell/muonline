using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    [MapObjectType(ModelType.SteelStatue)]
    public class SteelStatueObject : WorldObject
    {
        public SteelStatueObject()
        {
            LightEnabled = true;
        }

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            Model = await BMDLoader.Instance.Prepare($"Object1/SteelStatue01.bmd");
            await base.Load(graphicsDevice);
        }
    }
}
