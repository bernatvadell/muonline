using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters.BloodCastle
{
    [NpcInfo(113, "Chief Skeleton Warrior")]
    public class ChiefSkeletonWarrior4 : MonsterObject
    {
        public ChiefSkeletonWarrior4()
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
