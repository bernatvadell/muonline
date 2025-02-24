using Client.Main.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Atlans
{
    public class WaterPlantObject : ModelObject
    {
        public override async Task Load()
        {
            var idx = (Type + 1).ToString().PadLeft(2, '0');
            BlendState = BlendState.AlphaBlend;
            LightEnabled = true;
            BlendMesh = 0;
            BlendMeshState = BlendState.NonPremultiplied;
            Model = await BMDLoader.Instance.Prepare($"Object8/Object{idx}.bmd");

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