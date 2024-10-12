using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects.Player
{
    public class PlayerShadowObject : ModelObject
    {
        public PlayerShadowObject()
        {
            BlendMeshState = BlendState.AlphaBlend;
            BlendMesh = 0;
            Alpha = 0.5f;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Player/Shadow01.bmd");
            await base.Load();
        }
    }
}
