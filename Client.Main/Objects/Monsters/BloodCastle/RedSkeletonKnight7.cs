using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters.BloodCastle
{
    [NpcInfo(142, "Red Skeleton Knight")]
    public class RedSkeletonKnight7 : MonsterObject
    {
        public RedSkeletonKnight7()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster58.bmd");
            await base.Load();
        }
    }
}
