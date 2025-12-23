using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters.BloodCastle
{
    [NpcInfo(428, "Chief Skeleton Warrior (Master Level)")]
    public class ChiefSkeletonWarrior8 : MonsterObject
    {
        public ChiefSkeletonWarrior8()
        {
            Scale = 1.1f;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster48.bmd");
            await base.Load();
        }
    }
}
