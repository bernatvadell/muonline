using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters.BloodCastle
{
    [NpcInfo(106, "Laser Trap")]
    public class LaserTrap : MonsterObject
    {
        public LaserTrap()
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
