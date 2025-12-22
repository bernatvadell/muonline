using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(345, "Death Spirit")]
    public class DeathSpirit : MonsterObject
    {
        public DeathSpirit()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster94.bmd");
            await base.Load();
        }
    }
}
