using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    public class GoldenDragon : MonsterObject
    {
        public GoldenDragon()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster32.bmd");
            await base.Load();
        }
    }
}
