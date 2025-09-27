using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(292, "Queen Bee")]
    public class QueenBee : MonsterObject
    {
        public QueenBee()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster84.bmd");
            await base.Load();
        }
    }
}
