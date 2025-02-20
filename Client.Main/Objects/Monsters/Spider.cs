using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    public class Spider : MonsterObject
    {
        public Spider()
        {
            Scale = 0.4f;
            RenderShadow = true;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster10.bmd");
            await base.Load();
        }
    }
}
