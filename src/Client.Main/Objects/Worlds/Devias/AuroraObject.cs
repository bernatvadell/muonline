using Client.Main.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Devias
{
    public class AuroraObject : ModelObject
    {
        public override async Task Load()
        {
            BlendState = BlendState.AlphaBlend;
            LightEnabled = true;
            BlendMesh = 0;
            BlendMeshState = BlendState.Additive;
            Model = await BMDLoader.Instance.Prepare($"Object3/Object{Type + 1}.bmd");
            await base.Load();
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
        }
    }
}
