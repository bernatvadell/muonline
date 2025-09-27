using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(569, "Venomous Chain Scorpion")]
    public class VenomousChainScorpion : MonsterObject
    {
        public VenomousChainScorpion()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster210.bmd");
            await base.Load();
        }
    }
}
