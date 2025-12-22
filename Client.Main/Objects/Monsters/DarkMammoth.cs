using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(562, "Dark Mammoth")]
    public class DarkMammoth : MonsterObject
    {
        public DarkMammoth()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster206.bmd");
            await base.Load();
        }
    }
}
