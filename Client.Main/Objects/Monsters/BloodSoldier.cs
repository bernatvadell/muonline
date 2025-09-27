using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(146, "Blood Soldier")]
    public class BloodSoldier : MonsterObject
    {
        public BloodSoldier()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster66.bmd");
            await base.Load();
        }
    }
}
