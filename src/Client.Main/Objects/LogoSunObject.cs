using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    [ModelObjectType(ModelType.LogoSun)]
    public class LogoSunObject : ModelObject
    {
        public LogoSunObject()
        {
            LightEnabled = false;
        }

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            Model = await BMDLoader.Instance.Prepare("Logo/Logo04.bmd");
            await base.Load(graphicsDevice);
        }
    }
}
