using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters.BloodCastle
{
    [NpcInfo(429, "Chief Skeleton Archer (Master Level)")]
    public class ChiefSkeletonArcher8 : MonsterObject
    {
        public ChiefSkeletonArcher8()
        {
            Scale = 1.1f;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster47.bmd");
            await base.Load();
        }
    }
}
