using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(131, "Castle Gate")]
    public class BloodCastleGate : MonsterObject
    {
        public BloodCastleGate()
        {
            Scale = 0.8f;
            RenderShadow = false;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster62.bmd");
            await base.Load();
        }
    }
}
