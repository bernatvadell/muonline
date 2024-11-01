using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    public class Chicken : MonsterObject
    {
        public Chicken()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster337.bmd");
            await base.Load();
        }
    }
}
