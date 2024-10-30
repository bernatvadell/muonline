using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    public class OldCrimsonLycan : MonsterObject
    {
        public OldCrimsonLycan()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster391.bmd");
            await base.Load();
        }
    }
}
