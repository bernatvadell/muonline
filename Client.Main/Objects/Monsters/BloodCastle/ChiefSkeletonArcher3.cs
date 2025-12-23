using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters.BloodCastle
{
    [NpcInfo(97, "Chief Skeleton Archer")]
    public class ChiefSkeletonArcher3 : MonsterObject
    {
        public ChiefSkeletonArcher3()
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
