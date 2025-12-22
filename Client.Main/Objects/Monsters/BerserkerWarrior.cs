using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    public class BerserkerWarrior : MonsterObject
    {
        public BerserkerWarrior()
        {
            Scale = 1.15f;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster198.bmd");
            await base.Load();
        }
    }
}
