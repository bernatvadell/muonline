using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(363, "Maya Hand Right")]
    public class MayaHandRight : MonsterObject
    {
        public MayaHandRight()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster120.bmd");
            await base.Load();
        }
    }
}