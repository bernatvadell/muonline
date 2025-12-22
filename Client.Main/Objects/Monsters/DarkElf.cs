using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(340, "Dark Elf")]
    public class DarkElf : MonsterObject
    {
        public DarkElf()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster127.bmd");
            await base.Load();
        }
    }
}
