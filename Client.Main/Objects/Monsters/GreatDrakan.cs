using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(75, "Great Drakan")]
    public class GreatDrakan : MonsterObject
    {
        public GreatDrakan()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster55.bmd"); // TODO
            await base.Load();
        }
    }
}
