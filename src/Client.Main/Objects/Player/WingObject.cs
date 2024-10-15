using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects.Player
{
    public class WingObject : ModelObject
    {
        public override int OriginBoneIndex => 47;

        public WingObject()
        {
            RenderShadow = false;
            BlendMesh = -1;
            BlendState = BlendState.AlphaBlend;
            BlendMeshState = BlendState.Additive;
            Alpha = 1f;
            // se vincula con el hueso 47 (ver zzzCharacter->14628)
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Item/Wing02.bmd");
            await base.Load();
        }
    }
}
