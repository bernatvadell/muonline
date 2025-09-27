using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(303, "Gigantis")]
    public class Gigantis : MonsterObject
    {
        public Gigantis()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster113.bmd");
            await base.Load();
        }
    }
}
