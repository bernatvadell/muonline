using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects.Logo
{
    public class Logo05Object : ModelObject
    {
        public override async Task LoadContent()
        {
            Model = await BMDLoader.Instance.Prepare("Logo/Card04.bmd");
            await base.LoadContent();
        }
    }
}
