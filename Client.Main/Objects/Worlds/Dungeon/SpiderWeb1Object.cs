using Client.Main.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Dungeon
{
    public class SpiderWeb1Object : ModelObject
    {
        public override async Task Load()
        {
            BlendState = BlendState.Additive;
            LightEnabled = true;
            IsTransparent = true;
            Model = await BMDLoader.Instance.Prepare($"Object2/Object02.bmd");

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