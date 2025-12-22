using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(365, "Pouch Of Blessing")]
    public class PouchOfBlessing : MonsterObject
    {
        public PouchOfBlessing()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster123.bmd");
            await base.Load();
        }
    }
}