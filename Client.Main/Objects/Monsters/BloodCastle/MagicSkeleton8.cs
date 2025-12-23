using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters.BloodCastle
{
    [NpcInfo(433, "Magic Skeleton (Master Level)")]
    public class MagicSkeleton8 : MonsterObject
    {
        public MagicSkeleton8()
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
