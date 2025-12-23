using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters.BloodCastle
{
    [NpcInfo(123, "Red Skeleton Knight")]
    public class RedSkeletonKnight5 : MonsterObject
    {
        public RedSkeletonKnight5()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster58.bmd");
            await base.Load();
        }
    }
}
