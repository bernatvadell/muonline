using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(290, "Lizard Warrior")]
    public class LizardWarrior : MonsterObject
    {
        public LizardWarrior()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster82.bmd");
            await base.Load();
        }
    }
}
