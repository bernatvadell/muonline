using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters.BloodCastle
{
    [NpcInfo(100, "Lance Trap")]
    public class LanceTrap : MonsterObject
    {
        public LanceTrap()
        {
            RenderShadow = false;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Object/Object39.bmd");
            await base.Load();
        }
    }
}
