using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters.BloodCastle
{
    [NpcInfo(126, "Chief Skeleton Archer")]
    public class ChiefSkeletonArcher6 : MonsterObject
    {
        public ChiefSkeletonArcher6()
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
