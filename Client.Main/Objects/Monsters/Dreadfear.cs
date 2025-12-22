using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(360, "Dreadfear")]
    public class Dreadfear : MonsterObject
    {
        public Dreadfear()
        {
            Scale = 1.3f;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster117.bmd");
            await base.Load();
        }
    }
}
