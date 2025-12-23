using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters.BloodCastle
{
    [NpcInfo(98, "Dark Skull Soldier")]
    public class DarkSkullSoldier3 : MonsterObject
    {
        public DarkSkullSoldier3()
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
