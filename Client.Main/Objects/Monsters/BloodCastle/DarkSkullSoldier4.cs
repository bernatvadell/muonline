using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters.BloodCastle
{
    [NpcInfo(115, "Dark Skull Soldier")]
    public class DarkSkullSoldier4 : MonsterObject
    {
        public DarkSkullSoldier4()
        {
            Scale = 1.0f;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster60.bmd");
            await base.Load();
        }
    }
}
