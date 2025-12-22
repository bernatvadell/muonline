using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(350, "Berserker")]
    public class Berserker : MonsterObject
    {
        public Berserker()
        {
            Scale = 0.95f;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster28.bmd");
            await base.Load();
        }
    }
}
