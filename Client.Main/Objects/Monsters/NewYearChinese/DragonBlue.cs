using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters.NewYearChinese
{
    public class DragonBlue : MonsterObject
    {
        public DragonBlue()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster441.bmd");
            await base.Load();
        }
    }
}
