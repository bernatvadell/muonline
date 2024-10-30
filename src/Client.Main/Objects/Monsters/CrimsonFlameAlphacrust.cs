using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    public class CrimsonFlameAlphacrust : MonsterObject
    {
        public CrimsonFlameAlphacrust()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster368.bmd");
            await base.Load();
        }
    }
}
