using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(77, "Phoenix Of Darkness")]
    public class PhoenixOfDarkness : MonsterObject
    {
        public PhoenixOfDarkness()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster56.bmd");
            await base.Load();
        }
    }
}
