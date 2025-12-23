using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters.BloodCastle
{
    [NpcInfo(133, "Statue of Saint")]
    public class StatueOfSaint2 : MonsterObject
    {
        public StatueOfSaint2()
        {
            Scale = 0.7f;
            RenderShadow = false;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster61.bmd");
            await base.Load();
        }
    }
}
