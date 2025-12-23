using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters.BloodCastle
{
    [NpcInfo(132, "Statue of Saint")]
    public class StatueOfSaint1 : MonsterObject
    {
        public StatueOfSaint1()
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
