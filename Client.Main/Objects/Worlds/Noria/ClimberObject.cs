using Client.Main.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Noria
{

    public class ClimberObject : ModelObject
    {
        public ClimberObject()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Object4/Object07.bmd");
            BlendState = BlendState.NonPremultiplied;
            BlendMesh = 1;
            BlendMeshState = BlendState.Additive;
            LightEnabled = true;
            IsTransparent = true;
            await base.Load();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
        }
    }
}
