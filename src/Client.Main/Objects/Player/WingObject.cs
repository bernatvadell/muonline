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
            // link with bone 47 (see MuMain source -> file zzzCharacter -> line: 14628)
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Item/Wing02.bmd");
            await base.Load();
        }
    }
}
