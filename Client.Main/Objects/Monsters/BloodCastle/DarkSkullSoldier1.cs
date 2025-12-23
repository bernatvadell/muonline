using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters.BloodCastle
{
    [NpcInfo(86, "Dark Skull Soldier")]
    public class DarkSkullSoldier1 : MonsterObject
    {
        public DarkSkullSoldier1()
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
