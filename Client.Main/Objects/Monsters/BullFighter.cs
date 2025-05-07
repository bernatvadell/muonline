using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(0, "Bull Fighter")]
    public class BullFighter : MonsterObject
    {
        public BullFighter()
        {
            RenderShadow = false;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster01.bmd");
            await base.Load();
        }
    }
}
