using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    [MapObjectType(ModelType.PoseBox)]
    public class PoseBoxObject : WorldObject
    {
        public PoseBoxObject()
        {
            LightEnabled = false;
        }

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            Model = await BMDLoader.Instance.Prepare($"Object1/PoseBox01.bmd");
            await base.Load(graphicsDevice);
        }
    }
}
