using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using System.Threading.Tasks;

namespace Client.Main.Objects.Logo
{
    public class LogoObject : ModelObject
    {
        public LogoObject()
        {
            LightEnabled = true;
        }

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            Model = await BMDLoader.Instance.Prepare("Logo/Logo01.bmd");
            await base.Load(graphicsDevice);
            BlendState = BlendState.Additive;
        }
    }
}
