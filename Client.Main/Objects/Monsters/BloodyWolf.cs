using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(60, "BloodyWolf")]
    public class BloodyWolf : MonsterObject
    {
        public BloodyWolf()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster44.bmd");
            await base.Load();
        }
    }
}
