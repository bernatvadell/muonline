using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(370, "Jerridon")]
    public class Jerridon : MonsterObject
    {
        public Jerridon()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster128.bmd");
            await base.Load();
        }
    }
}