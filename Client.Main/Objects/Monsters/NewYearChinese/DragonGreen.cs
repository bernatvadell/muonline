using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters.NewYearChinese
{
    public class DragonGreen : MonsterObject
    {
        public DragonGreen()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster440.bmd");
            await base.Load();
        }
    }
}
