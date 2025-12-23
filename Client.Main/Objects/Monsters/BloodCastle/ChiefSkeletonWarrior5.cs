using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters.BloodCastle
{
    [NpcInfo(119, "Chief Skeleton Warrior")]
    public class ChiefSkeletonWarrior5 : MonsterObject
    {
        public ChiefSkeletonWarrior5()
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
