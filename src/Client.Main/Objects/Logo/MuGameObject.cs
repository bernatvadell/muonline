using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects.Logo
{
    public class MuGameObject : ModelObject
    {
        public MuGameObject()
        {
            LightEnabled = true;
            BlendState = BlendState.Additive;
        }


        public override async Task LoadContent()
        {
            Model = await BMDLoader.Instance.Prepare("Logo/Logo03.bmd");
            await base.LoadContent();
        }
    }
}
