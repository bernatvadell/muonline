using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(457, "Coolutin")]
    public class Coolutin : MonsterObject
    {
        public Coolutin()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster149.bmd");
            await base.Load();
        }
    }
}
