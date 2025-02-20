using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters.NewYearChinese
{
    public class DragonPurple : MonsterObject
    {
        public DragonPurple()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster442.bmd");
            await base.Load();
        }
    }
}
