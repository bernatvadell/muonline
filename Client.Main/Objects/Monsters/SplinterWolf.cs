using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(351, "Splinter Wolf")]
    public class SplinterWolf : MonsterObject
    {
        public SplinterWolf()
        {
            Scale = 0.8f;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster108.bmd");
            await base.Load();
        }
    }
}
