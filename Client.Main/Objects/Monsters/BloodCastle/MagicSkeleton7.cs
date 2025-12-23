using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters.BloodCastle
{
    [NpcInfo(143, "Magic Skeleton")]
    public class MagicSkeleton7 : MonsterObject
    {
        public MagicSkeleton7()
        {
            Scale = 1.2f;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster63.bmd");
            await base.Load();
        }
    }
}
