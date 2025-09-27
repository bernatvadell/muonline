using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(69, "Alquamos")]
    public class Alquamos : MonsterObject
    {
        public Alquamos()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster51.bmd");
            await base.Load();
        }
    }
}
