using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(312, "Bow Scout")]
    public class BowScout : MonsterObject
    {
        public BowScout()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster97.bmd");
            await base.Load();
        }
    }
}
