using Client.Main.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Tarkan
{
    public class LightBeamObject : ModelObject
    {
        public override async Task Load()
        {
            BlendState = BlendState.NonPremultiplied;
            BlendMesh = 0;
            BlendMeshState = BlendState.Additive;
            LightEnabled = true;
            IsTransparent = true;
            Scale = 1.5f;
            Model = await BMDLoader.Instance.Prepare($"Object9/Object83.bmd");

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