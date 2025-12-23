using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters.BloodCastle
{
    [NpcInfo(102, "Fire Trap")]
    public class FireTrap : MonsterObject
    {
        public FireTrap()
        {
            RenderShadow = false;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Object/Object51.bmd");
            await base.Load();
        }
    }
}
