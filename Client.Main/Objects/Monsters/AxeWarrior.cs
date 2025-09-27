using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(294, "Axe Warrior")]
    public class AxeWarrior : MonsterObject
    {
        public AxeWarrior()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster86.bmd");
            await base.Load();
        }
    }
}
