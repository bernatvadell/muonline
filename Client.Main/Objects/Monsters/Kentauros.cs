using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(355, "Kentauros")]
    public class Kentauros : MonsterObject
    {
        public Kentauros()
        {
            Scale = 1.1f;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster112.bmd");
            await base.Load();
        }
    }
}
