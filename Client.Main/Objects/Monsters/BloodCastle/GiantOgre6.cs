using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters.BloodCastle
{
    [NpcInfo(128, "Giant Ogre")]
    public class GiantOgre6 : MonsterObject
    {
        public GiantOgre6()
        {
            Scale = 0.8f;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster59.bmd");
            await base.Load();
        }
    }
}
