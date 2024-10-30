using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters.NewYearChinese
{
    public class GoatWhite : MonsterObject
    {
        public GoatWhite()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster279.bmd");
            await base.Load();
        }
    }
}
