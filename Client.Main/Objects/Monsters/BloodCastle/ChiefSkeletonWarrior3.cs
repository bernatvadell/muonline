using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters.BloodCastle
{
    [NpcInfo(96, "Chief Skeleton Warrior")]
    public class ChiefSkeletonWarrior3 : MonsterObject
    {
        public ChiefSkeletonWarrior3()
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
