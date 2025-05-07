using Client.Main.Content;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;

namespace Client.Main.Objects.NPCS.Effects
{
    public class Warp01NPCObject : ModelObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/warp01.bmd");
            BlendState = BlendState.Additive;
            Light = new Vector3(0.5f, 0.5f, 0.5f);
            await base.Load();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
        }
    }
}
