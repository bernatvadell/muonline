using Client.Main.Content;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

namespace Client.Main.Objects.Worlds.Login
{
    public class WaterSplashObject : ModelObject
    {
        public override async Task Load()
        {
            var idx = (Type + 1).ToString().PadLeft(2, '0');
            Model = await BMDLoader.Instance.Prepare($"Object95/Object{idx}.bmd");
            await base.Load();
        }
        public override void Draw(GameTime gameTime)
        {
            //TODO add effect
            // base.Draw(gameTime); 
        }
    }
}
