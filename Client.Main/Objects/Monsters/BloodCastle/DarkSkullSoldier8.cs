using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters.BloodCastle
{
    [NpcInfo(430, "Dark Skull Soldier (Master Level)")]
    public class DarkSkullSoldier8 : MonsterObject
    {
        public DarkSkullSoldier8()
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
