using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(307, "Forest Orc")]
    public class ForestOrc : MonsterObject
    {
        public ForestOrc()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster104.bmd");
            await base.Load();
        }
    }
}
