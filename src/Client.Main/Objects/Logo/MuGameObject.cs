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
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Logo/Logo03.bmd");
            await base.Load();
            BlendState = BlendState.Additive;
        }
    }
}
