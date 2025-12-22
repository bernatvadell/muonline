using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(362, "Maya Hand Left")]
    public class MayaHandLeft : MonsterObject
    {
        public MayaHandLeft()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster119.bmd");
            await base.Load();
        }
    }
}