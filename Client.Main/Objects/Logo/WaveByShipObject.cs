using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects.Logo
{
    public class WaveByShipObject : ModelObject
    {
        public WaveByShipObject()
        {
            LightEnabled = true;
            BlendState = BlendState.Additive;
        }

        public override async Task LoadContent()
        {
            Model = await BMDLoader.Instance.Prepare("Logo/Logo02.bmd");
            await base.LoadContent();
        }
    }

}
