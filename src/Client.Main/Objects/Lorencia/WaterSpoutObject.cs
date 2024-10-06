using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects.Lorencia
{
    public class WaterSpoutObject : ModelObject
    {
        public WaterSpoutObject()
        {
            LightEnabled = true;
            BlendMesh = 3;
        }

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            Model = await BMDLoader.Instance.Prepare($"Object1/Waterspout01.bmd");
            await base.Load(graphicsDevice);
        }
    }
}
