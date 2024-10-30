using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    public class Gorgon : MonsterObject
    {
        public Gorgon()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster323.bmd");
            await base.Load();
        }
    }
}
