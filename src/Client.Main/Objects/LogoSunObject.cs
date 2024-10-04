using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    [MapObjectType(ModelType.LogoSun)]
    public class LogoSunObject : WorldObject
    {
        public LogoSunObject()
        {
            LightEnabled = true;
        }

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            Model = await BMDLoader.Instance.Prepare("Logo/Logo04.bmd");
            await base.Load(graphicsDevice);
        }
    }
}
