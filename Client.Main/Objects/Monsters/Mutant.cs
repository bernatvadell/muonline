using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(62, "Mutant")]
    public class Mutant : MonsterObject
    {
        public Mutant()
        {
            Scale = 1.5f;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster46.bmd");
            await base.Load();
        }
    }
}
