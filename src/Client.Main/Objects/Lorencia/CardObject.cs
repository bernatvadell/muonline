using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects.Lorencia
{
    public class CardObject : ModelObject
    {
        public CardObject()
        {
            LightEnabled = true;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Logo/Logo05.bmd");
            await base.Load();
            BlendState = BlendState.Additive;
        }
    }
}
