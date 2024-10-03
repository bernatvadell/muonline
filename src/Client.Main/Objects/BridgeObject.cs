using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    [MapObjectType(ModelType.Bridge)]
    public class BridgeObject : WorldObject
    {
        public BridgeObject()
        {
            LightEnabled = false;
        }

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            Model = await BMDLoader.Instance.Prepare($"Object1/Bridge01.bmd");
            await base.Load(graphicsDevice);
        }
    }
}
