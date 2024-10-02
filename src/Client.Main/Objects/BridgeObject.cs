using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    [ModelObjectType(ModelType.Bridge)]
    public class BridgeObject : ModelObject
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

    [ModelObjectType(ModelType.BridgeStone)]
    public class BridgeStoneObject : ModelObject
    {
        public BridgeStoneObject()
        {
            LightEnabled = false;
        }

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            Model = await BMDLoader.Instance.Prepare($"Object1/BridgeStone01.bmd");
            await base.Load(graphicsDevice);
        }
    }

    [ModelObjectType(ModelType.StreetLight)]
    public class StreetLightObject : ModelObject
    {
        public StreetLightObject()
        {
            LightEnabled = false;
        }

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            Model = await BMDLoader.Instance.Prepare($"Object1/StreetLight01.bmd");
            await base.Load(graphicsDevice);
        }
    }
}
