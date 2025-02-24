using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    public class Giant : MonsterObject
    {
        public Giant()
        {
            Scale = 1.8f; // TODO highlight is to big with scale
            RenderShadow = true;
        }

    public override async Task Load()
    {
        Model = await BMDLoader.Instance.Prepare($"Monster/Monster06.bmd");
        await base.Load();
    }
}
}
