using Client.Main.Content;
using Client.Main.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Tarkan
{
    public class LavaObject : ModelObject
    {
        public override async Task Load()
        {
            BlendState = Blendings.InverseDestinationBlend;
            LightEnabled = true;
            IsTransparent = true;
            Scale = 1.0f;
            Model = await BMDLoader.Instance.Prepare($"Object9/Object08.bmd");

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