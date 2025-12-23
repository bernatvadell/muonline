using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters.BloodCastle
{
    [NpcInfo(137, "Orc Archer of Doom")]
    public class OrcArcherOfDoom : MonsterObject
    {
        public OrcArcherOfDoom()
        {
            Scale = 1.7f;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster47.bmd");
            await base.Load();
        }
    }
}
