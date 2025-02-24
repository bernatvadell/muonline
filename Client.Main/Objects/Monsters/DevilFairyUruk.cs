using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    internal class DevilFairyUruk : MonsterObject
    {
        public DevilFairyUruk()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster260.bmd");
            await base.Load();
        }
    }
}
