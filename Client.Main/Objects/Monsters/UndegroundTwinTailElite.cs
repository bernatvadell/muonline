using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    public class UndegroundTwinTailElite : MonsterObject
    {
        public UndegroundTwinTailElite()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster408.bmd");
            await base.Load();
        }
    }
}
