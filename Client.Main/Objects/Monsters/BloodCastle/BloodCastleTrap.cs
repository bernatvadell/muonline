using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters.BloodCastle
{
    [NpcInfo(104, "Blood Castle Trap")]
    public class BloodCastleTrap : MonsterObject
    {
        public BloodCastleTrap()
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
