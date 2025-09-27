using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(306, "Death Rider")]
    public class DeathRider : MonsterObject
    {
        public DeathRider()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster103.bmd");
            await base.Load();
        }
    }
}
