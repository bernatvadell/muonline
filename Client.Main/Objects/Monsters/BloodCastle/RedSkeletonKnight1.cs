using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters.BloodCastle
{
    [NpcInfo(88, "Red Skeleton Knight")]
    public class RedSkeletonKnight1 : MonsterObject
    {
        public RedSkeletonKnight1()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster58.bmd");
            await base.Load();
        }
    }
}
