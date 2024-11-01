using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters.NewYearChinese
{
    public class DragonRed : MonsterObject
    {
        public DragonRed()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster443.bmd");
            await base.Load();
        }
    }
}
