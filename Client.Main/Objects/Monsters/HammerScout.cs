using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(310, "Hammer Scout")]
    public class HammerScout : MonsterObject
    {
        public HammerScout()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster99.bmd");
            await base.Load();
        }
    }
}
