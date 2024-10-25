using Client.Main.Content;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    public class Monster446 : MonsterObject
    {
        public Monster446()
        {
            RenderShadow = true;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster446.bmd");
            await base.Load();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            BlendMesh = 4;
        }
    }
}
