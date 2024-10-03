using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    [MapObjectType(ModelType.DungeonGate)]
    public class DungeonGateObject : WorldObject
    {
        public DungeonGateObject()
        {
            LightEnabled = false;
        }

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            Model = await BMDLoader.Instance.Prepare($"Object1/DungeonGate01.bmd");
            await base.Load(graphicsDevice);
        }
    }
}
