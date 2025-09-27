using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(59, "Zaikan")]
    public class Zaikan : MonsterObject
    {
        public Zaikan()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster43.bmd");
            await base.Load();
            //TODO Zaikan uses tantalos model with some different blending options
        }
    }
}
