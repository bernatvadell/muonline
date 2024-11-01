using Client.Main.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects.Player
{
    public class WingObject : ModelObject
    {
        public WingObject()
        {
            RenderShadow = true;
            BlendMesh = -1;
            BlendState = BlendState.AlphaBlend;
            BlendMeshState = BlendState.Additive;
            Alpha = 1f;
            ParentBoneLink = 47; // link with bone 47 (see MuMain source -> file zzzCharacter -> line: 14628)
            // Position = new Vector3(0, 5, 140);
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Item/Wing02.bmd");
            await base.Load();
        }
    }
}
