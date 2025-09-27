using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(144, "Death Angel")]
    public class DeathAngel : MonsterObject
    {
        public DeathAngel()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster64.bmd");
            await base.Load();
        }
    }
}
