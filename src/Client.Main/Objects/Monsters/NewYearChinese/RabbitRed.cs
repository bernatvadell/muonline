using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters.NewYearChinese
{
    public class RabbitRed : MonsterObject
    {
        public RabbitRed()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster424.bmd");
            await base.Load();
        }
    }
}
