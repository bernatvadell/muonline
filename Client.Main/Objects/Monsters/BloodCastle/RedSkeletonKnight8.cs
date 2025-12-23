using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters.BloodCastle
{
    [NpcInfo(432, "Red Skeleton Knight (Master Level)")]
    public class RedSkeletonKnight8 : MonsterObject
    {
        public RedSkeletonKnight8()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster58.bmd");
            await base.Load();
        }
    }
}
