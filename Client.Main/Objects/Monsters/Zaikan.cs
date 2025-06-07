using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(43, "Zaikan")]
    public class Zaikan : MonsterObject
    {
        public Zaikan()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster32.bmd");
            await base.Load();
        }
    }
}
