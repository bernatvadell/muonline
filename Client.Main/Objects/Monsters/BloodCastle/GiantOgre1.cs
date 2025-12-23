using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters.BloodCastle
{
    [NpcInfo(87, "Giant Ogre")]
    public class GiantOgre1 : MonsterObject
    {
        public GiantOgre1()
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
