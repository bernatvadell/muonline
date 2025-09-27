using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(305, "Blue Golem")]
    public class BlueGolem : MonsterObject
    {
        public BlueGolem()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster102.bmd");
            await base.Load();
        }
    }
}
