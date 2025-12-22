using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(357, "Genocider")]
    public class Genocider : MonsterObject
    {
        public Genocider()
        {
            Scale = 1.2f;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster114.bmd");
            await base.Load();
        }
    }
}
