using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters.BloodCastle
{
    [NpcInfo(90, "Chief Skeleton Warrior")]
    public class ChiefSkeletonWarrior2 : MonsterObject
    {
        public ChiefSkeletonWarrior2()
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
