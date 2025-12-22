using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(454, "Ice Walker")]
    public class IceWalker : MonsterObject
    {
        public IceWalker()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster146.bmd");
            await base.Load();
        }
    }
}
