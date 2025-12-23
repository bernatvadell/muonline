using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters.BloodCastle
{
    [NpcInfo(117, "Red Skeleton Knight")]
    public class RedSkeletonKnight4 : MonsterObject
    {
        public RedSkeletonKnight4()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster58.bmd");
            await base.Load();
        }
    }
}
