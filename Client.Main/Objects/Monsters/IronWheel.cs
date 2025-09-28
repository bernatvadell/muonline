using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{

    [NpcInfo(57, "IronWheel")]
    public class IronWheel : MonsterObject
    {
        public IronWheel()
        {
            Scale = 1.4f;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster42.bmd");
            await base.Load();
        }
    }
}
