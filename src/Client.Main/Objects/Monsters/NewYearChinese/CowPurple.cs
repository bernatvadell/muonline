using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters.NewYearChinese
{
    public class CowPurple : MonsterObject
    {
        public CowPurple()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster378.bmd");
            await base.Load();
        }
    }
}
