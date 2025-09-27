using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(311, "Lance Scout")]
    public class LanceScout : MonsterObject
    {
        public LanceScout()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster98.bmd");
            await base.Load();
        }
    }
}
