using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects.WallObject
{
    public class WallObject : ModelObject
    {
        public WallObject()
        {
            LightEnabled = true;
            BlendState = BlendState.AlphaBlend;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Object11/Object11.bmd");
            await base.Load();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
        }
    }
}