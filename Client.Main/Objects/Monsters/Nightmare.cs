using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(361, "Nightmare")]
    public class Nightmare : MonsterObject
    {
        public Nightmare()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster121.bmd");
            await base.Load();
        }
    }
}