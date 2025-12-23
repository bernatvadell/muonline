using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters.BloodCastle
{
    [NpcInfo(138, "Chief Skeleton Warrior")]
    public class ChiefSkeletonWarrior7 : MonsterObject
    {
        public ChiefSkeletonWarrior7()
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
