using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(309, "Hell Maine")]
    public class HellMaine : MonsterObject
    {
        public HellMaine()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster106.bmd");
            await base.Load();
        }
    }
}
