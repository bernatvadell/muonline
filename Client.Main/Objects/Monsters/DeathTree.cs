using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(308, "Death Tree")]
    public class DeathTree : MonsterObject
    {
        public DeathTree()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster105.bmd");
            await base.Load();
        }
    }
}
