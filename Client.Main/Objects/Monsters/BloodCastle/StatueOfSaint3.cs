using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters.BloodCastle
{
    [NpcInfo(134, "Statue of Saint")]
    public class StatueOfSaint3 : MonsterObject
    {
        public StatueOfSaint3()
        {
            Scale = 0.9f;
            RenderShadow = false;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster61.bmd");
            await base.Load();
        }
    }
}
