using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(313, "Werewolf")]
    public class Werewolf : MonsterObject
    {
        public Werewolf()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster96.bmd");
            await base.Load();
        }
    }
}
