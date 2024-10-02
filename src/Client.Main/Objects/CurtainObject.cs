using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    [ModelObjectType(ModelType.Curtain)]
    public class CurtainObject : ModelObject
    {
        public CurtainObject()
        {
            LightEnabled = false;
        }

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            // the ceiling of the bar.
            Model = await BMDLoader.Instance.Prepare($"Object1/Curtain01.bmd");
            await base.Load(graphicsDevice);
        }
    }
}
