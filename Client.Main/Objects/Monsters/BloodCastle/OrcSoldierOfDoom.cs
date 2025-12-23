using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters.BloodCastle
{
    [NpcInfo(136, "Orc Soldier of Doom")]
    public class OrcSoldierOfDoom : MonsterObject
    {
        public OrcSoldierOfDoom()
        {
            Scale = 1.7f;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster48.bmd");
            await base.Load();
        }
    }
}
